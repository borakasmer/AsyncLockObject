using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;

namespace AsyncLock
{
    public class DistributedCacheWrapper
    {
        private static readonly AsyncLock m_lock = new AsyncLock();
        private readonly IDistributedCache _cache;
        public DistributedCacheWrapper(IDistributedCache cache)
        {
            _cache = cache;
        }

        public Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            return _cache.GetAsync(key, token);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            return _cache.SetAsync(key, value, options, token);
        }
        public Task<string> GetStringAsync(string key, CancellationToken token = default)
        {
            return _cache.GetStringAsync(key, token);
        }

        public async Task<T> GetOrSet<T>(string cacheKey, Func<T> fillAction, int cacheTime, string sender, CancellationToken token = default)
        where T : class
        {
            var resultBytes = await _cache.GetAsync(cacheKey);
            if (resultBytes == null)
            {
                using (await m_lock.LockAsync())
                {
                    resultBytes = await _cache.GetAsync(cacheKey);
                    if (resultBytes == null)
                    {
                        var allData = fillAction();
                        var data = JsonConvert.SerializeObject(allData);
                        var dataByte = Encoding.UTF8.GetBytes(data);
                        var option = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheTime));
                        option.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheTime);
                        Console.WriteLine($"Sender {sender} Time: {DateTime.Now}");
                        await _cache.SetAsync(cacheKey, dataByte, option);
                    }
                }
            }
            var cachedString = await _cache.GetStringAsync(cacheKey);
            return JsonConvert.DeserializeObject<T>(cachedString);
        }
    }
}
