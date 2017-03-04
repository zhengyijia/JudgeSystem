using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

namespace JudgeSystem
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Submit_click(object sender, RoutedEventArgs e)
        {
            // string s = HttpGet("http://localhost:8080/");
            // string s = HttpGet("http://172.26.1.56:8080");
            // return_message.Text = s;

            this.result.Text = "Running";
            new Thread(delegate ()
            {
                this.Dispatcher.Invoke(new Action(delegate ()
                {
                    string ip = this.ip.Text.ToString();
                    string port = this.port.Text.ToString();
                    string input = this.input_data.Text.ToString();
                    string output = this.output_data.Text.ToString();
                    string code = this.code.Text.ToString();

                    string url = "http://" + ip + ":" + port;
                    input = preProcessing(input);
                    output = preProcessing(output);
                    code = preProcessing(code);
                    string data = input + '#' + output + '#' + code;

                    this.result.Text = HttpPost(url, data, "utf-8");
                }));
            }).Start();
        }

        // 在字符串中原有'#'字符或'\'字符之前加上转义符'\'
        private string preProcessing(string data)
        {
            string return_data = data;
            for(int i = 0; i < return_data.Length; i++)
            {
                if('#' == return_data[i] || '\\' == return_data[i])
                {
                    return_data = return_data.Substring(0, i) + '\\' + return_data.Substring(i);
                    i++;
                }
            }

            return return_data;
        }

        public static string HttpPost(string url, string postData, string encodeType)
        {
            string result = null;
            try
            {
                Encoding encoding = Encoding.GetEncoding(encodeType);
                byte[] POST = encoding.GetBytes(postData);
                HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(url);
                myRequest.KeepAlive = false;
                myRequest.AllowAutoRedirect = true;
                myRequest.CookieContainer = new System.Net.CookieContainer();
                ServicePointManager.Expect100Continue = false;
                ServicePointManager.MaxServicePointIdleTime = 2000;
                myRequest.Method = "POST";
                myRequest.ContentType = "application/x-www-form-urlencoded";
                myRequest.ContentLength = POST.Length;
                Stream newStream = myRequest.GetRequestStream();
                newStream.Write(POST, 0, POST.Length);
                newStream.Close();
                HttpWebResponse myResponse = (HttpWebResponse)myRequest.GetResponse();
                StreamReader reader = new StreamReader(myResponse.GetResponseStream(), Encoding.Default);
                result = reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return result;
        }

        public string HttpGet(string Url)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);

                request.Method = "GET";
                request.ContentType = "text/html;charset=UTF-8";

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream myResponseStream = response.GetResponseStream();
                StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
                string returnStr = myStreamReader.ReadToEnd();
                myStreamReader.Close();
                myResponseStream.Close();

                return returnStr;
            }
            catch (Exception e)
            {
                return "Error";
            }

        }
    }
}
