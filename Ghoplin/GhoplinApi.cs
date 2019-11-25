using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ghoplin
{
    public class BlogConfig
    {
        public string ApiKey { get; set; }
        public List<string> AutoTags { get; set; } = new List<string>();
        public string BlogUrl { get; set; }
        public DateTime LastFetch { get; set; }
        public string NotebookId { get; set; }
        public int NotesTotal { get; set; }
        public string Title { get; set; }
    }

    public class GhoplinConfig
    {
        public List<BlogConfig> Blogs { get; set; } = new List<BlogConfig>();
        public DateTime LastRun { get; set; } = Helpers.Epoch;
    }

    public class Note
    {
        public string Content { get; set; }
        public IList<string> Tags { get; set; }
        public DateTime? Timestamp { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string Id { get; internal set; }
    }

    public class Tag
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }

    internal class GhoplinApi
    {
        public static async Task Sync(string apiUrl, string token)
        {
            Log.Debug("Starting sync");
            var joplin = new JoplinService(apiUrl, token);
            var ghost = new GhostService();
            var config = await joplin.GetConfigNote();
            if (config == null)
            {
                config = await joplin.CreateConfigNote();
            }
            var tags = await joplin.LoadTags();
            config.LastRun = DateTime.UtcNow;
            var totalNewNotes = 0;
            foreach (var blog in config.Blogs)
            {
                try
                {
                    totalNewNotes += await ProcessSingleBlog(joplin, ghost, blog, tags);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error while updating {blogUrl}", blog.BlogUrl);
                }
            }
            Log.Debug("Done. Added {newNotes} new notes.", totalNewNotes);
            await joplin.UpdateConfigNote(config);
        }

        private static async Task AddNoteTags(JoplinService joplin, BlogConfig blogConfig, List<Tag> tags, Note note)
        {
            foreach (var noteTag in note.Tags.Union(blogConfig.AutoTags).Distinct())
            {
                var tag = tags.FirstOrDefault(t => t.Title == noteTag);
                if (tag == null)
                {
                    Log.Debug("Creating tag {tagName}", noteTag);
                    tag = await joplin.CreateTag(noteTag);
                    tags.Add(tag);
                }
                Log.Verbose("Assigning tag {tagName}", noteTag);
                await joplin.AssignTag(tag, note);
            }
        }

        private static async Task<int> ProcessSingleBlog(JoplinService joplin, GhostService ghost, BlogConfig blogConfig, List<Tag> tags)
        {
            Log.Debug("Processing blog {blogUrl}", blogConfig.BlogUrl);
            var now = DateTime.UtcNow;
            int newPosts = 0;
            foreach (var note in await ghost.LoadBlogPostsSince(blogConfig.BlogUrl, blogConfig.ApiKey, blogConfig.LastFetch))
            {
                try
                {
                    Log.Information("Adding note {noteName}", note.Title);
                    var noteId = await joplin.CreateNote(blogConfig.NotebookId, note);
                    Log.Debug("Created note with ID {noteId}", noteId);
                    newPosts++;
                    await Task.Delay(900);

                    await AddNoteTags(joplin, blogConfig, tags, note);
                    blogConfig.LastFetch = now;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while doing {name}", note.Title);
                    throw;
                }
            }
            return newPosts;
        }
    }
}