using RedisPractice.Exceptions;
using RedisPractice.Provider;
using RedisPractice.Provider.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RedisPractice.Utility
{

    /// <summary>
    /// 傳入要使用的 CacheProvider, 依順序取用
    /// </summary>
    public class Cache : ICache
    {
        public TimeSpan DefaultExpired => new TimeSpan(0, 2280, 0);

        public TimeSpan NeverExpired => new TimeSpan(99, 0, 0);

        private readonly ILogger logger;

        private readonly ICacheProvider[] cacheProviders;

        /// <summary>
        /// 在多級 cache 的情況下，
        /// 在第幾級取得資料，
        /// 如果不是在第 0 級取得，需要往下級回寫資料
        /// </summary>
        private int GetDataLevel = 0;

        /// <summary>
        /// 只啟用 RedisCache
        /// </summary>
        public Cache()
        {
            logger = new TextLogger();

            cacheProviders = new ICacheProvider[] { new RedisCacheProvider() };
        }


        /// <summary>
        /// 使用另一種 CacheProvider 輔助 RedisCacheProvider 
        /// </summary>
        /// <param name="memoryCacheProvider"></param>
        public Cache(ICacheProvider cacheProvider)
        {
            logger = new TextLogger();

            cacheProviders = new List<ICacheProvider>()
            {
                new RedisCacheProvider(),
                cacheProvider
            }.OrderBy(t => t.CachePriority).ToArray();

        }

        public Cache(ICacheProvider[] cacheProvidersParam)
        {
            logger = new TextLogger();

            List<ICacheProvider> providerList = cacheProvidersParam.ToList();

            // 沒有 RedisCacheProvider
            //if(providerList.FirstOrDefault(t=>t.GetType() == typeof(RedisCacheProvider)) == null)
            //{
            //    providerList.Add(new RedisCacheProvider());
            //}

            cacheProviders = providerList
                .OrderBy(t => t.CachePriority)
                .ToArray();
        }

        /// <summary>
        /// 非同步寫入每一個 CacheProvider
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeSpan"></param>
        public void Add(string key, string value, TimeSpan timeSpan = default)
        {
            foreach (ICacheProvider cache in cacheProviders)
            {
                cache.SetAsync(key, value, timeSpan);
            }
        }

        /// <summary>
        /// 快取失敗時，回寫資料
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        private void WriteBackData<T>(string key, T value, TimeSpan expired)
        {
            for (int i = 0; i < GetDataLevel; i++)
            {
                cacheProviders[i].SetAsync(key, value, expired);
            }
        }

        /// <summary>
        /// 快取失敗時，回寫資料 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        private void WriteBackHash<T>(string key, string hashKey, T value, TimeSpan expired)
        {
            for (int i = 0; i < GetDataLevel; i++)
            {
                cacheProviders[i].HashSetAsync(key, hashKey, value, expired);
            }

        }

        /// <summary>
        /// 紀錄 Exception
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="cache"></param>
        /// <param name="method"></param>
        /// <param name="data"></param>
        private void ExceptionLogHandler(Exception ex, ICacheProvider cache, string method, string data)
        {

            Dictionary<string, string> details = new Dictionary<string, string>
                    {
                        { "CacheProvider", cache.GetType().Name },
                        { "Method", method },
                        { "Data", data },
                        { "exception", ex.Message }
                    };

            logger.WriteErrorLog(new OperationFailedException(EnumType.ResultCodeEnum.ConnectCacheFail), details);
        }

        /// <summary>
        /// 取得 string 類型的快取值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Get(string key)
        {
            string result = Get<string>(key);

            return result;
        }



        /// <summary>
        /// 取得泛型類型的快取值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Get<T>(string key) where T : class
        {

            // 重設取得資料的階層
            GetDataLevel = 0;

            T result = default;

            List<Exception> exceptions = new List<Exception>();

            foreach (ICacheProvider cache in cacheProviders)
            {
                try
                {
                    result = cache.Get<T>(key);
                }
                catch (Exception ex)
                {

                    ExceptionLogHandler(ex, cache, "T Get<T>", $"{{ \"key:\" \"{ key }\"}}");

                    exceptions.Add(ex);

                }

                // 已經取得資料，跳出循環
                if (result != default(T))
                    break;

                // 沒有取得資料，紀錄要回寫的快取數目
                GetDataLevel++;
            }

            // 如果有快取沒有值，進行回寫
            if (GetDataLevel != 0 && result != default(T))
                WriteBackData(key, result, default);

            // 當所有的快取都 exception 時，丟出 exception 
            if (exceptions.Count() == cacheProviders.Count())
                throw new OperationFailedException(EnumType.ResultCodeEnum.ConnectCacheFail);

            return result;

        }

        /// <summary>
        /// 取得 string 類型的快取值，如果無法取得就執行 func 並回寫快取
        /// </summary>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <param name="ttlMin"></param>
        /// <returns></returns>
        public string Get(string key, Func<string> func, int ttlMin = 2880)
        {
            string result = Get<string>(key, func, ttlMin);

            return result;
        }

        /// <summary>
        /// 取得泛型類型的快取值，如果無法取得就執行 func 並回寫快取
        /// </summary>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <param name="ttlMin"></param>
        /// <returns></returns>
        public T Get<T>(string key, Func<T> func, int ttlMin = 2880) where T : class
        {
            T result;

            Func<T> invoke = new Func<T>(() =>
            {
                var invokeResult = func.Invoke();

                if (invokeResult != default(T))
                    WriteBackData(key, invokeResult, new TimeSpan(0, ttlMin, 0));

                return invokeResult;

            });


            try
            {
                result = Get<T>(key);

                if (result == default(T))
                    result = invoke();

            }
            catch
            {
                result = invoke();
            }

            return result;
        }

        /// <summary>
        /// 取得泛型類型的 Hash 值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <returns></returns>
        public T HashGet<T>(string key, string hashKey)
        {
            T result = default;

            GetDataLevel = 0;

            List<Exception> exceptions = new List<Exception>();

            foreach (ICacheProvider cache in cacheProviders)
            {
                try
                {
                    result = cache.HashGet<T>(key, hashKey);
                }
                catch (Exception ex)
                {
                    ExceptionLogHandler(ex, cache, "HashGet<T>", $"{{ \"key:\" \"{ key }\", \"hashKey\": \"{ hashKey }\"}}");

                    exceptions.Add(ex);
                }

                if (result != null && !result.Equals(default(T)))
                    break;

                GetDataLevel++;
            }

            if (result != null && !result.Equals(default(T)) && GetDataLevel != 0)
                WriteBackHash(key, hashKey, result, default);

            if (exceptions.Count() == cacheProviders.Count())
                throw new OperationFailedException(EnumType.ResultCodeEnum.ConnectCacheFail);

            return result;
        }

        public T HashGet<T>(string key, string hashKey, Func<T> func)
        {
            T result;

            Func<T> invoke = new Func<T>(() =>
            {
                var invokeResult = func.Invoke();

                if (invokeResult != null && !invokeResult.Equals(default(T)))
                    WriteBackHash(key, hashKey, invokeResult, default);

                return invokeResult;

            });

            try
            {
                result = HashGet<T>(key, hashKey);

                if (result == null || result.Equals(default(T)))
                    result = invoke();
            }
            catch
            {
                result = invoke();
            }


            return result;

        }

        /// <summary>
        /// 只實作 RedisCache 的 HashIncrement，如果 RedisCache 有問題，就執行 func 取得
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public double HashIncrement(string key, string hashKey, double value, Func<double> func)
        {
            RedisCacheProvider cache = cacheProviders
                .FirstOrDefault(p => p.GetType() == typeof(RedisCacheProvider)) as RedisCacheProvider;

            var result = default(double);
            try
            {
                result = cache.HashIncrement(key, hashKey, value, func);
            }
            catch
            {
                throw new OperationFailedException(EnumType.ResultCodeEnum.ConnectCacheFail);
            }

            return result;
        }

        /// <summary>
        /// 非同步寫入所有 Cache，有 Exception 會紀錄，不會報錯
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="hashValue"></param>
        public void HashSet(string key, string hashKey, string hashValue)
        {
            foreach (ICacheProvider cache in cacheProviders)
            {
                cache.HashSetAsync(key, hashKey, hashValue, default);
            }
        }

        /// <summary>
        /// MemoryCache 不支援 Keys 的搜尋功能，所以不實作
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="expectEmpty">預期結果是否為空集合</param>
        /// <returns></returns>
        public string[] Keys(string pattern, bool expectEmpty = false)
        {

            /// MemoryCache 不支援 Keys 的搜尋功能，所以不實作
            var providers = cacheProviders.Where(p => p.GetType() != typeof(MemoryCacheProvider));

            string[] keys = new string[0];

            List<Exception> exceptions = new List<Exception>();

            foreach (ICacheProvider cache in providers)
            {
                try
                {
                    keys = cache.Keys(pattern);
                }
                catch (Exception ex)
                {

                    ExceptionLogHandler(ex, cache, "Keys", $"{{ \"key:\" \"{ pattern }\"}}");

                    exceptions.Add(ex);

                }

                if (keys.ToList().Any())
                    break;
            }

            // 所有的 provider 都 exception
            if(exceptions.Count() == providers.Count())
                throw new OperationFailedException(EnumType.ResultCodeEnum.ConnectCacheFail);

            /**
             * 在有引用多個 CachProvider 的時候，如果同時滿足以下條件時
             *  1. 有一個 CacheProvider 有 exception.
             *  2. 不符合預期結果時.
             * 要 throw exception
             */
            if (keys.ToList().Any() == expectEmpty && exceptions.Any())
                throw new OperationFailedException(EnumType.ResultCodeEnum.ConnectCacheFail);

            return keys;

        }

        /// <summary>
        /// Lock功能，需要跨主機，MemoryCache 因為是單主機存取，所以不進行實作
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool LockRelease(string key, string value)
        {
            var providers = cacheProviders.Where(p => p.GetType() != typeof(MemoryCacheProvider));

            List<Task<bool>> tasks = new List<Task<bool>>();

            foreach (ICacheProvider cache in providers)
            {
                Task<bool> release = Task.Run(() => cache.LockRelease(key, value));
                tasks.Add(release);
            }

            // 只抓第一個返回的
            var result = FirstSuccessTask(tasks).Result;

            // 所有的 task 都是 exception
            if (result.Exception != null)
                throw new OperationFailedException(EnumType.ResultCodeEnum.ConnectCacheFail);

            return result.Result;

        }

        /// <summary>
        /// 取得一個成功的 task
        /// </summary>
        /// <param name="tasks"></param>
        /// <returns></returns>
        async Task<Task<bool>> FirstSuccessTask(List<Task<bool>> tasks)
        {
            for (int i = 0; i < tasks.Count(); i++)
            {
                var task = tasks[i];
                try
                {
                    await task.ConfigureAwait(false);
                    return task;
                }
                catch
                {
                    if (i == tasks.Count() - 1)
                    {
                        return task;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Lock功能，需要跨主機，MemoryCache 因為是單主機存取，所以不進行實作
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public bool LockTake(string key, string value, TimeSpan timeSpan)
        {

            // Lock、Release 功能建立在單一快取，
            // 在 Lock、Release 期間，單一快取不能出現問題 (連線失敗...)
            // 如果出問題會造成Lock、Release失敗

            var providers = cacheProviders.Where(p => p.GetType() != typeof(MemoryCacheProvider));

            bool result = false;

            List<Exception> exceptions = new List<Exception>();

            foreach (var cache in providers)
            {
                try
                {
                    result = cache.LockTake(key, value, timeSpan);

                    // 只要有一個快取成功，就跳出
                    break;
                }
                catch (Exception ex)
                {
                    ExceptionLogHandler(ex, cache, "LockTake", $"{{ \"key:\" \"{ key }\", \"value\": \"{ value }\" }}");

                    exceptions.Add(ex);
                }
            }

            // 所有的 CacheProvider 都是 exception
            if (exceptions.Count() == providers.Count())
                throw new OperationFailedException(EnumType.ResultCodeEnum.ConnectCacheFail);

            return result;
        }

        /// <summary>
        /// 刪除 key
        /// </summary>
        /// <param name="key"></param>
        public void Remove(string key)
        {
            foreach (ICacheProvider cache in cacheProviders)
            {
                cache.RemoveAsync(key);
            }
        }

        /// <summary>
        /// 刪除多組 Keys
        /// </summary>
        /// <param name="keys"></param>
        public void Remove(string[] keys)
        {
            foreach (ICacheProvider cache in cacheProviders)
            {
                cache.RemoveAsync(keys);
            }
        }

        /// <summary>
        /// RemovePattern 是基於 Redis 的實作，所以然要用 Redis 的 Keys 去取有關的 Keys 名稱
        /// </summary>
        /// <param name="pattern"></param>
        public void RemovePattern(string pattern)
        {
            string[] keys = Keys(pattern);

            if (keys.ToList().Any())
                Remove(keys);
        }

    }

}
