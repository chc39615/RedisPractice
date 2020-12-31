using FluentAssertions;
using NUnit.Framework;
using RedisPractice.Provider;
using RedisPractice.Provider.Interface;
using RedisPracticeTest.Dummy;
using System;
using System.Collections.Generic;
using System.Threading;

namespace RedisPracticeTest
{
    public class ProviderTests
    {

        private static IEnumerable<ICacheProvider> GetTestCaseOfProviders
        {
            get
            {
                yield return new RedisCacheProvider();
                yield return new MemoryCacheProvider();
            }
        }

        private static IEnumerable<ICacheProvider> GetTestCaseOfProviders2
        {
            get
            {
                yield return new RedisCacheProvider();
            }
        }

        /// <summary>
        /// HashGet, HashSet 測試
        /// </summary>
        /// <param name="cache"></param>
        [Test, TestCaseSource("GetTestCaseOfProviders")]
        public void TestHashFunction(ICacheProvider cache)
        {
            string key = "HashTest";

            int collectionLength = 10;

            List<KeyValuePair<string, DummyClass>> list = new List<KeyValuePair<string, DummyClass>>();

            for (int i = 0; i < collectionLength; i++)
            {
                list.Add(new KeyValuePair<string, DummyClass>(i.ToString(), new DummyClass()));
            }

            // HashSet Test 
            foreach (var pair in list)
            {
                cache.HashSet(key, pair.Key, pair.Value);
            }

            // HashGet Test & Verify
            foreach (var pair in list)
            {
                var hashData = cache.HashGet<DummyClass>(key, pair.Key);

                hashData.Should().BeEquivalentTo(pair.Value);

            }

            // overwrite Test
            List<KeyValuePair<string, DummyClass>> newList = new List<KeyValuePair<string, DummyClass>>();

            for (int i = 0; i < collectionLength; i++)
            {
                newList.Add(new KeyValuePair<string, DummyClass>(i.ToString(), new DummyClass()));
            }

            // overwrite
            foreach (var pair in newList)
            {
                cache.HashSet(key, pair.Key, pair.Value);
            }


            // compare not equal
            foreach (var pair in list)
            {
                var hashData = cache.HashGet<DummyClass>(key, pair.Key);

                hashData.Should().NotBeEquivalentTo(pair.Value);

            }

            // equal
            foreach (var pair in newList)
            {
                var hashData = cache.HashGet<DummyClass>(key, pair.Key);

                hashData.Should().BeEquivalentTo(pair.Value);

            }

            // remove test
            cache.Remove(key);

            foreach (var pair in newList)
            {
                var hashData = cache.HashGet<DummyClass>(key, pair.Key);

                hashData.Should().BeNull();
            }
        }


        /// <summary>
        /// Get, Set 測試
        /// </summary>
        /// <param name="cache"></param>
        [Test, TestCaseSource("GetTestCaseOfProviders")]
        public void TestGetSetFunction(ICacheProvider cache)
        {
            string key = "GetSetTest";
            string data = Generator.RandomString(5);

            cache.Set(key, data);

            string actualData = cache.Get<string>(key);

            actualData.Should().BeEquivalentTo(actualData);

            cache.Remove(key);

            actualData = cache.Get<string>(key);

            actualData.Should().BeNull();

            DummyClass genericData = new DummyClass();

            cache.Set(key, genericData);

            DummyClass actualGenericData = cache.Get<DummyClass>(key);

            actualGenericData.Should().BeEquivalentTo(genericData);

            cache.Remove(key);

            actualGenericData = cache.Get<DummyClass>(key);

            actualGenericData.Should().BeNull();
        }


        /// <summary>
        /// 在 lock 時間內，重復取 key，回傳 false
        /// </summary>
        /// <param name="cache"></param>
        [Test, TestCaseSource("GetTestCaseOfProviders")]
        public void LockReleaseTestInTimeReturnFalse(ICacheProvider cache)
        {
            string key = "lockTest";

            string value = Generator.RandomString(5);

            cache.LockTake(key, value, new TimeSpan(0, 0, 30));

            // 不同的 value
            var actual = cache.LockTake(key, Generator.RandomString(5), new TimeSpan(0, 0, 30));

            Assert.IsFalse(actual);

            // 相同的 value
            actual = cache.LockTake(key, value, new TimeSpan(0, 0, 30));

            Assert.IsFalse(actual);

            // 移除 LockKey
            cache.Remove(key);

        }

        /// <summary>
        /// Lock、Release 測試，在時間內，不同、相同的 value 是否可以 Release
        /// </summary>
        /// <param name="cache"></param>
        [Test, TestCaseSource("GetTestCaseOfProviders")]
        public void LockReleaseTestInTimeRelease(ICacheProvider cache)
        {
            string key = "lockTest";

            string value = Generator.RandomString(5);

            cache.LockTake(key, value, new TimeSpan(0, 0, 30));

            var actual = cache.LockRelease(key, Generator.RandomString(5));

            Assert.IsFalse(actual);

            actual = cache.LockRelease(key, value);

            Assert.IsTrue(actual);

            cache.Remove(key);

        }

        /// <summary>
        /// Lock, Release 測試，超過時間後，是否自動解鎖
        /// </summary>
        /// <param name="cache"></param>
        [Test, TestCaseSource("GetTestCaseOfProviders")]
        public void LockReleaseTestOverTime(ICacheProvider cache)
        {
            string key = "lockTest";

            string value = Generator.RandomString(5);

            cache.LockTake(key, value, new TimeSpan(0, 0, 1));

            Thread.Sleep(1005);

            var actual = cache.LockTake(key, value, new TimeSpan(0, 0, 1));

            Assert.IsTrue(actual);

            Thread.Sleep(1005);

            // 超過時間後，Release 會是 false
            actual = cache.LockRelease(key, value);

            Assert.IsFalse(actual);

        }


        [Test, TestCaseSource("GetTestCaseOfProviders")]
        public void HashIncrmentTest(ICacheProvider cache)
        {
            string key = "HashIncrementTest";

            string hashKey = "0";

            double init = 10;

            double increment = 1;

            double expect = init + increment;

            cache.HashSet(key, hashKey, init);

            double actual = cache.HashIncrement(key, hashKey, increment);

            Assert.AreEqual(expect, actual);

        }


        [Test, TestCaseSource("GetTestCaseOfProviders")]
        public void HashIncrementFuncTest(ICacheProvider cache)
        {

            string key = "HashIncrementTest";

            string hash = "0";

            cache.Remove(key);

            double expect = 22;

            double func() => expect;

            double actual = cache.HashIncrement(key, hash, 1, func);

            Assert.AreEqual(expect, actual);

            actual = cache.HashIncrement(key, hash, 1, func);

            Assert.AreEqual(expect + 1, actual);

        }


        [Test, TestCaseSource("GetTestCaseOfProviders2")]
        public void KeysTest(ICacheProvider cache)
        {
            int keyCounts = Generator.RandomInt(5, 20);

            string keyPrefix = Generator.RandomString(5);

            List<string> expectKeys = new List<string>();

            for (int i = 0; i < keyCounts; i++)
            {
                string key = $":{keyPrefix}:{i:d2}";

                expectKeys.Add(key);

                cache.Set(key, $"{i:d2}");
            }

            var actualKeys = cache.Keys($"*{keyPrefix}:*");

            Assert.AreEqual(expectKeys.Count, actualKeys.Length);

            foreach(string key in actualKeys)
            {
                Assert.IsTrue(expectKeys.Contains(key));
            }

            cache.Remove(actualKeys);

            actualKeys = cache.Keys($"*{keyPrefix}:*");

            Assert.AreEqual(0, actualKeys.Length);

        }

    }
}