using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using IdentityModel.OidcClient;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Threading;

namespace DeviantArtUploader.src
{
    internal class LoginProc
    {
        public static string GetActiveTabUrl()
        {
            Process[] procsChrome = Process.GetProcessesByName("chrome");

            if (procsChrome.Length <= 0)
                return null;

            while (true)
            {
                foreach (Process proc in procsChrome)
                {
                    // the chrome process must have a window 
                    if (proc.MainWindowHandle == IntPtr.Zero)
                        continue;
                    Thread.Sleep(50);
                    // to find the tabs we first need to locate something reliable - the 'New Tab' button 
                    AutomationElement root = AutomationElement.FromHandle(proc.MainWindowHandle);
                    var SearchBar = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"));

                    string SomethingToReturn = (string)SearchBar.GetCurrentPropertyValue(ValuePatternIdentifiers.ValueProperty);
                    if (SomethingToReturn.Contains("code=")) return SomethingToReturn;
                }
            }
        }
        public static string GetRequestPostData(HttpListenerRequest request)
        {
            if (!request.HasEntityBody)
            {
                return null;
            }

            using (var body = request.InputStream)
            {
                using (var reader = new System.IO.StreamReader(body, request.ContentEncoding))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
