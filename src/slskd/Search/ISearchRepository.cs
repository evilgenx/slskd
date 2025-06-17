namespace slskd.Search
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;

    public interface ISearchRepository
    {
        Task AddAsync(Search search);
        Task DeleteAsync(Search search);
        Task<Search> FindAsync(Expression<Func<Search, bool>> expression, bool includeResponses = false);
        Task<List<Search>> ListAsync(Expression<Func<Search, bool>> expression = null);
        Task UpdateAsync(Search search);
        Task<int> PruneAsync(int age);
        void CacheSearch(Search search);
        bool TryGetCachedSearch(Guid id, out Search search);
    }
}
