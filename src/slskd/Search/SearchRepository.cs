using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace slskd.Search
{
    public class SearchRepository : ISearchRepository
    {
        private const string SearchCacheKey = "SearchCache";
        private readonly IDbContextFactory<SearchDbContext> _contextFactory;
        private readonly IMemoryCache _memoryCache;

        public SearchRepository(IDbContextFactory<SearchDbContext> contextFactory, IMemoryCache memoryCache)
        {
            _contextFactory = contextFactory;
            _memoryCache = memoryCache;
        }

        public async Task AddAsync(Search search)
        {
            using var context = _contextFactory.CreateDbContext();
            context.Add(search);
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Search search)
        {
            using var context = _contextFactory.CreateDbContext();
            context.Searches.Remove(search);
            await context.SaveChangesAsync();
            _memoryCache.Remove($"{SearchCacheKey}-{search.Id}");
        }

        public async Task<Search> FindAsync(Expression<Func<Search, bool>> expression, bool includeResponses = false)
        {
            if (expression.Body is BinaryExpression binaryExpression &&
                binaryExpression.NodeType == ExpressionType.Equal &&
                binaryExpression.Left is MemberExpression leftMember && leftMember.Member.Name == "Id" &&
                binaryExpression.Right is ConstantExpression rightConstant && rightConstant.Value is Guid searchId)
            {
                if (TryGetCachedSearch(searchId, out var cachedSearch))
                {
                    return cachedSearch;
                }
            }

            using var context = _contextFactory.CreateDbContext();
            var query = context.Searches.AsNoTracking().Where(expression);

            if (!includeResponses)
            {
                query = query.WithoutResponses();
            }

            return await query.FirstOrDefaultAsync();
        }

        public async Task<List<Search>> ListAsync(Expression<Func<Search, bool>> expression = null)
        {
            expression ??= s => true;
            using var context = _contextFactory.CreateDbContext();
            return await context.Searches
                .AsNoTracking()
                .Where(expression)
                .WithoutResponses()
                .ToListAsync();
        }

        public async Task UpdateAsync(Search search)
        {
            using var context = _contextFactory.CreateDbContext();
            context.Update(search);
            await context.SaveChangesAsync();
        }

        public async Task<int> PruneAsync(int age)
        {
            using var context = _contextFactory.CreateDbContext();
            var cutoffDateTime = DateTime.UtcNow.AddMinutes(-age);
            var expired = await context.Searches
                .Where(s => s.EndedAt.HasValue && s.EndedAt.Value < cutoffDateTime)
                .WithoutResponses()
                .ToListAsync();

            foreach (var search in expired)
            {
                await DeleteAsync(search);
            }

            return expired.Count;
        }

        public void CacheSearch(Search search)
        {
            _memoryCache.Set($"{SearchCacheKey}-{search.Id}", search, TimeSpan.FromMinutes(5));
        }

        public bool TryGetCachedSearch(Guid id, out Search search)
        {
            return _memoryCache.TryGetValue($"{SearchCacheKey}-{id}", out search);
        }
    }
}
