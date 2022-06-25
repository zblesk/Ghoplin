using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Serilog;
using zblesk.Joplin;
using JNote = zblesk.Joplin.Note;

namespace Ghoplin;

internal class LevelService
{
    private readonly JoplinApi _joplin;
    private readonly LevelConfig _config;

    public LevelService(JoplinApi joplin, LevelConfig config)
    {
        _joplin = joplin;
        _config = config;
    }

    public async Task<int> Sync()
    {
        int totalNewNotes = 0;
        var nextIssue = _config.LatestIssue;
        Log.Information("Processing Level");
        while (true)
            try
            {
                nextIssue++;
                var issue = await FetchIssue(nextIssue);
                Log.Information("Added {name}", issue.title);
                totalNewNotes++;
                _config.LastFetched = $"[{issue.title}](:/{issue.id})";
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Log.Warning("Level {issue} not found", nextIssue);
                break;
            }
        _config.LastFetch = DateTime.Now;
        _config.LatestIssue = nextIssue - 1;
        _config.NotesTotal = totalNewNotes;
        return totalNewNotes;
    }

    public async Task<JNote> FetchIssue(int issue)
    {
        Log.Information("Fetching Level {issue}", issue);
        using var client = new HttpClient();
        var url = $"http://www.level.cz/starsi-cisla/level-{issue}/";
        string downloadString = await client.GetStringAsync(url);
        var html = new HtmlDocument();
        html.LoadHtml(downloadString);

        var document = html.DocumentNode;
        var body = document.QuerySelector(".theme-info > div:nth-child(1)").InnerHtml;
        var fuj = "send@send.cz</a></em></p>";
        var fuj2 = "send@send.cz)</a></em></p>";
        var pos = body.IndexOf(fuj);
        if (pos > 0)
        {
            body = body.Remove(0, pos + fuj.Length);
        }
        var pos2 = body.IndexOf(fuj2);
        if (pos2 > 0)
        {
            body = body.Remove(0, pos2 + fuj2.Length);
        }
        var note = body
            .Replace("<strong>", "").Replace("</strong>", "")
            .Replace("<h3>OBSAH", "<h1>OBSAH")
            .Replace("h3>", "h2>")
            ;
        var joplinNote = await _joplin.Add(new JNote
        {
            title = $"Level {issue}",
            body_html = note,
            is_todo = 1,
            parent_id = _config.NotebookId
        });
        var fixedBody = joplinNote!.body!.Replace("####", "#").Replace("##", "#");
        var pos3 = fixedBody.IndexOf("REDAKCE") + 8;
        var toFix = fixedBody[pos3..];
        var replaced = Regex.Replace(toFix,
            @"(^\p{L}.*)",
            "## $1",
            RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.CultureInvariant);
        joplinNote.body = fixedBody[..pos3] + replaced;
        await _joplin.Update(joplinNote);
        return joplinNote;
    }
}
