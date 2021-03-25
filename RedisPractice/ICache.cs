using System;

namespace RedisPractice
{
    public interface ICache
    {
        /// <summary>
        /// 預設逾時時間
        /// </summary>
        /// <value></value>
        TimeSpan DefaultExpired { get; }

        /// <summary>
        /// 永不逾期的 TimeSpan (不自動逾期，直到 cache 清除)
        /// </summary>
        /// <value></value>
        TimeSpan NeverExpired { get; }

        /// <summary>
        /// 新增一筆 cache 資料
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expired"></param>
        void Add(string key, string value, TimeSpan expired = default);

        /// <summary>
        /// 取得快取資料的 string 值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="expired"></param>
        /// <returns></returns>
        string Get(string key, TimeSpan expired = default);

        /// <summary>
        /// 取得快取資料並轉型成 T 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="expired"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T Get<T>(string key, TimeSpan expired = default);

        /// <summary>
        /// 取得快取資料的 string 值，無法取得時，以 func 來取 得，並回寫 cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <param name="ttlMin"></param>
        /// <returns></returns>
        string Get(string key, Func<string> func, int ttlMin = 2880);

        /// <summary>
        /// 取得取資料並轉型成 T，無法取得時，以 func 來取得，並回寫 cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <param name="ttlMin"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T Get<T>(string key, Func<T> func, int ttlMin = 2880);

        /// <summary>
        /// 使用一個 key 來實做 lock 功能
        /// </summary>
        /// <param name="key"></param>
        /// <param name="token"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        bool LockTake(string key, string token, TimeSpan timeSpan);

        /// <summary>
        /// 釋放用來 lock 的 key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        bool LockRelease(string key, string token);

        /// <summary>
        /// 依照傳入的 pattern 回傳相關的 keys
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="expectEmpty">預期結果是否為空陣列</param>
        /// <returns></returns>
        string[] Keys(string pattern, bool expectEmpty = false);

        /// <summary>
        /// 移除單筆資料
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool Remove(string key);

        /// <summary>
        /// 移除多筆 keys 的資料
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        bool Remove(string[] keys);

        /// <summary>
        /// 依照傳入的 pattern，移除相關的資料
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        bool RemovePattern(string pattern);

        /// <summary>
        /// 寫入 Hash (keyValuePair集合)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="hashValue"></param>
        /// <param name="expired"></param>
        /// <returns></returns>
        bool HashSet(string key, string hashKey, string hashValue, TimeSpan expired = default);

        /// <summary>
        /// HashGet 並轉型 T
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="expired"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T HashGet<T>(string key, string hashKey, TimeSpan expired = default);

        /// <summary>
        /// HashGet 並轉型 T，如果沒有值，就執行 func 取值，並回寫 cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="func"></param>
        /// <param name="expired"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T HashGet<T>(string key, string hashKey, func<T> func, TimeSpan expired = default);

        /// <summary>
        /// 對 Hash Key 的舊值 + value 後回傳(回傳值等於現值)
        /// 如果是 func 取得，就宜接寫傷 HashKey 並回傳 (不用再加 value)
        /// 因為使用多個 cache 和 func，來源不同，要注意怎麼取得唯一值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        double HashIncrement(string key, string hashKey, double value, Func<double> func);
        
    }
}