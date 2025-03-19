using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
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



            foreach (var screen in ScreenInfo.AllScreens)
            {
                // 转换为 WPF 物理像素 (考虑 DPI 缩放)
                var scaledBounds = new Rect(
                    screen.Bounds.X / screen.DpiScaleX,
                    screen.Bounds.Y / screen.DpiScaleY,
                    screen.Bounds.Width / screen.DpiScaleX,
                    screen.Bounds.Height / screen.DpiScaleY);

                Debug.WriteLine($"Display: {screen.DeviceName}");
                Debug.WriteLine($"Primary: {screen.IsPrimary}");
                Debug.WriteLine($"Resolution: {scaledBounds.Width}x{scaledBounds.Height}");
            }

            var primaryScreen = ScreenInfo.AllScreens.FirstOrDefault(s => s.IsPrimary);
            if (primaryScreen != null)
            {
                var workArea = primaryScreen.WorkingArea;
                var scaledWorkArea = new Rect(
                    workArea.X / primaryScreen.DpiScaleX,
                    workArea.Y / primaryScreen.DpiScaleY,
                    workArea.Width / primaryScreen.DpiScaleX,
                    workArea.Height / primaryScreen.DpiScaleY);

                Debug.WriteLine($"Available workspace: {scaledWorkArea}");
            }

            return false;
        }

      
      

        

    }
}
