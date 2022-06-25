using System;
using System.Collections.Generic;

namespace Ghoplin;

public class GhoplinConfig
{
    public List<BlogConfig> Blogs { get; set; } = new List<BlogConfig>();
    public DateTime LastRun { get; set; } = Helpers.Epoch;
}
