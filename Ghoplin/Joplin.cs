using Flurl;
using Flurl.Http;
using Newtonsoft.Json;
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

    public class JoplinNote
    {
        public string Content { get; set; }
        public IList<string> Tags { get; set; }
        public DateTime? Timestamp { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
    }

    public class Tag
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }

    internal class Joplin
    {
        public Joplin(string apiUrl, string token)
        {
            _apiUrl = apiUrl ?? throw new ArgumentNullException(nameof(apiUrl));
            _token = token ?? throw new ArgumentNullException(nameof(token));
        }

        public static async Task Sync(string apiUrl, string token)
        {
            var joplin = new Joplin(apiUrl, token);
            await joplin.Do();
        }

        private const string ConfigNoteId = "00000000000031337000000000000001";

        private readonly string _apiUrl;
        private readonly string _token;
        private GhostService _ghost;
        private List<Tag> _tags;

        private async Task AddNoteTags(BlogConfig blogConfig, JoplinNote post, string noteId)
        {
            foreach (var tag in post.Tags.Union(blogConfig.AutoTags).Distinct())
            {
                var tagId = "";
                if (_tags.Any(f => f.Title == tag))
                {
                    tagId = _tags.First(t => t.Title == tag).Id;
                }
                else
                {
                    dynamic newTag = await _apiUrl
                       .AppendPathSegment("tags")
                       .SetQueryParam("token", _token)
                       .PostJsonAsync(new
                       {
                           title = tag,
                       })
                       .ReceiveJson();
                    tagId = newTag.id.ToString() as string;
                    _tags.Add(new Tag { Id = tagId, Title = tag });
                }
                await _apiUrl
                    .AppendPathSegments("tags", tagId, "notes")
                    .SetQueryParam("token", _token)
                    .PostJsonAsync(new
                    {
                        id = noteId
                    });
            }
        }

        private async Task<GhoplinConfig> CreateConfigNote()
        {
            await _apiUrl
                .AppendPathSegments("notes")
                .SetQueryParam("token", _token)
                .PostJsonAsync(new
                {
                    title = "Ghoplin Config",
                    body = "",
                    id = ConfigNoteId,
                    source_application = "net.zblesk.ghoplin"
                })
                .ReceiveJson();
            return new GhoplinConfig();
        }

        private async Task Do()
        {
            var config = await GetConfigNote();
            if (config == null)
            {
                config = await CreateConfigNote();
            }
            await LoadTags();
            _ghost = new GhostService();
            config.LastRun = DateTime.UtcNow;

            foreach (var blog in config.Blogs)
            {
                await ProcessSingleBlog(blog).ConfigureAwait(false);
            }
            await UpdateConfigNote(config).ConfigureAwait(false);
        }

        private async Task<GhoplinConfig> GetConfigNote()
        {
            var configNote = await _apiUrl
                .AppendPathSegments("notes", ConfigNoteId)
                .SetQueryParam("token", _token)
                .SetQueryParam("fields", "body")
                .GetJsonAsync();
            return JsonConvert.DeserializeObject<GhoplinConfig>(configNote.body);
        }

        private async Task LoadTags()
        {
            var allTags = await _apiUrl
                            .AppendPathSegment("tags")
                            .SetQueryParam("token", _token)
                            .GetStringAsync();
            _tags = JsonConvert.DeserializeObject<List<Tag>>(allTags);
        }

        private async Task ProcessSingleBlog(BlogConfig blogConfig)
        {
            var now = DateTime.UtcNow;
            foreach (var post in await _ghost.LoadBlogPostsSince(blogConfig.BlogUrl, blogConfig.ApiKey, blogConfig.LastFetch))
            {
                Console.WriteLine(post.Title);
                try
                {
                    dynamic res = await _apiUrl
                        .AppendPathSegment("notes")
                        .SetQueryParam("token", _token)
                        .PostJsonAsync(new
                        {
                            parent_id = blogConfig.NotebookId,
                            title = post.Title,
                            body_html = post.Content,
                            source_url = post.Url,
                            base_url = new Uri(post.Url).GetLeftPart(UriPartial.Path),
                            user_created_time = post.Timestamp?.ToUnixTimestampMiliseconds().ToString(),
                            user_updated_time = post.Timestamp?.ToUnixTimestampMiliseconds().ToString(),
                        })
                        .ReceiveJson().ConfigureAwait(false);
                    var noteId = res.id.ToString() as string;
                    await Task.Delay(900);

                    await AddNoteTags(blogConfig, post, noteId);
                    blogConfig.LastFetch = now;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while doing {name}", post.Title);
                }
            }
        }

        private async Task UpdateConfigNote(GhoplinConfig ghostConfig)
        {
            await _apiUrl
                .AppendPathSegments("notes", ConfigNoteId)
                .SetQueryParam("token", _token)
                .PutJsonAsync(new
                {
                    body = JsonConvert.SerializeObject(ghostConfig, Formatting.Indented)
                })
                .ReceiveJson();
        }
    }
}
