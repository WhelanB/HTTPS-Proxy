using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebProxy
{
    class Program
    {
        //+:2000 will listen on localhost + device's local IP on port 2000
        static ProxyConsole console = new ProxyConsole("http://+:2000/");
        //Listen on device local IP address port 80
        static Proxy proxy = new Proxy(IPAddress.Parse(GetLocalIPAddress()), 80, console);
        const int bufferSize = 4096;
        static List<String> logs = new List<string>();

        static void Main(string[] args)
        {
            //Start the Proxy
            proxy.Start().Wait();
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("Could not get local IP");
        }


    }
}
