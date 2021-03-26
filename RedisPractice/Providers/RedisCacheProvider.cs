using System;
using System.Threading.Tasks;
using StackExchange.Redis;
using Newtonsoft.Json;
using System.Linq;

namespace RedisPractice.Providers
{
    public class RedisCacheProvider : ICacheProvider
    {
        public TimeSpan DefaultExpired => new TimeSpan(0, 2880, 0);

        public TimeSpan NeverExpired => new TimeSpan(99, 0, 0);

        /// <summary>
        /// 快取順序
        /// </summary>
        /// <value></value>
        public int CachePriority { get; set; }

        /// <summary>
        /// Redis server 位址，多主機可用「,」分隔開來
        /// </summary>
        /// <returns></returns>
        private static string GetEndPoint() => "127.0.0.1:6379";

        /// <summary>
        /// Redis server 密碼
        /// </summary>
        /// <returns></returns>
        private static string GetPassword() => "";

        /// <summary>
        /// 在測試時，為了不要混到其它環境的資料，可以調整預設的db
        /// </summary>
        private readonly int defaultDb = 0;

        public static ConnectionMultiplexer Connection => LazyConnection.Value;

        /// <summary>
        /// ConnectionMultiplexer 是 ThreadSafe 的物件
        /// </summary>
        /// <returns></returns>
        public static readonly Lazy<ConnectionMultiplexer> LazyConnection
            = new Lazy<ConnectionMultiplexer>(() =>
            {
                var options = new ConfigurationOptions();

                var ipList = GetEndPoint().Split(',');
                foreach (string ip in ipList)
                {
                    options.EndPoints.Add(ip);
                }

                options.Password = GetPassword();
                options.AbortOnConnectFail = false;

                options.ConnectRetry = 3;
                options.KeepAlive = 30;
                options.ConnectTimeout = 500;

                return ConnectionMultiplexer.Connect(options);

            });

        private void ExceptionLogHandler(Exception ex, string method, dynamic dataObj)
        {
            // 取得 data 字串
            string data = JsonConvert.SerializeObject(dataObj);

            // 非同步的時候，取得內部的 exception
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            //Dictionary<string, string> details = new Dictionary<string, string>
            //{
            //    { "CacheProvider", GetType().Name },
            //    { "Method", method },
            //    { "Data", data },
            //    { "exception", ex.Message }
            //};

            // 在測試時，有發生 Stackexchange.Redis 無法讀取 redise server 回應
            // 的情況，(目前無法重現)，所以在這裡判斷，如果是 RedisTimeoutExeption
            // 的話，就試著重啟。
            // exception 訊息如下:
            // ------
            // inst: 1, 
            // mgr: ExecuteSelect, err: never, 
            // queue: 932, qu: 0, qs: 932, qc: 0, wr: 0, wq: 0, in: 45007, ar: 0, 
            // clientName: PAYAPP01T, 
            // serverEndpoint: 192.168.98.17:6379, 
            // keyHashSlot: 11450, 
            // IOCP: (Busy=0,Free=1000,Min=16,Max=1000), 
            // WORKER: (Busy=8,Free=8183,Min=16,Max=8191) 
            // (http://stackexchange.github.io/StackExchange.Redis/Timeouts)
            // -----
            // queue 在等待中的指令數很高
            // qu (Queue-Awaiting-Write) 在等待寫入 Redis 的指令數 0
            // qs (Queue-Awaiting-Response) 等待從 Redis 回應的指令數
            // in (Inbound-Bytes) 等待讀取的 Bytes 大小
            // #######
            // 依照上面的訊息判斷，IOCP、Worker不是特別的忙，沒有等待寫入 Redis
            // 的指令數，另外用 redis-cli 登入 Redis sever 可以正常執行，代表
            // Redis server 正常，qs、in 過高，代表 Redis server 有回應，可是
            // stackexchange.redis 物件在讀取的地方卡住了。
            // 目前想到的解法，重建 ConnectionMultiplexer 
            // 此方法尚未驗證，日後有發生的時候，要注意驗證
            if (ex is RedisTimeoutException)
            {
                LazyConnection.Value.Close();
                LazyConnection.Value.Dispose();
            }

        }

        public RedisCacheProvider(int priority = 20)
        {
            CachePriority = priority;
        }

        public T Get<T>(string key)
        {
            var cache = Connection.GetDatabase(defaultDb);

            RedisValue redisValue;

            try
            {
                redisValue = cache.StringGet(key);
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "Get<T>", new { key });
                throw;
            }

            T result = default;

            if (!redisValue.HasValue)
                return result;

            if (typeof(T) == typeof(string))
            {
                result = (T)Convert.ChangeType(redisValue, typeof(T));
            }
            else
            {
                result = JsonConvert.DeserializeObject<T>(redisValue);
            }

            return result;

        }

        public T HashGet<T>(string key, string hashKey)
        {
            var cache = Connection.GetDatabase(defaultDb);

            RedisValue value;

            try
            {
                value = cache.HashGet(key, hashKey);
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "HashGet<T>", new { key, hashKey });
                throw;
            }

            T result = default;

            if (!value.HasValue)
                return result;

            if (typeof(T) == typeof(string))
            {
                result = (T)Convert.ChangeType(value, typeof(T));
            }
            else
            {
                result = JsonConvert.DeserializeObject<T>(value);
            }

            return result;

        }

        public double HashIncrement(string key, string hashKey, double value = 1)
        {

            var cache = Connection.GetDatabase(defaultDb);

            double result = default;

            //** TODO: 如果 cache 裡沒有這個 key ，要怎麼處理？
            try
            {
                if (cache.HashExists(key, hashKey))
                    result = cache.HashIncrement(key, hashKey, value);
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "HashIncrement", new { key, hashKey, value });
            }

            return result;

        }

        public Task<bool> HashSetAsync<T>(string key, string hashKey, T value, TimeSpan expired = default)
        {
            var cache = Connection.GetDatabase(defaultDb);

            if (expired == default)
                expired = DefaultExpired;

            string valueString;

            if (typeof(T) == typeof(string))
            {
                valueString = value as string;
            }
            else
            {
                valueString = JsonConvert.SerializeObject(value);
            }

            var result = cache.HashSetAsync(key, hashKey, valueString)
                .ContinueWith(t =>
                {
                    bool innerResult= false;
                    if (t.Exception == null)
                    {
                        var expireResult = cache.Execute("expire", key, expired.TotalSeconds);

                        int.TryParse((string)expireResult, out int affectedRows);
                        innerResult = affectedRows == 1;
                    }
                    else
                    {
                        ExceptionLogHandler(t.Exception, "HashSetAsync", new { key, hashKey, value });
                    }

                    return innerResult;

                });

            return result;
        }

        public bool HashSet<T>(string key, string hashKey, T value, TimeSpan expired = default)
        {
            var cache = Connection.GetDatabase(defaultDb);

            bool result;

            if (expired == default)
                expired = DefaultExpired;

            string valueString;

            if (typeof(T) == typeof(string))
            {
                valueString = value as string;
            }
            else
            {
                valueString = JsonConvert.SerializeObject(value);
            }

            try
            {
                result = cache.HashSet(key, hashKey, valueString);

                if (expired != NeverExpired)
                {
                    cache.Execute("expire", key, expired.TotalSeconds);
                }
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "HashSet<T>", new { key, hashKey, value });
                throw;
            }

            return result;
        }

        public string[] Keys(string pattern)
        {
            // 因為 keys * 查詢耗費大量資源，所以沒有傳入參數的話，就直接回傳空字串
            if (string.IsNullOrEmpty(pattern))
                return default;

            var cache = Connection.GetDatabase(defaultDb);

            string[] innerResult;

            try
            {
                var result = cache.Execute("keys", pattern);
                innerResult = (string[])result;
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "Keys", new { pattern });
                throw;
            }

            return innerResult;

        }

        public bool LockRelease(string key, string token)
        {
            var cache = Connection.GetDatabase(defaultDb);

            bool result;

            try
            {
                result = cache.LockRelease(key, token);
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "LockRelease", new { key, token });
                throw;
            }

            return result;

        }

        public bool LockTake(string key, string token, TimeSpan lockTime)
        {
            var cache = Connection.GetDatabase(defaultDb);
            bool result;

            try
            {
                result = cache.LockTake(key, token, lockTime);
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "LockTake", new { key, token, lockTime });
                throw;
            }

            return result;
        }

        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                return true;

            var cache = Connection.GetDatabase(defaultDb);

            bool result;

            try
            {
                result = cache.KeyDelete(key);
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "Remove", new { key });
                throw;
            }

            return result;
        }

        public Task<bool> RemoveAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
                return Task.Run(() => true);

            var cache = Connection.GetDatabase(defaultDb);

            var task = cache.KeyDeleteAsync(key)
                .ContinueWith(t =>
                {
                    ExceptionLogHandler(t.Exception, "RemoveAsync", new { key });
                    return false;
                }, TaskContinuationOptions.OnlyOnFaulted);


            return task;
        }
        public bool Remove(string[] keys)
        {
            if (!keys.ToList().Any())
                return true;

            var cache = Connection.GetDatabase(defaultDb);

            bool result;

            try
            {
                var redisResult = cache.Execute("del", keys);
                int.TryParse((string)redisResult, out int affectedRows);

                // 不確定是不是每一個key 都偭存在，所以只要有一筆成功，就視為全部成功
                result = affectedRows > 0;
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "Remove", new { keys });
                throw;
            }

            return result;
        }

        public Task<bool> RemoveAsync(string[] keys)
        {
            if (!keys.ToList().Any())
                return Task.Run(() => true);

            var cache = Connection.GetDatabase(defaultDb);

            var result = cache.ExecuteAsync("del", keys)
                .ContinueWith(t =>
                {
                    bool innerResult = false;

                    if (t.Exception == null)
                    {
                        int.TryParse((string)t.Result, out int affectedRows);

                        // 不確定是不是每一個key 都偭存在，所以只要有一筆成功，就視為全部成功
                        innerResult = affectedRows > 0;
                    }
                    else
                    {
                        ExceptionLogHandler(t.Exception, "RemoveAsync", new { keys });
                    }

                    return innerResult;
                });

            return result;
        }

        public bool Set<T>(string key, T value, TimeSpan expired = default)
        {
            if (expired == default)
                expired = DefaultExpired;

            var cache = Connection.GetDatabase(defaultDb);

            string valueString;

            if (typeof(T) == typeof(string))
            {
                valueString = value as string;
            }
            else
            {
                valueString = JsonConvert.SerializeObject(value);
            }

            bool result;

            try
            {
                if (expired == NeverExpired)
                {
                    result = cache.StringSet(key, valueString);
                }
                else
                {
                    result = cache.StringSet(key, valueString, expired);
                }
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "Set<T>", new { key, value, expired });
                throw;
            }

            return result;
        }

        public Task<bool> SetAsync<T>(string key, T value, TimeSpan expired = default)
        {
            if (expired == default(TimeSpan))
                expired = DefaultExpired;

            var cache = Connection.GetDatabase(defaultDb);

            string valueString;

            if (typeof(T) == typeof(string))
            {
                valueString = value as string;
            }
            else
            {
                valueString = JsonConvert.SerializeObject(value);
            }

            Task<bool> result;
            if (expired == NeverExpired)
            {
                result = cache.StringSetAsync(key, valueString);
            }
            else
            {
                result = cache.StringSetAsync(key, valueString, expired);
            }

            result.ContinueWith(t => ExceptionLogHandler(t.Exception, "SetAsync", new { key, value, expired })
                    , TaskContinuationOptions.OnlyOnFaulted);

            return result;
        }

    }
}
