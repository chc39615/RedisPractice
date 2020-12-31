using Newtonsoft.Json;
using RedisPractice.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;

namespace RedisPractice.Utility
{
    public class TextLogger : ILogger
    {
        private readonly DirectoryInfo logFolder = new DirectoryInfo(@"C:\LogsTemp\RedisPractice\");

        private readonly string logFile;

        private static readonly object locker = new object();

        public TextLogger()
        {
            if (!logFolder.Exists)
                Directory.CreateDirectory(logFolder.FullName);

            logFile = Path.Combine(logFolder.FullName, DateTime.Now.ToString("yyyy-MM-dd") + ".log");

        }

        public void WriteErrorLog(OperationFailedException ex, Dictionary<string, string> info)
        {
            string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffffK}] ";

            log += ex.InnerException;

            if (info.Count > 0)
            {
                log += Environment.NewLine + JsonConvert.SerializeObject(info);
            }

            log += Environment.NewLine;

            WriteToFile(log);

        }

        public void WriteNomalLog(string msg)
        {
            string log = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}] ";

            log += msg;

            log += Environment.NewLine;

            WriteToFile(log);

        }


        private void WriteToFile(string msg)
        {
            if (!File.Exists(logFile))
            {
                lock (locker)
                {
                    using StreamWriter sw = File.CreateText(logFile);
                    sw.Write(msg);
                }
            }
            else
            {
                lock (locker)
                {
                    using StreamWriter sw = File.AppendText(logFile);
                    sw.Write(msg);
                }
            }
        }

    }
}
