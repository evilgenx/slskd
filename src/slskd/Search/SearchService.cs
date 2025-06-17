﻿// <copyright file="SearchService.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

using Microsoft.Extensions.Options;

namespace slskd.Search
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.SignalR;
    using Serilog;
    using slskd.Search.API;
    using Soulseek;
    using SearchOptions = Soulseek.SearchOptions;
    using SearchQuery = Soulseek.SearchQuery;
    using SearchScope = Soulseek.SearchScope;
    using SearchStates = Soulseek.SearchStates;

    /// <summary>
    ///     Handles the lifecycle and persistence of searches.
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        ///     Deletes the specified search.
        /// </summary>
        /// <param name="search">The search to delete.</param>
        /// <returns>The operation context.</returns>
        Task DeleteAsync(Search search);

        /// <summary>
        ///     Finds a single search matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match searches.</param>
        /// <param name="includeResponses">A value indicating whether to include search responses in the result.</param>
        /// <returns>The found search, or default if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when an expression is not supplied.</exception>
        Task<Search> FindAsync(Expression<Func<Search, bool>> expression, bool includeResponses = false);

        /// <summary>
        ///     Returns a list of all completed and in-progress searches, with responses omitted, matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match searches.</param>
        /// <returns>The list of searches matching the specified expression, or all searches if no expression is specified.</returns>
        Task<List<Search>> ListAsync(Expression<Func<Search, bool>> expression = null);

        /// <summary>
        ///     Updates the specified <paramref name="search"/>.
        /// </summary>
        /// <remark>
        ///     Round-trips the database; use accordingly.
        /// </remark>
        /// <param name="search">The search to update.</param>
        void Update(Search search);

        /// <summary>
        ///     Performs a search for the specified <paramref name="query"/> and <paramref name="scope"/>.
        /// </summary>
        /// <param name="id">A unique identifier for the search.</param>
        /// <param name="query">The search query.</param>
        /// <param name="scope">The search scope.</param>
        /// <param name="options">Search options.</param>
        /// <returns>The completed search.</returns>
        Task<Search> StartAsync(Guid id, SearchQuery query, SearchScope scope, SearchOptions options = null);

        /// <summary>
        ///     Cancels the search matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the search.</param>
        /// <returns>A value indicating whether the search was successfully cancelled.</returns>
        bool TryCancel(Guid id);

        /// <summary>
        ///     Removes <see cref="SearchStates.Completed"/> searches older than the specified <paramref name="age"/>.
        /// </summary>
        /// <param name="age">The age after which records are eligible for pruning, in minutes.</param>
        /// <returns>The number of pruned records.</returns>
        Task<int> PruneAsync(int age);
    }

    /// <summary>
    ///     Handles the lifecycle and persistence of searches.
    /// </summary>
    public class SearchService : ISearchService
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SearchService"/> class.
        /// </summary>
        /// <param name="searchHub"></param>
        /// <param name="optionsMonitor"></param>
        /// <param name="soulseekClient"></param>
        /// <param name="repository">The search repository to use.</param>
        /// <param name="userService">The user service to use.</param>
        public SearchService(
            IHubContext<SearchHub> searchHub,
            IOptionsMonitor<Options> optionsMonitor,
            ISoulseekClient soulseekClient,
            ISearchRepository repository,
            Users.IUserService userService)
        {
            SearchHub = searchHub;
            OptionsMonitor = optionsMonitor;
            Client = soulseekClient;
            Repository = repository;
            UserService = userService;
        }

        private ConcurrentDictionary<Guid, CancellationTokenSource> CancellationTokens { get; }
            = new ConcurrentDictionary<Guid, CancellationTokenSource>();

        private ISoulseekClient Client { get; }
        private ILogger Log { get; set; } = Serilog.Log.ForContext<Application>();
        private IOptionsMonitor<Options> OptionsMonitor { get; }
        private IHubContext<SearchHub> SearchHub { get; set; }
        private Users.IUserService UserService { get; }
        private ISearchRepository Repository { get; }

        /// <summary>
        ///     Deletes the specified search.
        /// </summary>
        /// <param name="search">The search to delete.</param>
        /// <returns>The operation context.</returns>
        public async Task DeleteAsync(Search search)
        {
            if (search == default)
            {
                throw new ArgumentNullException(nameof(search));
            }

            await Repository.DeleteAsync(search);
            await SearchHub.BroadcastDeleteAsync(search);
        }

        /// <summary>
        ///     Finds a single search matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to use to match searches.</param>
        /// <param name="includeResponses">A value indicating whether to include search responses in the result.</param>
        /// <returns>The found search, or default if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when an expression is not supplied.</exception>
        public async Task<Search> FindAsync(Expression<Func<Search, bool>> expression, bool includeResponses = false)
        {
            if (expression == default)
            {
                throw new ArgumentException("An expression must be supplied.", nameof(expression));
            }

            return await Repository.FindAsync(expression, includeResponses);
        }

        /// <summary>
        ///     Returns a list of all completed and in-progress searches, with responses omitted, matching the optional <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to match searches.</param>
        /// <returns>The list of searches matching the specified expression, or all searches if no expression is specified.</returns>
        public Task<List<Search>> ListAsync(Expression<Func<Search, bool>> expression = null)
        {
            return Repository.ListAsync(expression);
        }

        /// <summary>
        ///     Updates the specified <paramref name="search"/>.
        /// </summary>
        /// <param name="search">The search to update.</param>
        public void Update(Search search)
        {
            Repository.UpdateAsync(search).GetAwaiter().GetResult();
        }

        /// <summary>
        ///     Performs a search for the specified <paramref name="query"/> and <paramref name="scope"/>.
        /// </summary>
        /// <param name="id">A unique identifier for the search.</param>
        /// <param name="query">The search query.</param>
        /// <param name="scope">The search scope.</param>
        /// <param name="options">Search options.</param>
        /// <returns>The completed search.</returns>
        public async Task<Search> StartAsync(Guid id, SearchQuery query, SearchScope scope, SearchOptions options = null)
        {
            var token = Client.GetNextToken();
            var cancellationTokenSource = new CancellationTokenSource();
            CancellationTokens.TryAdd(id, cancellationTokenSource);
            var rateLimiter = new RateLimiter(250);

            // Initialize search record
            var search = new Search()
            {
                SearchText = query.SearchText,
                Token = token,
                Id = id,
                State = SearchStates.Requested,
                StartedAt = DateTime.UtcNow,
                FileTypes = ExtractFileTypes(query.SearchText)
            };

            try
            {
                await Repository.AddAsync(search);
                await SearchHub.BroadcastCreateAsync(search);

                List<SearchResponse> responses = new();
                options ??= new SearchOptions();
                
                options = options.WithActions(
                    stateChanged: (args) => HandleStateChange(args, search, id),
                    responseReceived: (args) => rateLimiter.Invoke(() => HandleResponseReceived(args, search)));

                var soulseekSearchTask = Client.SearchAsync(
                    query,
                    responseHandler: responses.Add,
                    scope,
                    token,
                    options,
                    cancellationToken: cancellationTokenSource.Token);

                _ = Task.Run(async () =>
                {
                    var soulseekSearch = await soulseekSearchTask;
                    search = search.WithSoulseekSearch(soulseekSearch);
                    Log.Debug("Search for '{Query}' ended normally (id: {Id})", query, id);
                }, cancellationTokenSource.Token)
                .ContinueWith(async task => await FinalizeSearch(task, search, responses, id, query, rateLimiter));

                await SearchHub.BroadcastUpdateAsync(search);
                return search;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to execute search {Search}: {Message}", new { query, scope, options }, ex.Message);
                await HandleFailedSearch(search, query);
                throw;
            }
        }

        private List<string> ExtractFileTypes(string searchText)
        {
            var fileTypes = new List<string>();
            var typePatterns = new Dictionary<string, string[]>
            {
                { "audio", new[] { "mp3", "flac", "wav", "aac", "ogg", "m4a" } },
                { "video", new[] { "mp4", "mkv", "avi", "mov", "wmv" } },
                { "document", new[] { "pdf", "doc", "docx", "txt", "rtf" } },
                { "image", new[] { "jpg", "jpeg", "png", "gif", "bmp" } },
                { "archive", new[] { "zip", "rar", "7z", "tar", "gz" } }
            };

            foreach (var type in typePatterns)
            {
                if (type.Value.Any(ext => 
                    searchText.Contains($" .{ext} ", StringComparison.OrdinalIgnoreCase) || 
                    searchText.EndsWith($".{ext}", StringComparison.OrdinalIgnoreCase)))
                {
                    fileTypes.Add(type.Key);
                }
            }

            return fileTypes;
        }

        private void HandleStateChange((Soulseek.SearchStates PreviousState, Soulseek.Search Search) args, Search search, Guid id)
        {
            search = search.WithSoulseekSearch(args.Search);
            SearchHub.BroadcastUpdateAsync(search);
            Update(search);
            Log.Debug("Search state changed: {State} (id: {Id})", search.State, id);
        }

        private void HandleResponseReceived((Soulseek.Search Search, Soulseek.SearchResponse Response) args, Search search)
        {
            search.ResponseCount = args.Search.ResponseCount;
            search.FileCount = args.Search.FileCount;
            search.LockedFileCount = args.Search.LockedFileCount;
            SearchHub.BroadcastUpdateAsync(search);
            Update(search);
        }

        private async Task FinalizeSearch(Task task, Search search, List<SearchResponse> responses, Guid id, SearchQuery query, RateLimiter rateLimiter)
        {
            try
            {
                if (task.IsFaulted)
                {
                    Log.Error(task.Exception, "Failed to execute search for '{Query}' (id: {Id}): {Message}", query, id, task.Exception?.Message ?? "Task completed in Faulted state, but there is no Exception");
                    search.State = SearchStates.Completed | SearchStates.Errored;
                }

                search.EndedAt = DateTime.UtcNow;
                search.Responses = responses.Select(r => 
                {
                    var response = Response.FromSoulseekSearchResponse(r);
                    response.UserGroup = UserService.GetGroup(response.Username);
                    return response;
                }).ToList();

                Update(search);
                Repository.CacheSearch(search);
                await SearchHub.BroadcastUpdateAsync(search with { Responses = new List<Response>() });
                Log.Debug("Search for '{Query}' finalized (id: {Id})", query, id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to finalize search for '{Query}' (id: {Id}): {Message}", query, id, ex.Message);
                throw;
            }
            finally
            {
                rateLimiter.Dispose();
                CancellationTokens.TryRemove(id, out _);
            }
        }

        private async Task HandleFailedSearch(Search search, SearchQuery query)
        {
            if (search.Id != Guid.Empty) // Check if search was persisted
            {
                search.State = SearchStates.Completed | SearchStates.Errored;
                search.EndedAt = DateTime.UtcNow;
                Update(search);
                await SearchHub.BroadcastUpdateAsync(search with { Responses = new List<Response>() });
            }
        }

        /// <summary>
        ///     Cancels the search matching the specified <paramref name="id"/>, if it is in progress.
        /// </summary>
        /// <param name="id">The unique identifier for the search.</param>
        /// <returns>A value indicating whether the search was successfully cancelled.</returns>
        public bool TryCancel(Guid id)
        {
            if (CancellationTokens.TryGetValue(id, out var cts))
            {
                cts.Cancel();
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Removes <see cref="SearchStates.Completed"/> searches older than the specified <paramref name="age"/>.
        /// </summary>
        /// <param name="age">The age after which searches are eligible for pruning, in minutes.</param>
        /// <returns>The number of pruned records.</returns>
        public async Task<int> PruneAsync(int age)
        {
            return await Repository.PruneAsync(age);
        }
    }
}
