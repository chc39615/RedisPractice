using System.ComponentModel;

namespace RedisPractice.EnumType
{
    public enum ResultCodeEnum
    {
        #region 系統類(1000)，編碼區間0000~0999
        
        /// <summary>
        /// 執行成功
        /// </summary>
        [Description("0000")]
        Success,

        /// <summary>
        /// 未登入系統
        /// </summary>
        [Description("0001")]
        UnLogin,

        /// <summary>
        /// 連線到 Cache 失敗
        /// </summary>
        [Description("0019")]
        ConnectCacheFail


        #endregion


    }
}
