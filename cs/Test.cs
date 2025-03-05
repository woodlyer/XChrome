using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using XChrome.cs.db;
using XChrome.cs.win32;

namespace XChrome.cs
{
    public class Test
    {
        public static async Task<bool> TestAndGoAsync()
        {
            
            return true;

            //=======开发测试=======

            var clist= Win32Helper.GetAllWindowsByClass("Chrome_WidgetWin_1");
            foreach(var v in clist)
            {
                // 获取窗口标题
                int titleLength = Win32Helper.GetWindowTextLength(v);
                StringBuilder sbTitle = new StringBuilder(titleLength + 1);
                Win32Helper.GetWindowText(v, sbTitle, sbTitle.Capacity);
                string title = sbTitle.ToString();
                if (title != "") continue;

                // 获取窗口类名，通常最大长度设定为 256
                const int maxClassNameLength = 256;
                StringBuilder sbClassName = new StringBuilder(maxClassNameLength);
                int classNameLength = Win32Helper.GetClassName(v, sbClassName, maxClassNameLength);
                string className = classNameLength != 0 ? sbClassName.ToString() : "未知";
                // 获取窗口对应的进程ID
                uint processId;
                uint threadId = Win32Helper.GetWindowThreadProcessId(v, out processId);

                Debug.WriteLine($"子窗口句柄: {v}, ClassName: {className}, 标题: {title}, 进程：{processId}");

            }


            //IntPtr chrome = (IntPtr)14095812;
            ////Win32Helper.FindWindowEx(chrome, IntPtr.Zero, "Intermediate D3D Window", "");

            //    List<IntPtr> clist = Win32Helper.GetAllChildWindows(chrome);
            //    foreach (var v in clist)
            //    {
            //        //Debug.WriteLine((long)v);
            //        // 获取窗口标题
            //        int titleLength = Win32Helper.GetWindowTextLength(v);
            //        StringBuilder sbTitle = new StringBuilder(titleLength + 1);
            //        Win32Helper.GetWindowText(v, sbTitle, sbTitle.Capacity);
            //        string title = sbTitle.ToString();

            //        // 获取窗口类名，通常最大长度设定为 256
            //        const int maxClassNameLength = 256;
            //        StringBuilder sbClassName = new StringBuilder(maxClassNameLength);
            //        int classNameLength = Win32Helper.GetClassName(v, sbClassName, maxClassNameLength);
            //        string className = classNameLength != 0 ? sbClassName.ToString() : "未知";

            //        Debug.WriteLine($"子窗口句柄: {v}, ClassName: {className}, 标题: {title}");
            //    }
            //    int xxx = 0;




            //var x=Win32Helper.FindWindow("", "OKX Wallet");

            return false;
        }
    }
}
