using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Serilog;
using zblesk.Helpers;
using zblesk.Joplin;
using JNote = zblesk.Joplin.Note;

namespace Ghoplin;

public class LevelService
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

    public async Task<JNote> FetchIssue(int level)
    {
        Log.Information("Fetching Level {issue}", level);

        using var client = new HttpClient();
        var url = $"https://www.level.cz/level/level-{level}/";

        string downloadString = await client.GetStringAsync(url);

        var html = new HtmlDocument();
        html.LoadHtml(downloadString);

        var document = html.DocumentNode;
        var vydanie = document.QuerySelector(".push-left").Attributes["data-date"].Value;
        vydanie = vydanie
            .ToLower()
            .Replace("leden", "Január")
            .Replace("únor", "Február")
            .Replace("březen", "Marec")
            .Replace("duben", "Apríl")
            .Replace("květen", "Máj")
            .Replace("červen", "Jún")
            .Replace("červenec", "Júl")
            .Replace("srpen", "August")
            .Replace("září", "September")
            .Replace("říjen", "Október")
            .Replace("listopad", "November")
            .Replace("prosinec", "December");

        var artikel = document.QuerySelector("#post-content-article")?.InnerText.Trim();
        artikel = Regex.Replace(artikel, @"POZOR! POKUD SI.+ ZDARMA!\)", "")
            .Replace(new string(new char[] { '\uFEFF' }), "");
        artikel = Regex.Replace(artikel, @"Starší čísla .+send.cz", "");

        var obrazok = document.QuerySelector(".attachment-post-thumb").OuterHtml;

        var noteText = new StringBuilder($@"
{obrazok}

{artikel}


{vydanie}");
        var table = document.QuerySelectorAll("#post-content-index .tableview-row");

        noteText.AppendLine();
        foreach (var row in table)
        {
            var nadpisSekcie = row.QuerySelector("h3").InnerText;

            noteText.AppendLine($"<h1>{nadpisSekcie}</h1>");
            foreach (var clanok in row.QuerySelectorAll("ul"))
            {
                var info =
                    clanok.QuerySelectorAll("li")
                    .Select(li =>
                        li.InnerText
                        .Trim()
                        .Replace("\t", "")
                        .Replace("- ", " - ")
                    ).Take(2).ToList();
                var nadpis = info?[0];
                var autor = info?[1];
                noteText.Append($"<h2>{nadpis}</h2>");
                if (!string.IsNullOrWhiteSpace(autor))
                    noteText.AppendLine($"<i>Autor: {autor}</i>");
            }
        }

        var note = noteText.ToString().Trim();
        note = note.Replace("\n", "<br />\n");

        var n = (new JNote
        {
            title = $"Level {level}",
            body_html = note,
            is_todo = 1,
            parent_id = _config.NotebookId
        });
        var joplinNote = await _joplin.Add(n);
        joplinNote.body = Regex.Replace(joplinNote!.body!, @"## (.+)\n+\*Autor:", "## $1\n*Autor:",
            RegexOptions.IgnoreCase
            | RegexOptions.Multiline
            | RegexOptions.CultureInvariant);
        await _joplin.Update(joplinNote);
        return joplinNote;
    }
}
