using RedisPractice.Exceptions;
using RedisPractice.Provider.Interface;
using RedisPractice.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace RedisPractice.Provider
{
    public class MemoryCacheProvider : ICacheProvider
    {

        private readonly ILogger logger;

        /// <summary>
        /// 預設的 TimeSpan
        /// </summary>
        public TimeSpan DefaultExpired
        {
            get
            {
                //string secondFromConfig = System.Configuration.ConfigurationManager.AppSettings["MemeryCacheTime"];
                string secondFromConfig = "30";

                if (!int.TryParse(secondFromConfig, out int second))
                {
                    second = 7;
                };

                return new TimeSpan(0, 0, second);
            }
        }


        /// <summary>
        /// 永不過期的 TimeSpan ( 不自動過期，直到 cache 清除)
        /// </summary>
        public TimeSpan NeverExpired
        {
            get
            {
                return new TimeSpan(99, 0, 0);
            }
        }


        /// <summary>
        /// 快取順序
        /// </summary>
        public int CachePriority { get; set; }


        private readonly ObjectCache _cache;

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

        public MemoryCacheProvider(int priority = 10)
        {
            CachePriority = priority;
            _cache = MemoryCache.Default;
        }

        /// <summary>
        /// 取得快取裡的值，並轉成指定的類型，如果沒有值就回傳 null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public T Get<T>(string key) where T : class
        {
            if (!_cache.Contains(key))
                return default;

            var value = _cache.Get(key);

            return value as T;
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
                ExceptionLogHandler(ex, "T Get<T>", $"{{ \"key\": \"{key}\" }}");
            }

            if (result == default(T))
            {
                var value = func.Invoke();

                if (value != default(T))
                {
                    try
                    {
                        Set(key, value);
                    }
                    catch (Exception ex)
                    {
                        ExceptionLogHandler(ex, "T Get<T>", $"{{ \"key\": \"{key}\" }}");
                    }
                }

                result = value;

            }

            return result;
        }

        public T HashGet<T>(string key, string hashKey)
        {

            var result = new List<KeyValuePair<string, T>>();

            result = Get<List<KeyValuePair<string, T>>>(key);

            if (result == null)
                return default;

            var hashValue = result.FirstOrDefault(t => t.Key == hashKey);

            if (hashKey.Equals(default(KeyValuePair<string, T>)))
                return default;

            return hashValue.Value;

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
            var existValue = HashGet<string>(key, hashKey);

            double result;

            if (existValue == null)
            {
                result = value;
            }
            else
            {
                result = double.Parse(existValue);
                result += value;
            }

            HashSet(key, hashKey, result.ToString());

            return result;


        }

        /// <summary>
        /// 將 HashKey 值加 value 後，取出 (代表目前的值)，如果沒有辦法取得，就執行 func 來取得
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <paam name="value"></param>
        /// <returns></returns>
        public double HashIncrement(string key, string hashKey, double value, Func<double> func)
        {
            string existValue = null;


            try
            {
                existValue = HashGet<string>(key, hashKey);
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "HashIncrement", $"{{ \"key:\" \"{ key }\", \"hashKey\": \"{ hashKey }\", \"value\": \"{ value }\"}}");
            }

            double result;

            if (existValue == null)
            {
                result = func.Invoke();
            }
            else
            {
                result = double.Parse(existValue);
                result += value;
            }

            try
            {
                HashSet(key, hashKey, result);
            }
            catch (Exception ex)
            {
                ExceptionLogHandler(ex, "HashIncrement", $"{{ \"key:\" \"{ key }\", \"hashKey\": \"{ hashKey }\", \"value\": \"{ value }\"}}");
            }

            return result;
        }

        public void HashSet<T>(string key, string hashKey, T value, TimeSpan expired = default)
        {

            if (!value.GetType().IsClass)
            {

                List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();

                KeyValuePair<string, string> pair = new KeyValuePair<string, string>(hashKey, value.ToString());

                // 已經有這個 key
                if (_cache.Contains(key))
                {
                    list = Get<List<KeyValuePair<string, string>>>(key);

                    var originalPair = list.FirstOrDefault(t => t.Key == hashKey);

                    // 已經有這個 hashKey
                    if (!originalPair.Equals(default(KeyValuePair<string, string>)))
                    {
                        list.Remove(originalPair);
                    }
                }


                // 新增新的資料
                list.Add(pair);

                Set(key, list, expired);

            }
            else
            {

                List<KeyValuePair<string, T>> list = new List<KeyValuePair<string, T>>();

                KeyValuePair<string, T> pair = new KeyValuePair<string, T>(hashKey, value);

                // 已經有這個 key
                if (_cache.Contains(key))
                {
                    list = Get<List<KeyValuePair<string, T>>>(key);

                    var originalPair = list.FirstOrDefault(t => t.Key == hashKey);

                    // 已經有這個 hashKey
                    if (!originalPair.Equals(default(KeyValuePair<string, T>)))
                    {
                        list.Remove(originalPair);
                    }
                }

                // 新增新的資料
                list.Add(pair);

                Set(key, list, expired);
            }


        }

        public Task HashSetAsync<T>(string key, string hashKey, T value, TimeSpan expired)
        {
            var task = Task.Run(() => HashSet(key, hashKey, value, expired));

            // exception 
            task.ContinueWith(t =>
            {
                ExceptionLogHandler(t.Exception, "HashSetAsync<T>", $"{{ \"key:\" \"{ key }\", \"hashKey\": \"{ hashKey }\", \"value\": \"{ value }\"}}");

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
            // MemoryCache 無法對 Key 做查詢
            // 所以直接回傳空集合

            return new string[0];
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
        /// <returns></returns>
        public bool LockRelease(string key, string token)
        {
            if (!_cache.Contains(key))
                return false;

            if (Get<string>(key) != token)
                return false;

            return Remove(key);

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

            // 已經有這個 key 了 (就算 token 相同，也不會重置 lockTime)
            if (_cache.Contains(key))
                return false;

            // key 不存在，進行鎖定時間設定
            return Set(key, token, lockTime);

        }

        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            bool result = true;

            try
            {
                if (_cache.Contains(key))
                {
                    var removedObject = _cache.Remove(key);

                    result = removedObject != null;
                }
            }
            catch
            {
                result = false;
            }

            return result;
        }

        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public Task<bool> RemoveAsync(string key)
        {
            var task = Task.Run(() => Remove(key));

            // exception 
            task.ContinueWith(t =>
            {
                ExceptionLogHandler(t.Exception, "RemoveAsync", $"{{ \"key:\" \"{ key }\" }}");

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

            bool result = true;
            try
            {
                foreach (string key in keys)
                {
                    Remove(key);
                }
            }
            catch
            {
                result = false;
            }

            return result;
        }

        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        public Task<bool> RemoveAsync(string[] keys)
        {
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

            var policy = new CacheItemPolicy();
            if (expired != NeverExpired)
            {
                policy.AbsoluteExpiration = DateTimeOffset.Now.Add(expired);
            }

            _cache.Set(key, value, policy);

            return true;


        }

        /// <summary>
        /// 設定快取值，TimeSpan 不指定時取預設值 (DefaultExpired => 預設值，NeverExpired => 永不過期)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        /// <returns></returns>
        public Task<bool> SetAsync<T>(string key, T value, TimeSpan expired = default)
        {

            var task = Task.Run(() => Set(key, value, expired));

            // exception
            task.ContinueWith(t =>
            {
                ExceptionLogHandler(t.Exception, "SetAsync<T>", $"{{ \"key:\" \"{ key }\", \"value\": \"{ value }\"}}");

            }, TaskContinuationOptions.OnlyOnFaulted);

            return task;
        }

    }

}
