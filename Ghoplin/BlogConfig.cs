using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ghoplin
{
    public class BlogConfig
    {
        public string Title { get; set; }
        public string BlogUrl { get; set; }
        public string ApiKey { get; set; }
        public List<string> AutoTags { get; set; }
        public DateTime LastFetch { get; set; }
        public string LastFetchedPost { get; set; }
        public string NotebookId { get; set; }
        public int NotesTotal { get; set; }
        public bool Disabled { get; set; } 
    }
}