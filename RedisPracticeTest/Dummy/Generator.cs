using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RedisPracticeTest.Dummy
{
    public class Generator
    {
        private static readonly Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklumnpqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static int RandomInt(int maxValue)
        {
            return random.Next(maxValue);
        }

        public static int RandomInt(int minValue, int maxValue)
        {
            return random.Next(minValue, maxValue);
        }

        public static bool RandomBool()
        {
            return random.Next(1) == 1;
        }

        public static DateTime RandomDate()
        {
            DateTime now = DateTime.Now;

            DateTime randomDate = new DateTime(random.Next(now.Year - 5, now.Year + 5), random.Next(1, 13), 1);

            int days = DateTime.DaysInMonth(randomDate.Year, randomDate.Month);

            randomDate = new DateTime(randomDate.Year, randomDate.Month, random.Next(1, days + 1));

            return randomDate;

        }
    }
}
