using System;
using System.Threading.Tasks;

namespace RedisPractice.Providers
{
    public interface ICacheProvider
    {
        /// <summary>
        /// 預設逾期 TimeSpane
        /// </summary>
        /// <value></value>
        TimeSpan DefaultExpired { get; }

        /// <summary>
        /// 永不逾期的 TimeSpan (不自動逾期，直到 cache 清除)
        /// </summary>
        /// <value></value>
        TimeSpan NeverExpired { get; }

        /// <summary>
        /// 快取順序
        /// </summary>
        /// <value></value>
        int CachePriority { get; set; }

        /// <summary>
        /// 設定快取值，TimeSpane 不指定時取預設值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        bool Set<T>(string key, T value, TimeSpan expired = default);

        /// <summary>
        /// 非同步設定快取值，TimeSpane 不指定時取預設值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task<bool> SetAsync<T>(string key, T value, TimeSpan expired = default);

        /// <summary>
        /// 取得快取值，轉型成 T 後回傳
        /// </summary>
        /// <param name="key"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T Get<T>(string key);

        /// <summary>
        /// 移除快取值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool Remove(string key);

        /// <summary>
        /// 移除多組快取值
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        bool Remove(string[] keys);

        /// <summary>
        /// 非同步移除快取值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<bool> RemoveAsync(string key);

        /// <summary>
        /// 非同步移除多組快取值
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
        /// 寫入 Hash (keyValuePair集合)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        bool HashSet<T>(string key, string hashKey, T value, TimeSpan expired = default);

        /// <summary>
        /// 非同步寫入 Hash (keyValuePair集合)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task<bool> HashSetAsync<T>(string key, string hashKey, T value, TimeSpan expired = default);

        /// <summary>
        /// 取得Hash裡的值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T HashGet<T>(string key, string hashKey);

        /// <summary>
        /// 將 HashKey 值加 value 後取出 (代表目前的值)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        double HashIncrement(string key, string hashKey, double value);
    }
}