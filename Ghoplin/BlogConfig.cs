using System;
using System.Collections.Generic;

namespace Ghoplin;

public class BlogConfig
{
    public BlogConfig(string blogUrl, string apiKey, string notebookId)
    {
        BlogUrl = blogUrl;
        ApiKey = apiKey;
        NotebookId = notebookId;
    }

    public string? Title { get; set; }
    public string BlogUrl { get; set; }
    public string ApiKey { get; set; }
    public List<string> AutoTags { get; set; } = new List<string>();
    public DateTime LastFetch { get; set; }
    public string? LastFetchedPost { get; set; }
    public string NotebookId { get; set; }
    public int NotesTotal { get; set; }
    public bool Disabled { get; set; }
}
