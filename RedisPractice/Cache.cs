using System;
using System.Collections.Generic;
using System.Linq;
using RedisPractice.Providers;

namespace RedisPractice
{
    public class Cache : ICache
    {
        public TimeSpan DefaultExpired => default(TimeSpan);

        public TimeSpan NeverExpired => new TimeSpan(99, 0, 0);

        /// <summary>
        /// 要使用的CacheProvider集合
        /// </summary>
        private ICacheProvider[] cacheProviders;

        /// <summary>
        /// 快取的狀態
        /// 預設為 false (狀態正常)
        /// 遇到要回寫時先判斷狀態，再進行回寫，避免重複 exception
        /// </summary>
        private bool[] providerException;

        /// <summary>
        /// 根據使用的 CacheProvider，建立對應數量的 providerStatus
        /// </summary>
        private void InitProviderStatus()
        {
            providerException = new bool[cacheProviders.Length];
        }

        public Cache(ICacheProvider[] cacheProvidersParam)
        {
            List<ICacheProvider> providerList = cacheProvidersParam.ToList();

            cacheProviders = providerList
                .OrderBy(t => t.CachePriority)
                .ToArray();

            InitProviderStatus();
        }

        public void Add(string key, string value, TimeSpan expired = default)
        {
            throw new NotImplementedException();
        }

        public string Get(string key, TimeSpan expired = default)
        {
            throw new NotImplementedException();
        }

        public string Get(string key, Func<string> func, int ttlMin = 2880)
        {
            throw new NotImplementedException();
        }

        public T Get<T>(string key, Func<T> func, int ttlMin = 2880)
        {
            throw new NotImplementedException();
        }

        public T Get<T>(string key, TimeSpan expired = default)
        {
            throw new NotImplementedException();
        }

        public T HashGet<T>(string key, string hashKey, TimeSpan expired = default)
        {
            throw new NotImplementedException();
        }

        public T HashGet<T>(string key, string hashKey, func<T> func, TimeSpan expired = default)
        {
            throw new NotImplementedException();
        }

        public double HashIncrement(string key, string hashKey, double value, Func<double> func)
        {
            throw new NotImplementedException();
        }

        public bool HashSet(string key, string hashKey, string hashValue, TimeSpan expired = default)
        {
            throw new NotImplementedException();
        }

        public string[] Keys(string pattern, bool expectEmpty = false)
        {
            throw new NotImplementedException();
        }

        public bool LockRelease(string key, string token)
        {
            throw new NotImplementedException();
        }

        public bool LockTake(string key, string token, TimeSpan timeSpan)
        {
            throw new NotImplementedException();
        }

        public bool Remove(string key)
        {
            throw new NotImplementedException();
        }

        public bool Remove(string[] keys)
        {
            throw new NotImplementedException();
        }

        public bool RemovePattern(string pattern)
        {
            throw new NotImplementedException();
        }
    }
}
