using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
            List<Task> taskList = new();

            // 寫入通常為第一次執行 cacheProvider 操作，
            // providerException 都為為 false，所以暫時不用判斷狀態
            // 因為是 Async，無法紀綠 providerException
            foreach (ICacheProvider cache in cacheProviders)
            {
                // 依照不同的 cacheProvider 計算逾期時間
                TimeSpan cacheExpired = GetExpired(cache, expired);

                taskList.Add(cache.SetAsync(key, value, cacheExpired));
            }

            var firstTask = WhenAnyCondition(taskList, t => t.Status == TaskStatus.RanToCompletion).GetAwaiter().GetResult();

            if (firstTask == null)
                throw new Exception("Cache Failed");


        }

        /// <summary>
        /// 根據傳入的 expired 回傳各個 cacheProvider 的預設逾時時間
        /// </summary>
        private TimeSpan GetExpired(ICacheProvider cacheProvider, TimeSpan inputExpired)
        {
            if (inputExpired == DefaultExpired)
                return cacheProvider.DefaultExpired;

            if (inputExpired == NeverExpired)
                return cacheProvider.NeverExpired;

            return inputExpired;

        }

        /// <summary>
        /// 快取失敗時，回寫資料
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="getDataLevel">成功取得資料的快取層級</param>
        /// <param name="expired"></param>
        private async Task WriteBackData<T>(string key, T value, int getDataLevel, TimeSpan expired = default)
        {

            List<Task> taskList = new List<Task>();

            for (int i = 0; i < getDataLevel; i++)
            {
                // 根據 providerStatus 狀態，寫入 cache
                if (!providerException[i])
                {
                    TimeSpan cacheExpired = GetExpired(cacheProviders[i], expired);
                    taskList.Add(cacheProviders[i].SetAsync(key, value, cacheExpired));
                }
            }

            // 如果所有的 providerStatus 都無法使用的狀態下，直接回傳
            if (taskList.Count == 0)
                return;

            var firstTask = await WhenAnyCondition(taskList, t => t.Status == TaskStatus.RanToCompletion);

            if (firstTask == null)
                throw new Exception("Cache Failed");

        }

        /// <summary>
        /// 快取失敗時，回寫資料 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <param name="getDataLevel"></param>
        /// <param name="expired"></param>
        private async Task WriteBackHash<T>(string key, string hashKey, T value, int getDataLevel, TimeSpan expired = default(TimeSpan))
        {
            List<Task> taskList = new List<Task>();

            for (int i = 0; i < getDataLevel; i++)
            {
                // 根據 providerStatus 狀態，寫入 cache
                if (!providerException[i])
                {
                    TimeSpan cacheExpired = GetExpired(cacheProviders[i], expired);
                    taskList.Add(cacheProviders[i].HashSetAsync(key, hashKey, value, expired));
                }
            }

            // 如果所有的 providerStatus 都無法使用的狀態下，直接回傳
            if (taskList.Count == 0)
                return;

            // Debug.WriteLine("WriteBackHash WhenAnyCondition Start");
            var firstTask = await WhenAnyCondition(taskList, t => t.Status == TaskStatus.RanToCompletion);
            // Debug.WriteLine("WriteBackHash WhenAnyCondition End");

            if (firstTask == null)
                throw new Exception("Cache Failed");

        }


        /// <summary>
        /// 紀錄 Exception
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="cache"></param>
        /// <param name="method"></param>
        /// <param name="data"></param>
        private void ExceptionLogHandler(Exception ex, ICacheProvider cache, string method, dynamic dataObj)
        {

            // 取得 data 字串
            string data = JsonConvert.SerializeObject(dataObj);

            // 非同步的時候，取得內部的 exception
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            Dictionary<string, string> details = new Dictionary<string, string>
                    {
                        { "CacheProvider", cache.GetType().Name },
                        { "Method", method },
                        { "Data", data },
                        { "exception", ex.Message }
                    };


        }



        public string Get(string key, TimeSpan expired = default)
        {
            string result = Get<string>(key);
            return result;
        }

        public string Get(string key, Func<string> func, int ttlMin = 2880)
        {
            string result = Get<string>(key ,func, ttlMin);
            return result;
        }

        public T Get<T>(string key, Func<T> func, int ttlMin = 2880)
        {
            T result;

            // 如果已經 invoke 過，還是撈不到值的話，就不用重覆再 invoke 了
            bool invoked = false;

            TimeSpan expired = new TimeSpan(0, ttlMin, 0);

            Func<T> invoke = new Func<T>(() =>
            {
                if (invoked)
                    return default;

                var invokeResult = func.Invoke();
                invoked = true;

                if (EqualityComparer<T>.Default.Equals(invokeResult, default))
                    WriteBackData(key, invokeResult, cacheProviders.Length, expired).ConfigureAwait(false);

                return invokeResult;

            });


            try
            {
                result = Get<T>(key, expired);
            }
            catch
            {
                result = invoke();
            }

            if (EqualityComparer<T>.Default.Equals(result, default))
                result = invoke();

            return result;

        }

        public T Get<T>(string key, TimeSpan expired = default)
        {

            // CacheProvider 的數量
            int providerCounts = cacheProviders.Length;

            InitProviderStatus();

            // 重設取得資料的階層 
            // (預設是 CacheProvider 的長度，如果都沒有讀取到資料的話，才會回寫)
            int getDataLevel = providerCounts;

            T result = default;

            // 紀錄是不是有從 cache 中取回值
            bool hasValue = false;

            for (int i = 0; i < providerCounts; i++)
            {
                ICacheProvider cache = cacheProviders[i];

                try
                {
                    result = cache.Get<T>(key);
                }
                catch (Exception ex)
                {

                    ExceptionLogHandler(ex, cache, "T Get<T>", new { key, expired });

                    // 變更 cacheProvider 狀態
                    providerException[i] = true;

                }

                // 比較取得的值是不是 default (包含 null)
                if (!EqualityComparer<T>.Default.Equals(result, default))
                {
                    // 紀錄取得快取的層級
                    getDataLevel = i;
                    hasValue = true;
                    break;
                }

            }

            // 最後一個 CacheProvider exception 
            // 會跑到最後一個，代表前面的 CacheProvider 都沒有值或是 exception，
            // 最後一個也 exception，就直接丟出 exception
            if (providerException.Last())
                throw new Exception("Cache failed");

            // 低階快取沒有值，進行回寫
            if (getDataLevel != 0 && hasValue)
                WriteBackData(key, result, getDataLevel, expired).ConfigureAwait(false);

            return result;

        }

        public T HashGet<T>(string key, string hashKey, TimeSpan expired = default)
        {
            T result = default;

            int providerCounts = cacheProviders.Length;

            InitProviderStatus();

            // 預設為 cacheProviders 的長度
            // 在所有 cacheProviders 都讀取失敗後，才有辦法回寫
            int getDataLevel = providerCounts;

            for (int i = 0; i < providerCounts; i++)
            {
                ICacheProvider cache = cacheProviders[i];

                try
                {
                    result = cache.HashGet<T>(key, hashKey);
                }
                catch (Exception ex)
                {

                    ExceptionLogHandler(ex, cache, "HashGet<T>", new { key, hashKey, expired});

                    // 變更 cacheProvider 狀態
                    providerException[i] = true;

                }

                if (EqualityComparer<T>.Default.Equals(result, default))
                {
                    // 紀錄取得快取的層級
                    getDataLevel = i;
                    break;
                }
            }

            // 如果後一層的快取也 exception 直接丟出
            if (providerException.Last())
                throw new Exception("Cache failed");


            // 如果 default(T) 是 null 的話，不能使用 Equals 比較
            if (!EqualityComparer<T>.Default.Equals(result, default) && getDataLevel != 0)
                WriteBackHash(key, hashKey, result, getDataLevel, expired).ConfigureAwait(false);

            return result;

        }

        public T HashGet<T>(string key, string hashKey, Func<T> func, TimeSpan expired = default)
        {

            T result;

            bool invoked = false;

            Func<T> invoke = new Func<T>(() =>
            {
                if (invoked)
                    return default;

                T invokeResult = func.Invoke();

                if (!EqualityComparer<T>.Default.Equals(invokeResult, default))
                    WriteBackHash(key, hashKey, invokeResult, cacheProviders.Length, default).ConfigureAwait(false);

                return invokeResult;

            });

            try
            {
                result = HashGet<T>(key, hashKey);
            }
            catch
            {
                result = invoke();
            }

            if (result == null || result.Equals(default(T)))
                result = invoke();

            return result;


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

        /// <summary>
        /// 使用非同步方法執行Task時，取得第一個符合 condition 的結果
        /// ----
        /// 在 unit test 時，因為在間隔很短的時間內，使用 RemoveAsync, SetAsync,
        /// 造成先 Remove 再 Set，導致原本應該要刪掉的 key 沒有刪掉，所以測試失敗。
        /// 目前想到的解法:
        /// 1. 先進先出，搞清楚怎麼控制順序 => 暫時不知道怎麼實現
        /// 2. 在執行非同步的Task時，不要丟了就跑，要等到第一個執行完成的Task回來，再繼續往下 => 這個 Method
        /// ---
        /// 如果其它地方也有這樣的需求的話，這個 Method 可以考慮抽出共用
        /// </summary>
        /// <param name="tasks"></param>
        /// <param name="condition"></param>
        /// <returns></returns>
        private static async Task<Task> WhenAnyCondition(IEnumerable<Task> tasks, Predicate<Task> condition)
        {
            var tasklist = tasks.ToList();

            while (tasklist.Any())
            {
                var task = await Task.WhenAny(tasklist).ConfigureAwait(false);
                tasklist.Remove(task);

                if (condition(task))
                    return task;
            }

            return null;
        }

    }

}
}
