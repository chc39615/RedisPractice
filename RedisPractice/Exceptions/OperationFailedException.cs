using log4net.Core;
using RedisPractice.EnumType;
using RedisPractice.Extensions;
using System;
using System.Collections.Generic;

namespace RedisPractice.Exceptions
{
    public class OperationFailedException : Exception
    {
        /// <summary>
        /// 取得錯誤列舉
        /// </summary>
        public ResultCodeEnum MessageCodeEnum { get; private set; }

        /// <summary>
        ///  取得錯誤等級
        /// </summary>
        public Level LogLevel { get; private set; }

        /// <summary>
        /// 設定或取得與此例外相關的資料
        /// </summary>
        private Dictionary<string, object> DataInternal { get; set; }

        /// <summary>
        /// 依照傳入的 ResultCodeEnum 判斷 log level
        /// </summary>
        /// <param name="messageCodeEnum"></param>
        /// <returns></returns>
        private static Level GetLogLevel(ResultCodeEnum messageCodeEnum)
        {
            return Level.Warn;
        }

        /// <summary>
        /// 使用指定的錯誤代碼和造成這個 Exception 的 innerException 參考，初始化 <see cref="OperationFailedException" /> 類別的新執行個體
        /// </summary>
        /// <param name="messageCodeEnum">解釋例外狀況原因的錯誤代碼列舉</param>
        /// <param name="logLevel">例外等級</param>
        /// <param name="innerException">适成目前 exception 的 innerException，若未指定，則為 null 參考</param>
        /// <param name="data">要帶入至此 exception 的相關參數</param>
        public OperationFailedException(ResultCodeEnum messageCodeEnum, Level logLevel,
                                    Exception innerException, Dictionary<string, object> data = null)
            : base(messageCodeEnum.GetEnumDescription(), innerException)
        {
            
            LogLevel = logLevel ?? throw new ArgumentNullException("logLevel");

            MessageCodeEnum = messageCodeEnum;

            DataInternal = data;
        }

        /// <summary>
        /// 使用指定的錯誤代碼，初始化 <see cref="OperationFailedException"/> 類別的新執行個體
        /// </summary>
        /// <param name="data">要帶入至此例外的相關參數</param>
        /// <param name="messageCodeEnum">解釋例外狀況原因的錯誤代碼列舉</param>
        public OperationFailedException(ResultCodeEnum messageCodeEnum, Dictionary<string, object> data = null)
            : this(messageCodeEnum, GetLogLevel(messageCodeEnum), null, data)
        { }




    }
}
