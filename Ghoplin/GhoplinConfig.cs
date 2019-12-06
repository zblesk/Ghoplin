using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ghoplin
{

    public class GhoplinConfig
    {
        public List<BlogConfig> Blogs { get; set; } = new List<BlogConfig>();
        public DateTime LastRun { get; set; } = Helpers.Epoch;
    }
}