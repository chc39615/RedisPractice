using RedisPractice.Exceptions;
using System.Collections.Generic;

namespace RedisPractice.Utility
{
    interface ILogger
    {
        /// <summary>
        /// 紀錄錯誤訊息
        /// </summary>
        /// <param name="ex"></param>
        /// <param name="info"></param>
        public void WriteErrorLog(OperationFailedException ex, Dictionary<string, string> info);


        /// <summary>
        /// 紀錄一般資訊
        /// </summary>
        /// <param name="log"></param>
        public void WriteNomalLog(string log);
    }
}
