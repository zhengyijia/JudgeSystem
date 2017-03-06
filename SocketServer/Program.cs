using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SocketServer
{
    class Program
    {

        public class HttpProcessor
        {
            public TcpClient socket;
            public HttpServer srv;

            private Stream inputStream;
            public StreamWriter outputStream;

            public String http_method;
            public String http_url;
            public String http_protocol_versionstring;
            public Hashtable httpHeaders = new Hashtable();


            private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

            public HttpProcessor(TcpClient s, HttpServer srv)
            {
                this.socket = s;
                this.srv = srv;
            }


            private string streamReadLine(Stream inputStream)
            {
                int next_char;
                string data = "";
                while (true)
                {
                    next_char = inputStream.ReadByte();
                    if (next_char == '\n') { break; }
                    if (next_char == '\r') { continue; }
                    if (next_char == -1) { Thread.Sleep(1); continue; };
                    data += Convert.ToChar(next_char);
                }
                return data;
            }
            public void process()
            {
                // we can't use a StreamReader for input, because it buffers up extra data on us inside it's
                // "processed" view of the world, and we want the data raw after the headers
                inputStream = new BufferedStream(socket.GetStream());

                // we probably shouldn't be using a streamwriter for all output from handlers either
                outputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
                try
                {
                    parseRequest();
                    readHeaders();
                    if (http_method.Equals("GET"))
                    {
                        handleGETRequest();
                    }
                    else if (http_method.Equals("POST"))
                    {
                        handlePOSTRequest();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: " + e.ToString());
                    writeFailure();
                }
                outputStream.Flush();
                // bs.Flush(); // flush any remaining output
                inputStream = null; outputStream = null; // bs = null;            
                socket.Close();
            }

            public void parseRequest()
            {
                String request = streamReadLine(inputStream);
                string[] tokens = request.Split(' ');
                if (tokens.Length != 3)
                {
                    throw new Exception("invalid http request line");
                }
                http_method = tokens[0].ToUpper();
                http_url = tokens[1];
                http_protocol_versionstring = tokens[2];

                Console.WriteLine("starting: " + request);
            }

            public void readHeaders()
            {
                Console.WriteLine("readHeaders()");
                String line;
                while ((line = streamReadLine(inputStream)) != null)
                {
                    if (line.Equals(""))
                    {
                        Console.WriteLine("got headers");
                        return;
                    }

                    int separator = line.IndexOf(':');
                    if (separator == -1)
                    {
                        throw new Exception("invalid http header line: " + line);
                    }
                    String name = line.Substring(0, separator);
                    int pos = separator + 1;
                    while ((pos < line.Length) && (line[pos] == ' '))
                    {
                        pos++; // strip any spaces
                    }

                    string value = line.Substring(pos, line.Length - pos);
                    Console.WriteLine("header: {0}:{1}", name, value);
                    httpHeaders[name] = value;
                }
            }

            public void handleGETRequest()
            {
                srv.handleGETRequest(this);
            }

            private const int BUF_SIZE = 4096;
            public void handlePOSTRequest()
            {
                // this post data processing just reads everything into a memory stream.
                // this is fine for smallish things, but for large stuff we should really
                // hand an input stream to the request processor. However, the input stream 
                // we hand him needs to let him see the "end of the stream" at this content 
                // length, because otherwise he won't know when he's seen it all! 

                Console.WriteLine("get post data start");
                int content_len = 0;
                MemoryStream ms = new MemoryStream();
                if (this.httpHeaders.ContainsKey("Content-Length"))
                {
                    content_len = Convert.ToInt32(this.httpHeaders["Content-Length"]);
                    if (content_len > MAX_POST_SIZE)
                    {
                        throw new Exception(
                            String.Format("POST Content-Length({0}) too big for this simple server",
                              content_len));
                    }
                    byte[] buf = new byte[BUF_SIZE];
                    int to_read = content_len;
                    while (to_read > 0)
                    {
                        Console.WriteLine("starting Read, to_read={0}", to_read);

                        int numread = this.inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                        Console.WriteLine("read finished, numread={0}", numread);
                        if (numread == 0)
                        {
                            if (to_read == 0)
                            {
                                break;
                            }
                            else {
                                throw new Exception("client disconnected during post");
                            }
                        }
                        to_read -= numread;
                        ms.Write(buf, 0, numread);
                    }
                    ms.Seek(0, SeekOrigin.Begin);
                }
                Console.WriteLine("get post data end");
                srv.handlePOSTRequest(this, new StreamReader(ms));

            }

            public void writeSuccess(string content_type = "text/html")
            {
                outputStream.WriteLine("HTTP/1.0 200 OK");
                outputStream.WriteLine("Content-Type: " + content_type);
                outputStream.WriteLine("Connection: close");
                outputStream.WriteLine("");
            }

            public void writeFailure()
            {
                outputStream.WriteLine("HTTP/1.0 404 File not found");
                outputStream.WriteLine("Connection: close");
                outputStream.WriteLine("");
            }
        }


        public class MyHttpServer : HttpServer
        {
            public MyHttpServer(int port)
                : base(port)
            {
            }
            public override void handleGETRequest(HttpProcessor p)
            {
                Console.WriteLine("request: {0}", p.http_url);
                p.writeSuccess();
                p.outputStream.WriteLine("<!DOCTYPE html><html lang=\"en\"><html lang=\"en\"><head><meta charset=\"UTF-8\">");
                p.outputStream.WriteLine("<title>测试服务器</title></head><body>");
                p.outputStream.WriteLine("<div style=\"height: 20px; color: blue; text-align:center; \">Hello world</div>");
                p.outputStream.WriteLine("</body></html>");
            }

            public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
            {
                Console.WriteLine("POST request: {0}", p.http_url);
                string data = inputData.ReadToEnd();
                p.writeSuccess();
                
                string[] getData = mySplit(data);   // [0] for input; [1] for output; [2] for code;
                for(int i = 0; i < getData.Length; i++)
                {
                    getData[i] = preProcessing(getData[i]);
                }

                string result = myCompile(getData[0], getData[1], getData[2]);

                p.outputStream.Write(result);
            }

            // 去除自定义转义字符前面添加的'/'
            private string preProcessing(string data)
            {

                string return_data = data;
                for(int i = 0; i < return_data.Length; i++)
                {
                    if('\\' == return_data[i])
                    {
                        return_data = return_data.Remove(i, 1);
                    }
                }
                return return_data;
            }

            // 针对含转义字符的字符串分割函数，以非转义的'#'符切割
            // 返回切割后的字符串数组
            private string[] mySplit(string data)
            {
                string[] return_data = new string[3];
                int return_data_index = 0;
                int start_index = 0;
                for(int i = 0; i < data.Length; i++)
                {
                    if('\\' == data[i])
                    {
                        i++;
                        continue;
                    }else{
                        if('#' == data[i])
                        {
                            return_data[return_data_index] = data.Substring(start_index, i - start_index);
                            return_data_index++;
                            start_index = i + 1;
                            if (return_data_index > 1)
                            {
                                return_data[return_data_index] = data.Substring(start_index);
                                return return_data;
                            }
                        }
                    }
                }

                return return_data;
            }

            private string myCompile(string input, string output, string code)
            {
                string result = null;
                string current_thread_id = Thread.CurrentThread.ManagedThreadId.ToString();

                FileStream fs = new FileStream(current_thread_id + ".c", FileMode.Create);
                StreamWriter sw = new StreamWriter(fs);
                sw.Write(code);
                sw.Close();
                fs.Close();

                if (File.Exists(current_thread_id + ".exe"))
                {
                    File.Delete(current_thread_id + ".exe");
                }

                int exitcode;
                Process compileProcess = new Process();
                compileProcess.StartInfo.FileName = "gcc.exe";
                compileProcess.StartInfo.Arguments = current_thread_id + ".c -o " + current_thread_id + ".exe";
                compileProcess.StartInfo.UseShellExecute = false;
                compileProcess.StartInfo.RedirectStandardInput = true;
                compileProcess.StartInfo.RedirectStandardOutput = true;
                compileProcess.StartInfo.RedirectStandardError = true;
                compileProcess.StartInfo.CreateNoWindow = true;
                compileProcess.Start();
                string res = compileProcess.StandardOutput.ReadToEnd();
                exitcode = compileProcess.ExitCode;
                compileProcess.WaitForExit();
                compileProcess.Close();
                if (0 != exitcode)
                {
                    result = "Compilation Error";
                }
                else
                {
                    Process exeProcess = new Process();
                    exeProcess.StartInfo.FileName = current_thread_id + ".exe";
                    exeProcess.StartInfo.UseShellExecute = false;
                    exeProcess.StartInfo.RedirectStandardInput = true;
                    exeProcess.StartInfo.RedirectStandardOutput = true;
                    exeProcess.StartInfo.RedirectStandardError = true;
                    exeProcess.StartInfo.CreateNoWindow = true;
                    exeProcess.Start();
                    exeProcess.StandardInput.WriteLine(input);
                    exeProcess.StandardInput.AutoFlush = true;
                    res = exeProcess.StandardOutput.ReadToEnd();
                    exeProcess.WaitForExit();
                    exeProcess.Close();

                    Console.WriteLine("Actual output: " + res);
                    if (res.Equals(output))
                    {
                        result = "Accept";
                    }
                    else
                    {
                        result = "Wrong Answer";
                    }
                }

                if (File.Exists(current_thread_id + ".c"))
                {
                    File.Delete(current_thread_id + ".c");
                }

                if (File.Exists(current_thread_id + ".exe"))
                {
                    File.Delete(current_thread_id + ".exe");
                }

                return result;
            }
        }


        public abstract class HttpServer
        {

            protected int port;
            TcpListener listener;
            bool is_active = true;

            public HttpServer(int port)
            {
                this.port = port;
            }

            public void listen()
            {
                listener = new TcpListener(port);
                listener.Start();
                while (is_active)
                {
                    TcpClient s = listener.AcceptTcpClient();
                    HttpProcessor processor = new HttpProcessor(s, this);
                    Thread thread = new Thread(new ThreadStart(processor.process));
                    thread.Start();
                    Thread.Sleep(1);
                }
            }

            public abstract void handleGETRequest(HttpProcessor p);
            public abstract void handlePOSTRequest(HttpProcessor p, StreamReader inputData);
        }

        static void Main(string[] args)
        {
            HttpServer httpServer = new MyHttpServer(8080);
            Thread thread = new Thread(new ThreadStart(httpServer.listen));
            thread.Start();
        }
    }
}

