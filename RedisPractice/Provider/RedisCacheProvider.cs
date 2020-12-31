using Newtonsoft.Json;
using RedisPractice.Exceptions;
using RedisPractice.Provider.Interface;
using RedisPractice.Utility;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RedisPractice.Provider
{
    public class RedisCacheProvider : ICacheProvider
    {
        public TimeSpan DefaultExpired => new TimeSpan(0, 2280, 0);

        public TimeSpan NeverExpired => new TimeSpan(99, 0, 0);

        private readonly ILogger logger;

        /// <summary>
        /// 快取順序
        /// </summary>
        public int CachePriority { get; set; }

        public static ConnectionMultiplexer Connection => lazyConnection.Value;

        public RedisCacheProvider(int priority = 20)
        {
            CachePriority = priority;
            logger = new TextLogger();
        }

        private string ConnectedServerRole(IDatabase db)
        {
            RedisResult redisResult = db.Execute("ROLE");

            RedisResult[] resultArr = (RedisResult[])redisResult;

            return resultArr[0].ToString();

        }

        private void WriteLog(IDatabase db, string msg)
        {
            string role = ConnectedServerRole(db);

            string log = $"{role} - {msg}";

            logger.WriteNomalLog(log);

        }


        /// <summary>
        /// 建立 Redis 連線，使用 Lazy 在多執行緒間共享，提供 ThreadSafe 的 initialize
        /// </summary>
        private static readonly Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
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

        /// <summary>
        /// 取得 Redis 連線伺服器位址
        /// </summary>
        /// <returns></returns>
        private static string GetEndPoint()
        {
            string[] servers = new string[]
            {
                "localhost:6379"
            };

            return string.Join(",", servers);
        }

        /// <summary>
        /// 取得 Redis 連線密碼
        /// </summary>
        /// <returns></returns>
        private static string GetPassword() => "";


        /// <summary>
        /// 紀錄 Exception
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="cache"></param>
        /// <param name="method"></param>
        /// <param name="data"></param>
        private void ExceptionLogHandler(Exception ex, string method, string data)
        {

            Dictionary<string, string> details = new Dictionary<string, string>
                    {
                        { "CacheProvider", GetType().Name },
                        { "Method", method },
                        { "Data", data },
                        { "exception", ex.Message }
                    };

            logger.WriteErrorLog(new OperationFailedException(EnumType.ResultCodeEnum.ConnectCacheFail), details);
        }

        /// <summary>
        /// 取得快取裡的值，並轉成指定的類型，如果沒有值就回傳 null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Get<T>(string key) where T : class
        {
            var cache = Connection.GetDatabase();

            RedisValue redisValue = cache.StringGet(key);

            T result = default;

            WriteLog(cache, $"Get {key}");

            if (redisValue.HasValue)
            {
                if (typeof(T) != typeof(string))
                {
                    result = JsonConvert.DeserializeObject<T>(redisValue);
                }
                else
                {
                    result = redisValue.ToString() as T;
                }
            }

            return result;
        }


        /// <summary>
        /// 取得快取裡的值，如果沒有值，就執行 func ，並回寫快取
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public T Get<T>(string key, Func<T> func) where T : class
        {
            T result = default;

            try
            {
                result = Get<T>(key);
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "T Get<t>", $"{{ \"key\": \"{ key }\" }}");
            }

            if (result == default(T))
            {
                result = func.Invoke();

                if (result != default(T))
                {
                    // 回寫 value
                    try
                    {
                        Set(key, result);
                    }
                    catch (Exception ex)
                    {
                        ExceptionLogHandler(ex, "T Get<t>", $"{{ \"key\": \"{ key }\" }}");
                    }
                }

            }

            return result;
        }


        /// <summary>
        /// 取得Hash值(其中一組 KeyValuePair 的 value)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        public T HashGet<T>(string key, string hashKey)
        {
            var cache = Connection.GetDatabase();

            string value = cache.HashGet(key, hashKey);

            if (string.IsNullOrEmpty(value))
                return default;

            T result;

            if (typeof(T) != typeof(string))
            {
                result = JsonConvert.DeserializeObject<T>(value);
            }
            else
            {
                result = (T)Convert.ChangeType(value, typeof(T));
            }

            return result;

        }

        /// <summary>
        /// 將 HashKey 值加 value 後，取出 (代表目前的值)
        /// 如果沒有這個 key-hashKey, 將會從 1 開始
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public double HashIncrement(string key, string hashKey, double value)
        {
            var cache = Connection.GetDatabase();

            var result = cache.HashIncrement(key, hashKey, value);

            return result;
        }

        /// <summary>
        /// 將 HashKey 值加 value 後，取出 (代表目前的值)，如果沒有辦法取得，就執行 func 來取得
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public double HashIncrement(string key, string hashKey, double value, Func<double> func)
        {
            var cache = Connection.GetDatabase();

            double result;

            try
            {
                if (cache.HashExists(key, hashKey))
                {
                    result = cache.HashIncrement(key, hashKey, value);
                }
                else
                {
                    result = func.Invoke();
                    cache.HashSetAsync(key, hashKey, result);
                }
            }
            catch (Exception ex)
            {

                ExceptionLogHandler(ex, "HashIncrement", $"{{ \"key:\" \"{ key }\", \"hashKey\": \"{ hashKey }\", \"value\": \"{ value }\"}}");

                result = func.Invoke();
                cache.HashSetAsync(key, hashKey, result);

            }

            return result;
        }

        /// <summary>
        /// 寫入Hash (KeyValuePair的集合)
        /// 過期是整個集合都會清除，無法對單一 KeyValuePair 過期
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        public void HashSet<T>(string key, string hashKey, T value, TimeSpan expired = default(TimeSpan))
        {
            var cache = Connection.GetDatabase();

            if (expired == default)
            {
                expired = DefaultExpired;
            }

            string valueString;

            if (typeof(T) == typeof(string))
            {
                valueString = value as string;
            }
            else
            {
                valueString = JsonConvert.SerializeObject(value, Formatting.None);
            }

            cache.HashSet(key, hashKey, valueString);

            if (expired != NeverExpired)
            {
                cache.Execute("expire", key, expired.TotalSeconds);
            }
        }


        /// <summary>
        /// 寫入Hash (KeyValuePair的集合)
        /// 過期是整個集合都會清除，無法對單一 KeyValuePair 過期
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        public Task HashSetAsync<T>(string key, string hashKey, T value, TimeSpan expired)
        {
            // 因為還要設定 expired，所以沒有辦法直接用 Redis.HashSetAsync
            var task = Task.Run(() => HashSet(key, hashKey, value, expired));

            // exception 
            task.ContinueWith(t =>
            {
                ExceptionLogHandler(t.Exception, "HashSetAsync", $"{{ \"key:\" \"{ key }\", \"hashKey\": \"{ hashKey }\", \"value\": \"{ value }\"}}");

            }, TaskContinuationOptions.OnlyOnFaulted);

            return task;
        }

        /// <summary>
        /// 依照傳入的 pattern 取得相關的 key
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        public string[] Keys(string pattern)
        {

            if (string.IsNullOrEmpty(pattern))
                pattern = "*";

            var cache = Connection.GetDatabase();
            var result = cache.Execute($"keys", pattern);

            var innerResult = (string[])result;

            return innerResult;
        }



        /// <summary>
        /// 選用一個 key，設定其值為 token，在指定時間 lockTime 內，視為鎖定
        /// 如果在 lockTime 內解鎖，需要使用 LockRelease
        /// true => delete key
        /// false => key 不存在，或是 token 不合
        /// ** 快取本身並沒有鎖定這個 key，只是表示一個狀態，這時如果使用 Set 
        ///    來重設這個 key，會導致鎖定失敗，所以在選定 key 時，要注意是獨特
        ///    的值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="token"></param>
        /// <param name="lockTime"></param>
        /// <returns></returns>
        public bool LockRelease(string key, string token)
        {
            var cache = Connection.GetDatabase();
            return cache.LockRelease(key, token);
        }

        /// <summary>
        /// 選用一個 key，設定其值為 token，在指定時間 lockTime 內，視為鎖定
        /// 如果在 lockTime 內解鎖，需要使用 LockRelease
        /// ** 快取本身並沒有鎖定這個 key，只是表示一個狀態，這時如果使用 Set 
        ///    來重設這個 key，會導致鎖定失敗，所以在選定 key 時，要注意是獨特
        ///    的值
        /// ** key、token 相同時不會重置 lockTime
        /// </summary>
        /// <param name="key"></param>
        /// <param name="token"></param>
        /// <param name="lockTime"></param>
        /// <returns></returns>
        public bool LockTake(string key, string token, TimeSpan lockTime)
        {
            var cache = Connection.GetDatabase();
            return cache.LockTake(key, token, lockTime);
        }

        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
                return true;

            var cache = Connection.GetDatabase();
            bool result = cache.KeyDelete(key);

            WriteLog(cache, $"Remove {key}");

            return result;
        }

        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Task<bool> RemoveAsync(string key)
        {

            if (string.IsNullOrEmpty(key))
                return Task.Run(() => true);

            var cache = Connection.GetDatabase();

            var task = cache.KeyDeleteAsync(key);

            // exception 
            task.ContinueWith(t =>
            {
                ExceptionLogHandler(t.Exception, "RemoveAsnyc<T>", $"{{ \"key:\" \"{ key }\" }}");

            }, TaskContinuationOptions.OnlyOnFaulted);

            return task;
        }


        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public bool Remove(string[] keys)
        {

            if (!keys.ToList().Any())
                return true;

            var cache = Connection.GetDatabase();

            RedisResult redisResult = cache.Execute("del", keys);

            bool result = !redisResult.Equals(0);

            return result;

        }

        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public Task<bool> RemoveAsync(string[] keys)
        {

            if (!keys.ToList().Any())
                return Task.Run(() => true);

            var task = Task.Run(() => Remove(keys));

            // exception 
            task.ContinueWith(t =>
            {
                ExceptionLogHandler(t.Exception, "RemoveAsync", $"{{ \"key:\" \"{ keys }\" }}");

            }, TaskContinuationOptions.OnlyOnFaulted);

            return task;

        }

        /// <summary>
        /// 設定快取值，TimeSpan 不指定時取預設值 (DefaultExpired => 預設值，NeverExpired => 永不過期)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        /// <returns></returns>
        public bool Set<T>(string key, T value, TimeSpan expired = default)
        {
            if (expired == default)
                expired = DefaultExpired;

            var cache = Connection.GetDatabase();

            string valueString;

            if (typeof(T) == typeof(string))
            {
                valueString = value as string;
            }
            else
            {
                valueString = JsonConvert.SerializeObject(value, Formatting.None);
            }

            bool result;

            if (expired == NeverExpired)
            {
                result = cache.StringSet(key, valueString);
            }
            else
            {
                result = cache.StringSet(key, valueString, expired);
            }

            WriteLog(cache, $"set {key}");

            return result;
        }

        /// <summary>
        /// 設定快取值，TimeSpan 不指定時取預設值 (DefaultExpired => 預設值，NeverExpired => 永不過期)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        /// <returns></returns>
        public Task<bool> SetAsync<T>(string key, T value, TimeSpan expired)
        {
            if (expired == default)
                expired = DefaultExpired;

            var cache = Connection.GetDatabase();

            string valueString;

            // TODO: Set, Get 非對稱造成 string 不需要 SerializeObject 
            // (已經在外面做了，之後如果有修改的話要拿掉 string 的判斷
            if (typeof(T) == typeof(string))
            {
                valueString = value as string;
            }
            else
            {
                valueString = JsonConvert.SerializeObject(value, Formatting.None);
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

            // exception
            result.ContinueWith(t =>
            {
                ExceptionLogHandler(t.Exception, "SetAsync<T>", $"{{ \"key:\" \"{ key }\", \"value\": \"{ value }\"}}");

            }, TaskContinuationOptions.OnlyOnFaulted);

            return result;
        }

    }
}
