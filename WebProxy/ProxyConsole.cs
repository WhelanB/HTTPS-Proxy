using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebProxy
{
    class ProxyConsole
    {
        static HttpListener server = new HttpListener();
        ConcurrentStack<String> logs;
        public ProxyOptions options;
        HttpListener httpListener;
        public bool showHeaders = false;
        public ProxyConsole(string prefix)
        {
            //Create a ProxyOptions
            options = new ProxyOptions();
            //If a filter list exists on disk, load it into the filter
            options.LoadFilter();
            //logs stack for processing logged messages
            logs = new ConcurrentStack<string>();
            //Begin the page serving server
            server.Prefixes.Add(prefix);
            server.Start();
            server.BeginGetContext(Handle, null);
            //Begin the WebSocket listener
            httpListener = new HttpListener();
            httpListener.Prefixes.Add("http://+:8080/");
            httpListener.Start();
            Write("Web Console Started");
            

        }

        private void ParseCommand(string command, string[] parameters)
        {
            switch (command.ToLower())
            {
                //Add a URL to the filter
                case "add-filter":
                    if (parameters.Length != 1)
                    {
                        Write("Invalid Parameter Count");
                        return;
                    }
                    while (!options.AddFilter(parameters[0])) { }
                    options.FlushFilter();
                    Write(parameters[0] + " added to filter");
                    break;
                //Remove a URL from the filter
                case "remove-filter":
                    if (parameters.Length != 1)
                    {
                        Write("Invalid Parameter Count");
                        return;
                    }
                    while (!options.RemoveFilter(parameters[0])) { }
                    options.FlushFilter();
                    Write(parameters[0] + " removed from filter");
                    break;
                //List the filter as text
                case "list-filter":
                    Write(String.Join("\n", options.filter.Keys));
                    break;
                //Update the logged level of the Proxy {None, Debug, Verbose}
                case "set-log":
                    if (parameters.Length != 1)
                    {
                        Write("Invalid Parameter Count");
                        return;
                    }
                    try
                    {
                        options.SetDebugLevel((ProxyOptions.DebugLevel)Enum.Parse(typeof(ProxyOptions.DebugLevel), parameters[0]));
                    }
                    catch (ArgumentException)
                    {
                        Write(String.Format("{0} is not a valid logging level", parameters[0]));
                    }
                    break;
                //Flush the filter to disk and shut down
                case "stop":
                    Write("Shutting Down");
                    options.FlushFilter();
                    System.Environment.Exit(-1);
                    break;
                default:
                    Write("Command not recognised");
                    break;
            }
        }

        private void Handle(IAsyncResult result)
        {
            var endContext = server.EndGetContext(result);
            server.BeginGetContext(Handle, null);
            HttpListenerResponse response = endContext.Response;
            //If info is accessed, serve the Proxy.html file.
            if (endContext.Request.Url.LocalPath == "/info")
            {
                byte[] buffer = Encoding.UTF8.GetBytes(File.ReadAllText(@"Proxy.html"));
                response.ContentLength64 = buffer.Length;
                Stream st = response.OutputStream;
                st.Write(buffer, 0, buffer.Length);
                endContext.Response.Close();
            }
            //If filter is accessed, serve the filter as a HTML table
            else if (endContext.Request.Url.LocalPath == "/filter")
            {
                String command = endContext.Request.Url.Query.Substring(1).Split("%20")[0];
                if (command.ToLower() == "remove")
                    options.filter.TryRemove(endContext.Request.Url.Query.Substring(1).Split("%20")[1], out _);
                string table = string.Concat(options.filter.Keys.Select(i => string.Format("<tr><td>{0}</td><td><a onclick=\"$.get('/filter?remove%20{0}', function (data) {{ $('.table').html(data); }});\" href=\"javascript: void(0);\">✖</a></td></tr>", i)));
                byte[] buffer = Encoding.UTF8.GetBytes(table);

                response.ContentLength64 = buffer.Length;
                Stream st = response.OutputStream;
                st.Write(buffer, 0, buffer.Length);

                endContext.Response.Close();
            }
            //Otherwise server the management console
            else
            {
                //byte[] buffer = Encoding.UTF8.GetBytes("<head><title>Proxy Console</title><style type=\"text/css\">samp { height:400px;overflow:auto; background: #000; border: 3px groove #ccc; color: #ccc; display: block; padding: 5px; width: 100%; }</style> <script src=\"http://ajax.googleapis.com/ajax/libs/jquery/3.3.1/jquery.min.js\" integrity=\"sha256-FgpCb/KJQlLNfOu91ta32o/NMZxltwRo8QtmkMRdAu8=\" crossorigin=\"*\"></script></head><body style=\"background-color: #f1eded;\"><h1>HTTP Proxy Console</h1><br><p><samp id=\"console\"></samp></p><input id=\"text\" type=\"text\"><input id = \"btnSubmit\" type=\"submit\" value=\"Send\"/><script>$(document).ready(function() { $(\"#btnSubmit\").click(function(){ $.get(\"http://localhost:2000/Command?\" + $(\"#text\").val(), function(data, status){}); }); });window.setInterval(function(){ $.get(\"http://localhost:2000/Log\", function(data, status){ $('#console').html(data); });},2000);</script></body>");
                byte[] buffer = Encoding.UTF8.GetBytes(File.ReadAllText(@"Console.html"));
                response.ContentLength64 = buffer.Length;
                Stream st = response.OutputStream;
                st.Write(buffer, 0, buffer.Length);
                HttpListenerContext context = httpListener.GetContext();
                if (context.Request.IsWebSocketRequest)
                {
                    HttpListenerWebSocketContext webSocketContext = context.AcceptWebSocketAsync(null).Result;
                    WebSocket webSocket = webSocketContext.WebSocket;
                    new Thread(() =>
                    {
                        while (webSocket.State == WebSocketState.Open)
                        {
                            //Pop the logs from the stack and serve to Client via WebSocket
                            string data;
                            while (logs.Count > 0)
                            {
                                while (!logs.TryPop(out data)) { }
                                if (data != null)
                                    webSocket.SendAsync(new ArraySegment<byte>(Encoding.ASCII.GetBytes(data), 0, data.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                            }
                        }
                    }).Start();
                    new Thread(() =>
                    {
                        while (webSocket.State == WebSocketState.Open)
                        {
                            //Block until received, and parse/execute command
                            byte[] receiveBuffer = new byte[4096];
                            WebSocketReceiveResult receiveResult = webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None).Result;
                            string[] request = Encoding.ASCII.GetString(receiveBuffer).Substring(0, receiveResult.Count).Split(" ");
                            Write(request[0]);
                            string command = request[0];
                            string[] parameters = new string[0];
                            if (request.Length > 1)
                                parameters = request.Skip(1).ToArray();
                            ParseCommand(command, parameters);

                        }
                    }).Start();
                }
                endContext.Response.Close();
            }
        }

        //Write to local console and any connected Web-Based consoles
        public void Write(String s)
        {
            string html = s.Replace("\n", "<br>");
            string log = s;
            logs.Push(html);
            Console.WriteLine(log);
        }

        //Write only if the Debug Level is consistent with the current debug level of the proxy
        public void Write(String s, ProxyOptions.DebugLevel level)
        {
            if ((int)level <= (int)options.GetDebugLevel())
                Write(s);
        }
    }
}
