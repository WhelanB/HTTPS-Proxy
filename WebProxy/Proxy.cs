using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebProxy
{
    class Proxy
    {
        IPAddress ip;
        int port;
        //buffer size for tunnelling
        static int bufferSize = 4096;
        List<String> logs = new List<string>();
        ProxyConsole console;
        public Proxy(IPAddress ipAddress, int port, ProxyConsole cs)
        {
            ip = ipAddress;
            this.port = port;
            console = cs;
        }

        public Proxy(IPAddress ipAddress, int port, int buffer)
        {
            ip = ipAddress;
            this.port = port;
            bufferSize = buffer;
        }

        public async Task Start()
        {
            var server = new TcpListener(ip, port);
            server.Start();
            while (true)
            {
                if (server.Pending())
                {
                    var client = server.AcceptTcpClient();
                    Thread thread = new Thread(() => Process(client));
                    thread.Start();
                }
            }
        }

        public void Process(TcpClient client)
        {
            var clientStream = client.GetStream();
            StreamReader inputStream = new StreamReader(clientStream);
            String request = inputStream.ReadLine();
            if (request == null)
                return;
            int port = 80;
            if (request.Contains("443")) //Determine if request is HTTP/HTTPS
                port = 443;
            //console.Write(request);
            string[] parsedRequest = request.Split(' ');
            string url = parsedRequest[1];
            string headers = "";
            string host = "";
            WebHeaderCollection headerss = new WebHeaderCollection();
            for (string header; (header = inputStream.ReadLine()) != null;)
            {
                if (header.Contains("Host"))
                    host = header.Split(":", 3)[1].Trim(); //Get the Host to connect to
                if (header.Equals("")) //If header reading is complete
                    break;
                headerss.Add(header);
                headers += header + "\r\n"; //HTTP headers lines end with \r\n
            }
            headers += "\r\n"; //HTTP headers are terminated with \r\n\r\n
            console.Write(headers, ProxyOptions.DebugLevel.Debug);
            
            //Check filter for host
            if (console.options.filter.ContainsKey(host))
            {
                console.Write("Blocked user from accessing " + host);
                string connect = "HTTP/1.1 403 Forbidden\r\n\r\n";
                byte[] data = Encoding.ASCII.GetBytes(connect);
                clientStream.Write(data, 0, data.Length);
                clientStream.Flush(); //Return a messsage indicating that the request has been denied (403 forbidden)
                //client.Close();

            }

            url.Trim(); //Trim any excess spaces from the Host and connect
                TcpClient server = new TcpClient();
                try
                {
                    server.Connect(host, port);
                    if (parsedRequest[0] == "CONNECT") //If the HTTP Request is CONNECT, a HTTP request is being created
                    {
                        //Inform the browser that the connection has been established
                        string connect = "HTTP/1.1 200 Connection established\r\n\r\n";
                        byte[] data = Encoding.ASCII.GetBytes(connect);
                        lock (clientStream)
                        {
                            clientStream.Write(data, 0, data.Length);
                            clientStream.Flush(); //Return a messsage indicating that the connection has been established
                        }
                    }
                }
                catch (Exception e)
                {
                    if (parsedRequest[0] == "CONNECT") //If the HTTP Request is CONNECT, we're dealing with HTTPS/WebSocket
                    {
                        string connect = "HTTP/1.1 503 Service Unavailable\r\n\r\n";
                        byte[] data = Encoding.ASCII.GetBytes(connect);
                        lock (clientStream)
                        {
                            clientStream.Write(data, 0, data.Length);
                            clientStream.Flush(); //Return a messsage indicating that the connection has been established (even though it hasn't!)
                        }
                    }
                    console.Write("Failed to connect to Host: " + host);
                    console.Write(e.Message, ProxyOptions.DebugLevel.Verbose);
                    return;
                }
                var serverStream = server.GetStream();
                if (parsedRequest[0] != "CONNECT") //If it's not HTTP Connect, we want to pass the request and headers on to the connected host
                {
                    byte[] fullRequest = Encoding.ASCII.GetBytes(request + "\r\n" + headers);
                    serverStream.Write(fullRequest, 0, fullRequest.Length);
                    serverStream.Flush();
                }
                //Finally, invoke two new threads which tunnel the sockets, one thread handling Client -> Server and one thread handling Server -> Client
                new Thread(() =>
                {
                    byte[] clientData = new byte[bufferSize];
                    int clientDataSize;
                    
                    while (client.Connected && server.Connected)
                    {

                        try
                        {
                            clientDataSize = clientStream.Read(clientData, 0, bufferSize);
                            serverStream.Write(clientData, 0, clientDataSize);
                            serverStream.Flush();
                        }
                        catch
                        {
                            client.Close();
                            server.Close();
                            break;
                        }
                        if (clientDataSize == 0)
                        {
                            client.Close();
                            server.Close();
                            break;
                        }

                    }
                }).Start();
                new Thread(() =>
                {
                    byte[] serverData = new byte[bufferSize];
                    int serverBytes;
                    List<byte> cacheData = new List<byte>();
                    
                    while (client.Connected && server.Connected)
                    {
                        
                        try
                        {
                            serverBytes = serverStream.Read(serverData, 0, bufferSize);
                            clientStream.Write(serverData, 0, serverBytes);
                            clientStream.Flush();
                        }
                        catch
                        {
                            client.Close();
                            server.Close();
                            break;
                        }
                        if (serverBytes == 0)
                        {
                            client.Close();
                            server.Close();
                            break;
                        }
                        
                    }
                    
                }).Start();


        }
    }
}
