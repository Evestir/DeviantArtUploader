using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using DeviantArtUploader.src;
using System.Web;
using System.Linq.Expressions;
using ToastNotifications;
using ToastNotifications.Core;
using ToastNotifications.Lifetime;
using ToastNotifications.Position;
using ToastNotifications.Messages;
using System.Windows.Interop;

namespace DeviantArtUploader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("Kernel32")]
        public static extern void AllocConsole();

        [DllImport("Kernel32")]
        public static extern void FreeConsole();

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, [In] ref bool attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_CAPTION_COLOR = 35;

        private static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            if (IsWindows10OrGreater(17763))
            {
                var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (IsWindows10OrGreater(18985))
                {
                    attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                }

                bool value = enabled;

                int useImmersiveDarkMode = enabled ? 1 : 0;
                if (DwmSetWindowAttribute(handle, (int)attribute, ref value, Marshal.SizeOf<bool>()) == 1) return true;
                else return false;
            }

            return false;
        }

        private static bool IsWindows10OrGreater(int build = -1)
        {
            return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= build;
        }

        public MainWindow()
        {
            InitializeComponent();
            IntPtr hWnd = new WindowInteropHelper(this).EnsureHandle();
            UseImmersiveDarkMode(hWnd, true);
        }

        bool isWide = false;
        int CurrentNumb = 0;
        List<string> ImageList = new List<string>();
        string CurrentShowingImageName = null;

        public void Notify(string message, int second)
        {
            Snackbar.MessageQueue?.Enqueue(message, null, null, null, false, true, TimeSpan.FromSeconds(second));
        }

        float Blend(float time, float startValue, float change, float duration)
        {
            time /= duration / 2;
            if (time < 1)
            {
                return change / 2 * time * time + startValue;
            }

            time--;
            return -change / 2 * (time * (time - 2) - 1) + startValue;
        }
        public class Token
        {
            public string client_id { get; set; }
            public string client_secret { get; set; }
            public string grant_type { get; set; }
            public string code { get; set; }
            public string redirect_uri { get; set; }
        }
        public static string GetAccessToken(string response)
        {
            // Parse the JSON into a dictionary
            var responseDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);

            // Get the value of the access_token field
            string accessToken = responseDict["access_token"].ToString();

            //Console.WriteLine("[*] Access Token is: " + accessToken);

            return accessToken;
        }
        string accesstoken = string.Empty;
        private async Task Upload(string PathToImage)
        {
            using(var client = new HttpClient())
            {
                /* New Way */
                if (accesstoken == string.Empty)
                {
                    var authorizationEndpoint = "https://www.deviantart.com/oauth2/authorize";

                    var clientId = "24845";
                    string client_secret = "d6aba10da2769a0c34fe92ffcf33d8e8";
                    var redirectUri = "https://google.com";
                    var authorizationRequest = $"{authorizationEndpoint}?response_type=code&client_id={clientId}&redirect_uri={redirectUri}";

                    Process.Start("C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", authorizationRequest);

                    string redirected = LoginProc.GetActiveTabUrl();
                    string authorization_code = redirected.Substring(redirected.IndexOf('=') + 1);

                    Notify($"ℹ️ Found Auth Key: {authorization_code}", 1);

                    /* Obtain Token */
                    var tokenObtainer = new Uri(@"https://www.deviantart.com/oauth2/token");

                    var token = new Token()
                    {
                        client_id = "24845",
                        client_secret = "d6aba10da2769a0c34fe92ffcf33d8e8",
                        grant_type = "authorization_code",
                        code = authorization_code,
                        redirect_uri = "https://google.com"
                    };

                    string postData = $"grant_type=authorization_code&client_id={clientId}&client_secret={client_secret}&code={authorization_code}&redirect_uri=https://google.com";
                    // Create a WebRequest object to send the POST request
                    WebRequest request = WebRequest.Create(tokenObtainer);
                    request.Method = "POST";
                    request.ContentType = "application/x-www-form-urlencoded";

                    using (var writer = new StreamWriter(request.GetRequestStream()))
                    {
                        writer.Write(postData);
                    }

                    WebResponse response = request.GetResponse();
                    string responseString = string.Empty;
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        responseString = reader.ReadToEnd();
                    }

                    //Console.WriteLine("[-] " + responseString);

                    /* Submit */
                    accesstoken = GetAccessToken(responseString);
                }

                // Load the image file into a byte array
                //Console.WriteLine($"[-] Target Image Path: {PathToImage}");

                // Construct the request body
                var content = new MultipartFormDataContent();
                content.Add(new StreamContent(File.OpenRead(PathToImage)), "test", "image.jpg");
                content.Add(new StringContent(DevName.Text), "title");
                //content.Add(new StringContent("This is my deviation description."), "description");
                content.Add(new StringContent(accesstoken), "access_token");

                // Construct the HTTP request
                var responsed = await client.PostAsync("https://www.deviantart.com/api/v1/oauth2/stash/submit", content);


                // Handle the response
                string responseContent = await responsed.Content.ReadAsStringAsync();
                dynamic responseData = JsonConvert.DeserializeObject(responseContent);
                //Console.WriteLine($"[*] Response: {responseData}");

                string DeviantID = responseData.itemid;
                if (responseData.status == "success")
                {
                    Notify($"ℹ️ Deviation submitted successfully with ID: {DeviantID}", 1);
                }
                else
                {
                    string errormsg = responseData.error_message;
                    Notify($"⚠️ Error submitting deviation: {errormsg}", 1);
                }

                content = null;
                content = new MultipartFormDataContent();
                if (NudityCheckBox.IsChecked == true)
                {
                    content.Add(new StringContent("true"), "is_mature");
                    content.Add(new StringContent("moderate"), "mature_level");
                }
                else
                {
                    content.Add(new StringContent("false"), "is_mature");
                }
                if (WatermarkCheckBox.IsChecked == true)
                {
                    content.Add(new StringContent("true"), "add_watermark");
                }
                content.Add(new StringContent("true"), "agree_submission");
                content.Add(new StringContent(DeviantID), "itemid");
                content.Add(new StringContent("true"), "agree_tos");
                content.Add(new StringContent(accesstoken), "access_token");

                responsed = await client.PostAsync("https://www.deviantart.com/api/v1/oauth2/stash/publish", content);
                responseContent = await responsed.Content.ReadAsStringAsync();
                responseData = JsonConvert.DeserializeObject(responseContent);
                //Console.WriteLine($"[*] Response: {responseData}");

                if (responseData.status == "success")
                {
                    string deviationId = responseData.deviationid;
                    Notify("ℹ️ Deviation published successfully", 1);
                }
                else
                {
                    string errormsg = responseData.error_message;
                    Notify($"❌ Error publishing deviation: {errormsg}", 1);
                }
                responseData = null;
                content = null;
            }
        }
        private void Update(int ver)
        {
            double x = 0;

            if (ver == 0)
            {
                while (x < 1)
                {
                    x = x + 0.015;
                    this.Dispatcher.Invoke(() =>
                    {
                        this.Height = 210 + Blend((float)x, 0, 650, 1f);
                        ImageHolder.Opacity = x;
                    });
                    Thread.Yield();
                }
            }
            else if (ver == 1)
            {
                while (x < 1)
                {
                    x = x + 0.01;
                    this.Dispatcher.Invoke(() =>
                    {
                        this.Height = 860 - Blend((float)x, 0, 650, 1f);
                        ImageHolder.Opacity = 1 - x;
                    });
                    Thread.Yield();
                }
            }
            else if (ver == 2)
            {
                while (x < 1)
                {
                    x = x + 0.02;
                    this.Dispatcher.Invoke(() =>
                    {
                        ImageHolder.Opacity = 1 - x;
                    });
                    Thread.Sleep(1);
                }

                this.Dispatcher.Invoke(() =>
                {
                    ShowImage(CurrentNumb);
                });

                x = 0;

                Thread.Sleep(100);

                while (x < 1)
                {
                    x = x + 0.01;
                    this.Dispatcher.Invoke(() =>
                    {
                        ImageHolder.Opacity = x;
                    });
                    Thread.Sleep(1);
                }
            }
        }
        private void Opener_Click(object sender, RoutedEventArgs e)
        {
            if (isWide == false && this.Height == 210) 
            {
                Thread thread = new Thread(delegate ()
                {
                    Update(0);
                });

                thread.Start();

                isWide = true;
            }
            else if (isWide == true)
            {
                Thread thread = new Thread(delegate ()
                {
                    Update(1);
                });

                thread.Start();

                isWide = false;
            }
        }
        private bool ShowImage(int y)
        {
            if (ImageList != null && y < ImageList.Count)
            {
                CurrentShowingImageName = ImageList[y].ToString();
                ImageHolder.Source = new BitmapImage(new Uri(ImageList[y]));
                return true;
            }
            else return false;
        }
        private void Find_Click(object sender, RoutedEventArgs e)
        {
            string PathLocation = PathLoc.Text;
            if (Directory.Exists(PathLocation))
            {
                string[] files = Directory.GetFiles(PathLocation); // Get all files in the directory

                ImageList.Clear();
                CurrentNumb = 1; 

                foreach (string file in files)
                {
                    if (System.IO.Path.GetExtension(file) == ".png" | System.IO.Path.GetExtension(file) == ".jpg" | System.IO.Path.GetExtension(file) == ".webp")
                    {
                        ImageList.Add(file); // Add the file to the list
                        //AllocConsole();
                        //Console.WriteLine(file);
                    }
                }
            }
            else
            {
                Notify("⚠️ The Intended path was not found!", 1);
                return;
            }

            if (ImageList.Count > 0)
            {
                if (isWide == false)
                {
                    ShowImage(CurrentNumb);
                    Thread thread = new Thread(delegate ()
                    {
                        Update(0);
                    });

                    thread.Start();
                    isWide = true;
                }
                else if (isWide == true)
                {
                    ShowImage(CurrentNumb);
                }
            }
            else
            {
                Notify("⚠️ The folder is empty!", 1);
                return;
            }
        }
        private void Yess_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNumb != 0)
            {
                CurrentNumb++;
                if (CurrentNumb < ImageList.Count)
                {
                    //Console.WriteLine($"Current Numb: {CurrentNumb} ImageList.Count: {ImageList.Count}");

                    Thread thread = new Thread(delegate ()
                    {
                        Update(2);
                    });

                    thread.Start();
                }
                else
                {
                    CurrentNumb--;
                    Notify("⚠️ No more images are left.", 1);
                }
            }
        }

        private void Nope_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNumb - 2 <= ImageList.Count && CurrentNumb > 1)
            {
                CurrentNumb -= 1;
                //Console.WriteLine($"Current Numb: {CurrentNumb} ImageList.Count: {ImageList.Count}");

                Thread thread = new Thread(delegate ()
                {
                    Update(2);
                });

                thread.Start();
            }
            else
            {
                Notify("⚠️ No more images are left.", 1);
            }
        }

        private void UploadBtn_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNumb > 0)
            {
                Upload(ImageList[CurrentNumb].ToString());
            }
            else
            {
                Notify("⚠️ Enter the folder first.", 1);
            }

        }

        private void DragPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}
