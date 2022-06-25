using System;
using System.Collections.Generic;

namespace Ghoplin;

public class Note
{
    public string? Content { get; set; }
    public IList<string> Tags { get; set; } = new List<string>();
    public DateTime? Timestamp { get; set; }
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Id { get; internal set; }
}
