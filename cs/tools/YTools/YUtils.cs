#region copyright
/**
// --------------------------------------------------------------------------------
// 文件名：YUtils.cs
// 作者：刹那 https://x.com/chanawudi
// 公司：https://x.com/chanawudi
// 更新日期：2025，2，27，13:54
// 版权所有 © Your Company. 保留所有权利。
// --------------------------------------------------------------------------------
*/
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace XChrome.cs.tools.YTools
{
    public class YUtils
    {
        public static string GetRandomUserAgent(string zhongzi)
        {

            try
            {
                int zz = 0;
                using (var md5 = MD5.Create())
                {
                    var inputBytes = Encoding.UTF8.GetBytes(zhongzi);
                    byte[] hashBytes = md5.ComputeHash(inputBytes);
                    zz = BitConverter.ToInt32(hashBytes, 0);
                }
                Random rand = new Random(zz);
                string u = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/";
                u += rand.Next(93, 121);
                u += ".0." + rand.Next(500, 5166);
                u += "." + rand.Next(60, 200);
                u += " Safari/537.36";
                return u;
            }
            catch (Exception ev)
            {
                string uu = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36";
                return uu;
            }

        }

        public static long GetTime13(DateTime time, bool isUTC = false)
        {
            System.DateTime startTime = isUTC ?
                TimeZone.CurrentTimeZone.ToUniversalTime(new System.DateTime(1970, 1, 1)) :
                TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
            long timeStamp = (long)(time - startTime).TotalMilliseconds; // 相差秒数
            return timeStamp;
        }

        public static long GetTime10(DateTime time, bool isUTC = false)
        {
            System.DateTime startTime = isUTC ?
                TimeZone.CurrentTimeZone.ToUniversalTime(new System.DateTime(1970, 1, 1)) :
                TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1));
            return (int)(time - startTime).TotalSeconds;
        }

        public static long GetTime10_sample(DateTime time)
        {
            bool isUTC = false;
            System.DateTime startTime = isUTC ?
                TimeZone.CurrentTimeZone.ToUniversalTime(new System.DateTime(1970, 1, 1)) :
                TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1));
            return (int)(time - startTime).TotalSeconds;
        }

        public static DateTime ConventToTimeFrom10(string timeStamp, bool isUTC = false)
        {

            DateTime dtStart = isUTC ?
                TimeZone.CurrentTimeZone.ToUniversalTime(new DateTime(1970, 1, 1)) :
                TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1));
            long lTime = long.Parse(timeStamp + "0000000");
            TimeSpan toNow = new TimeSpan(lTime);
            return dtStart.Add(toNow);
        }

        public static DateTime ConventToTimeFrom13(string timeStamp, bool isUTC = false)
        {
            timeStamp = timeStamp.Substring(0, 10);
            return ConventToTimeFrom10(timeStamp, isUTC);
        }


        

        
    }
}
