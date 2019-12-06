using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ghoplin
{

    public class Note
    {
        public string Content { get; set; }
        public IList<string> Tags { get; set; }
        public DateTime? Timestamp { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string Id { get; internal set; }
    }
}