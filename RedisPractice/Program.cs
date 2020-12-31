using RedisPractice.Provider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RedisPractice
{
    class Program
    {
        static async Task Main(string[] args)
        {

            int times = 0;
            while (times < 10)
            {

                List<Task> taskList = new List<Task>();
                for (int i = 0; i < 4; i++)
                {
                    taskList.Add(
                        Task.Run(() =>
                        {
                            var process = new PrepProcess();

                            try
                            {
                                process.RedisAction();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }

                        })
                    );
                }

                await Task.WhenAll(taskList);

                times++;
                Thread.Sleep(200);
            }



        }

        public class PrepProcess
        {

            public string RandomString(int length)
            {
                Random random = new Random();

                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                return new string(Enumerable.Repeat(chars, length)
                  .Select(s => s[random.Next(s.Length)]).ToArray());
            }

            public void RedisAction()
            {
                string rand = RandomString(5);

                RedisCacheProvider redisCacheProvider = new RedisCacheProvider();

                redisCacheProvider.Set(rand, rand);

                Thread.Sleep(300);

                redisCacheProvider.Get<string>(rand);

                Thread.Sleep(300);

                redisCacheProvider.Remove(rand);
            }

        }


    }



}
