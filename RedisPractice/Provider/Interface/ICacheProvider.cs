using System;
using System.Threading.Tasks;

namespace RedisPractice.Provider.Interface
{
    public interface ICacheProvider
    {
        /// <summary>
        /// 預設過期 TimeSpan
        /// </summary>
        //TimeSpan DefaultExpired => System.Configuration.ConfigurationManager.AppSettings["MemeryCacheTime"];
        TimeSpan DefaultExpired { get; }

        /// <summary>
        /// 永不過期的 TimeSpan ( 不自動過期，直到 cache 清除)
        /// </summary>
        TimeSpan NeverExpired { get; }

        /// <summary>
        /// 快取順序
        /// </summary>
        int CachePriority { get; set; }

        /// <summary>
        /// 設定快取值，TimeSpan 不指定時取預設值 (DefaultExpired => 預設值，NeverExpired => 永不過期)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        /// <returns></returns>
        bool Set<T>(string key, T value, TimeSpan expired = default);

        /// <summary>
        /// 設定快取值，TimeSpan 不指定時取預設值 (DefaultExpired => 預設值，NeverExpired => 永不過期)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        /// <returns></returns>
        Task<bool> SetAsync<T>(string key, T value, TimeSpan expired = default);

        /// <summary>
        /// 取得快取裡的值，並轉成指定的類型，如果沒有值就回傳 null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        T Get<T>(string key) where T : class;

        /// <summary>
        /// 取得快取裡的值，如果沒有值，就執行 func ，並回寫快取 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        T Get<T>(string key, Func<T> func) where T : class;

        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool Remove(string key);


        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<bool> RemoveAsync(string key);

        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        bool Remove(string[] keys);

        /// <summary>
        /// 移除快取中的 key 值
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        Task<bool> RemoveAsync(string[] keys);

        /// <summary>
        /// 依照傳入的 pattern 取得相關的 key
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        string[] Keys(string pattern);

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
        bool LockTake(string key, string token, TimeSpan lockTime);

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
        bool LockRelease(string key, string token);

        /// <summary>
        /// 寫入Hash (KeyValuePair的集合)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        void HashSet<T>(string key, string hashKey, T value, TimeSpan expired = default);


        /// <summary>
        /// 寫入Hash (KeyValuePair的集合)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        Task HashSetAsync<T>(string key, string hashKey, T value, TimeSpan expired = default);

        /// <summary>
        /// 取得Hash值(其中一組 KeyValuePair 的 value)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        T HashGet<T>(string key, string hashKey);


        /// <summary>
        /// 將 HashKey 值加 value 後，取出 (代表目前的值)
        /// 如果沒有這個 key-hashKey, 將會從 1 開始
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        double HashIncrement(string key, string hashKey, double value);

        /// <summary>
        /// 將 HashKey 值加 value 後，取出 (代表目前的值)，如果沒有辦法取得，就執行 func 來取得
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        double HashIncrement(string key, string hashKey, double value, Func<double> func);
    }
}
