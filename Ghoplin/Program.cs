using System;
using System.Collections.Generic;

namespace Ghoplin
{
    public class Config
    {
        public Dictionary<string, BlogConfig> Blogs { get; set; }
    }

    public class BlogConfig
    {
        public string ApiKey { get; set; }
        public string BlogUrl { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
        }
    }
}
