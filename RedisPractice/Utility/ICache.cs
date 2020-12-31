using System;

namespace RedisPractice.Utility
{
    interface ICache
    {
        TimeSpan DefaultExpired { get; }

        TimeSpan NeverExpired { get; }

        /// <summary>
        /// 新增一筆 cache 資料
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeSpan"></param>
        void Add(string key, string value, TimeSpan timeSpan);

        /// <summary>
        /// 取得快取資料的 string 值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        string Get(string key);

        /// <summary>
        /// 取得快取資料並轉型成 T
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        T Get<T>(string key) where T : class;

        /// <summary>
        /// 取得快取資料的 string 值，無法取得時，以 func 來取得，並回寫 cache
        /// </summary>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <param name="ttlMin"></param>
        /// <returns></returns>
        string Get(string key, Func<string> func, int ttlMin = 2880);

        /// <summary>
        /// 取得快取資料並轉型成 T，無法取得時，以 func 來取得，並回寫 cache
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="func">資料不存在時，從DB取得資料的Func</param>
        /// <param name="ttlMin">資料快取時間</param>
        /// <returns></returns>
        T Get<T>(string key, Func<T> func, int ttlMin = 2880) where T : class;

        /// <summary>
        /// 使用一個 key 來實做 lock 功能
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        bool LockTake(string key, string value, TimeSpan timeSpan);

        /// <summary>
        /// 釋放用來 Lock 的 key 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        bool LockRelease(string key, string value);


        /// <summary>
        /// 依照傳入的 pattern 回傳相關的 keys
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="expectEmpty">預期結果是否為空陣列</param>
        /// <returns></returns>
        string[] Keys(string pattern, bool expectEmpty = false);

        /// <summary>
        /// 移除單筆 key 的資料
        /// </summary>
        /// <param name="key"></param>
        void Remove(string key);

        /// <summary>
        /// 移除多筆  keys 的資料
        /// </summary>
        /// <param name="keys"></param>
        void Remove(string[] keys);

        /// <summary>
        /// 傳照傳入的 pattern ，移除相關的資料
        /// </summary>
        /// <param name="pattern"></param>
        void RemovePattern(string pattern);


        /// <summary>
        /// HashSet
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="hashValue"></param>
        void HashSet(string key, string hashKey, string hashValue);

        /// <summary>
        /// HashGet 並轉型 T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        T HashGet<T>(string key, string hashKey);

        /// <summary>
        /// HashGet 並轉型 T , 如果沒有值，就執行 func 取值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        T HashGet<T>(string key, string hashKey, Func<T> func);

        /// <summary>
        /// 對 Hash Key 的舊值 + value 後，回傳 (回傳值等於現值)
        /// 如果是 func 取得，就直接將取得的值寫入 Hash Key 並回傳 (不用再加 value)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="hashKey"></param>
        /// <param name="value"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        double HashIncrement(string key, string hashKey, double value, Func<double> func);


    }
}
