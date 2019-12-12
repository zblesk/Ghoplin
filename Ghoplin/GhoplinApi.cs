using Flurl.Http;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ghoplin
{
    internal class GhoplinApi
    {
        private readonly string _token;
        private readonly string _apiUrl;
        private readonly JoplinService _joplin;
        private readonly GhostService _ghost;

        public GhoplinApi(string apiUrl, string token)
        {
            _token = token;
            _apiUrl = apiUrl;
            _joplin = new JoplinService(_apiUrl, _token);
            _ghost = new GhostService();
        }

        public async Task Sync()
        {
            Log.Debug("Starting Sync");
            var config = await LoadConfig();
            var tags = await _joplin.LoadTags();
            var totalNewNotes = 0;
            foreach (var blog in config.Blogs.Where(blog => !blog.Disabled))
            {
                try
                {
                    totalNewNotes += await ProcessSingleBlog(_joplin, _ghost, blog, tags);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error while updating {blogUrl}", blog.BlogUrl);
                }
            }
            Log.Debug("Done. Added {newNotes} new notes.", totalNewNotes);
            await _joplin.UpdateConfigNote(config);
        }

        public async Task AddBlog(string apiKey, string blogUrl, string notebookId, params string[] autoTags)
        {
            if (string.IsNullOrWhiteSpace(notebookId))
            {
                throw new ArgumentNullException(nameof(notebookId));
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }
            if (string.IsNullOrWhiteSpace(blogUrl))
            {
                throw new ArgumentNullException(nameof(blogUrl));
            }

            Log.Debug("Starting AddBlog");
            var config = await LoadConfig();
            var notebook = await GetNotebookByIdOrTitle(notebookId);

            Log.Debug("Found notebook '{notebookTitle}' ({notebookId})", notebook.Title, notebook.Id);
            Log.Debug("Attempting to contact blog");
            var title = await _ghost.LoadBlogTitle(blogUrl, apiKey);
            Log.Information("Successfully connected to blog {blogTitle} at {blogUrl}", title, blogUrl);

            var newBlog = new BlogConfig
            {
                ApiKey = apiKey,
                BlogUrl = blogUrl,
                NotebookId = notebook.Id,
                AutoTags = autoTags == null
                    ? new List<string>()
                    : autoTags.ToList(),
                Title = title,
            };
            config.Blogs.Add(newBlog);

            Log.Information("Successfully added blog {blogUrl}", title);
            await _joplin.UpdateConfigNote(config);
            Log.Debug("Updated config in Joplin");
        }

        private async Task<Notebook> GetNotebookByIdOrTitle(string notebookId)
        {
            Notebook notebook;
            try
            {
                Log.Debug("Assuming ID. Trying to get Notebook with ID {notebookId}", notebookId);
                notebook = await _joplin.GetNotebookById(notebookId);
            }
            catch (FlurlHttpException ex) when (ex.Message.Contains("404"))
            {
                Log.Debug("No Notebook with ID {notebookId}. Trying fetch by name.", notebookId);
                var notebooks = await _joplin.GetNotebooks();
                notebook = notebooks.FirstOrDefault(nb => nb.Title == notebookId);
            }
            if (notebook == null)
            {
                throw new GhoplinException($"Couldn't find a notebook with Title or ID '{notebookId}'");
            }

            return notebook;
        }

        private async Task<GhoplinConfig> LoadConfig() => await _joplin.GetConfigNote() ?? await _joplin.CreateConfigNote();

        private async Task AddNoteTags(JoplinService joplin, BlogConfig blogConfig, List<Tag> tags, Note note)
        {
            foreach (var noteTag in note.Tags.Union(blogConfig.AutoTags).Distinct())
            {
                var tag = tags.Find(t => t.Title == noteTag.ToLower());
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

        private async Task<int> ProcessSingleBlog(JoplinService joplin, GhostService ghost, BlogConfig blogConfig, List<Tag> allTags)
        {
            Log.Information("Processing blog {blogTitle} @ {blogUrl}", blogConfig.Title, blogConfig.BlogUrl);
            var now = DateTime.UtcNow;
            int newPosts = 0;
            foreach (var note in await ghost.LoadBlogPostsSince(blogConfig.BlogUrl, blogConfig.ApiKey, blogConfig.LastFetch))
            {
                try
                {
                    Log.Information("Adding note {noteName}", note.Title);
                    var noteId = await joplin.CreateNote(blogConfig.NotebookId, note);
                    await Task.Delay(1900);
                    Log.Debug("Created note with ID {noteId}", noteId);
                    newPosts++;

                    await AddNoteTags(joplin, blogConfig, allTags, note);

                    blogConfig.LastFetch = note.Timestamp ?? now;
                    blogConfig.LastFetchedPost = $"[{note.Title}](:/{note.Id})";
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while processing note {noteTitle}", note.Title);
                    continue;
                }
            }
            blogConfig.LastFetch = now;
            blogConfig.NotesTotal += newPosts;
            return newPosts;
        }
    }
}