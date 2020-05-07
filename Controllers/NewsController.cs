using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections;

namespace AsyncLock.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NewsController : ControllerBase
    {
        private static readonly AsyncLock m_lock = new AsyncLock();

        //dotnet add package Microsoft.Extensions.Caching.Redis (For using IDistributedCache)
        private readonly IDistributedCache _distributedCache;
        public static List<News> model = new List<News>();
        public NewsController(IDistributedCache distributedCache)
        {
            _distributedCache = distributedCache;
            if (model.Count == 0)
            {
                model.AddRange(new News[]
                {
                new News(){
                ID = 1,
                Title = "'Cool' expectations from the new year",
                Detail = @"The coldest month in places where the Mediterranean climate is dominant, such as Istanbul There is a strong chance of snow in February. Other times, for a short time we will see snowfalls and sunny but high air pollution days.",
                CreatedDate = DateTime.Now,
                Image="weather.jpg"
                },
                new News(){
                ID = 2,
                Title = "The most popular addresses of 2020",
                Detail = @"Every year the number of passengers is growing rapidly in Turkey. Also in 2020 this increase is expected to continue. Now we are entering a new year. Where in 2020 Tourism agencies and some expert writers decide what to go with.",
                CreatedDate = DateTime.Now,
                Image="place.jpg"
                },
                 new News(){
                ID = 3,
                Title = "Why are people of nature always the happiest and healthiest people?",
                Detail = @"Scientists spend at least half an hour a day near trees people firstly develop mentally He also says that their physical reflections are realized.",
                CreatedDate = DateTime.Now,
                Image="nature.jpg"
                },
                 new News(){
                ID = 4,
                Title = "Do you have a virus on your computer? Here is the way to understand",
                Detail = @"Your last illness, some problems with your health can be a pointer. Similarly, a virus that infects your computer, can give a series of signs. A single sign you see is not caused by the virus it may happen, but a few symptoms mean you need to hear the bells of danger.",
                CreatedDate = DateTime.Now,
                Image="virus.jpg"
                }
                });
            }
        }
        [HttpGet]
        public async Task<List<News>> News()
        {
            string cacheKey = "AsyncNewsData";
            int cacheTime = 30;
            var allTasks = new List<Task<List<News>>>();
            var client1 = AddRedisCache(model, cacheTime, cacheKey, "Client 1");
            var client2 = AddRedisCache(model, cacheTime, cacheKey, "Client 2");
            var client3 = AddRedisCache(model, cacheTime, cacheKey, "Client 3");

            allTasks.AddRange(new List<Task<List<News>>>() { client1, client2, client3 });

            await Task.WhenAll(allTasks);
            return allTasks.First().Result;
            //var client1 = await AddRedisCache(model, cacheTime, cacheKey, "Client 1");
            //return client1;
        }

        public async Task<List<News>> AddRedisCache(List<News> allData, int cacheTime, string cacheKey, string sender = "", int errorValue = 2)
        {
            //if (sender == "Client 3") { throw new Exception(); }
            var dataNews = await _distributedCache.GetAsync(cacheKey);
            if (dataNews == null)
            {
                using (await m_lock.LockAsync())
                {
                    dataNews = await _distributedCache.GetAsync(cacheKey);
                    if (dataNews == null)
                    {
                        var data = JsonConvert.SerializeObject(allData);
                        var dataByte = Encoding.UTF8.GetBytes(data);

                        var option = new DistributedCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromSeconds(cacheTime));
                        option.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheTime);
                        Console.WriteLine("Sender :" + sender + " Time: " + DateTime.Now);
                        await _distributedCache.SetAsync(cacheKey, dataByte, option);
                    }
                }
            }
            var newsString = await _distributedCache.GetStringAsync(cacheKey);
            return JsonConvert.DeserializeObject<List<News>>(newsString);
        }

        public sealed class AsyncLock
        {
            private readonly SemaphoreSlim m_semaphore = new SemaphoreSlim(1, 1);
            private readonly Task<IDisposable> m_releaser;

            public AsyncLock()
            {
                m_releaser = Task.FromResult((IDisposable)new Releaser(this));
            }

            public Task<IDisposable> LockAsync()
            {
                var wait = m_semaphore.WaitAsync();
                return wait.IsCompleted ?
                            m_releaser :
                            wait.ContinueWith((_, state) => (IDisposable)state,
                                m_releaser.Result, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }

            private sealed class Releaser : IDisposable
            {
                private readonly AsyncLock m_toRelease;
                internal Releaser(AsyncLock toRelease) { m_toRelease = toRelease; }
                public void Dispose() { m_toRelease.m_semaphore.Release(); }
            }
        }
    }
}
