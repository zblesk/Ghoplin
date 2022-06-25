using System;
using System.Collections.Generic;

namespace Ghoplin;

public class LevelConfig
{
    public LevelConfig(string notebookId)
    {
        NotebookId = notebookId;
    }

    public int LatestIssue { get; set; } = 1;
    public DateTime LastFetch { get; set; }
    public string? LastFetched { get; set; }
    public string NotebookId { get; set; }
    public int NotesTotal { get; set; }
    public bool Disabled { get; set; }
}
