using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using XChrome.cs.db;
using XChrome.cs.win32;
using XChrome.cs.zchrome;

namespace XChrome.cs
{
    public class Test
    {
        public static async Task<bool> TestAndGoAsync()
        {

            return true;

            Debug.WriteLine("77777");
            //=======开发测试=======
            using(var job=new ZJob())
            {
                Debug.WriteLine("88888");
                await testZChrome(job);

                await Task.Run(async () => { 
                    await Task.Delay(100000000);
                });
            }



            return false;
        }


        private static async Task testZChrome(ZJob job)
        {
            // 实例化 ChromeDevToolsClient
  

            return;
        }

    }
}
