using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace XChrome.cs
{
    public class Test
    {
        public static async Task<bool> TestAndGoAsync()
        {
            return true;

            //=======开发测试=======
            var ip=await cs.IpChecker.CheckAsync(cs.ChecKUrl.api_ipapi_is);
            MessageBox.Show(ip.Item1+","+ip.Item2);



            return false;
        }
    }
}
