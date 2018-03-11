using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace WebProxy
{
    class ProxyOptions
    {
        public enum DebugLevel
        {
            None = 1,
            Debug = 2,
            Verbose = 3
        }

        //Current debug level of the proxy
        private DebugLevel print;
        //Path to the filter
        public string filterPath = "filter.txt";
        //Thread-safe dictionary object representing the filter
        public ConcurrentDictionary<string, byte> filter = new ConcurrentDictionary<string, byte>();

        public ProxyOptions()
        {
            print = DebugLevel.None;
        }

        public void SetDebugLevel(DebugLevel val)
        {
            print = val;
        }

        public DebugLevel GetDebugLevel()
        {
            return print;
        }

        public bool Filtered(string url)
        {
            return filter.ContainsKey(url);
        }

        public bool AddFilter(string url)
        {
            return filter.TryAdd(url, 0);
        }

        public bool RemoveFilter(string url)
        {
            return filter.TryRemove(url, out _);
        }

        //Concurrent Dictionary is not serializable, flush to disk 
        public void FlushFilter()
        {
            try
            {
                Stream ms = File.OpenWrite(filterPath);
                StreamWriter file = new StreamWriter(ms);
                foreach(KeyValuePair<string, byte> key in filter)
                {
                    file.WriteLine(key.Key);
                }
                ms.Flush();
                ms.Close();
                ms.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        //Load file into filter ConcurrentDictionary
        public void LoadFilter()
        {
            try
            {
                Stream fs = File.OpenRead(filterPath);
                StreamReader file = new StreamReader(fs);
                while (!file.EndOfStream)
                {
                    filter.TryAdd(file.ReadLine(),0);
                }
                fs.Flush();
                fs.Close();
                fs.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }


    }
}
