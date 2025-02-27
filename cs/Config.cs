#region copyright
/**
// --------------------------------------------------------------------------------
// 文件名：Config.cs
// 作者：刹那 https://x.com/chanawudi
// 公司：https://x.com/chanawudi
// 更新日期：2025，2，27，13:55
// 版权所有 © Your Company. 保留所有权利。
// --------------------------------------------------------------------------------
*/
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace XChrome.cs
{
    public class Config
    {
        /// <summary>
        /// 数据存储位置
        /// </summary>
        public static string chrome_data_path = Path.Combine(Directory.GetCurrentDirectory(), "chrome_data");
        /// <summary>
        /// 启动页
        /// </summary>
        public static string chrome_start_page = "https://web3tool.vip/browser/";
        /// <summary>
        /// 社群页面
        /// </summary>
        /// <returns></returns>
        public static string community_url = "http://web3tool.vip/xchrome/community";

        /// <summary>
        /// github
        /// </summary>
        public static string github_url = "http://web3tool.vip/xchrome/github";
        public static async Task ini()
        {
            if (!Path.Exists(chrome_data_path)) { Directory.CreateDirectory(chrome_data_path); }
        }
    }
}
