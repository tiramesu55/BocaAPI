using BocaAPI.Interfaces;
using BocaAPI.Models;
using BocaAPI.Models.DTO;
using Microsoft.Extensions.Caching.Memory;

namespace BocaAPI.Services
{
    public class CacheService: ICacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IBocaRepository _bocaRepository;

        public CacheService(IMemoryCache memoryCache, IBocaRepository bocaRepository)
        {
            _memoryCache = memoryCache;
            _bocaRepository = bocaRepository;
        }

        private async Task<List<T>> Get<T>(Func<Task<List<T>>> seedFunc)
        {
            if (!_memoryCache.TryGetValue(nameof(T), out List<T> cacheEntry))
            {
                cacheEntry = await seedFunc();
                _memoryCache.Set(nameof(T), cacheEntry, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2) });
            }
            return cacheEntry;
        }


        public async Task<List<PoliceCode>> GetPoliceCodes() => await Get(() => _bocaRepository.GetPoliceCodes());

    }
}
