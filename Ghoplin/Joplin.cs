using Flurl;
using Flurl.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ghoplin
{
    public class Tag
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }

    public class GhoplinConfig
    {
        public DateTime LastRun { get; set; }
        public List<BlogConfig> Blogs { get; set; } = new List<BlogConfig>();
    }

    public class BlogConfig
    {
        public string ApiKey { get; set; }
        public string BlogUrl { get; set; }
        public List<string> AutoTags { get; set; } = new List<string>();
        public string Title { get; set; }
        public DateTime LastFetch { get; set; }
        public int NotesTotal { get; set; }
        public string NotebookId { get; set; }
    }

    public class JoplinNote
    {
        public int TimelineId { get; set; }
        public DateTime Modified { get; set; }
        public DateTime Created { get; set; }
        public DateTime? EventTime { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string RawEventData { get; set; }
        public string ExternalSystemName { get; set; }
        public string ExternalSystemId { get; set; }
        public string Url { get; set; }
        public string EventType { get; set; }

        public IList<string> Tags { get; set; }
    }

    public class GhostService
    {
        public GhostService()
        {
        }

        public async Task<IList<JoplinNote>> LoadBlogPostsSince(string blogUrl, string apiKey, DateTime since)
        {
            if (string.IsNullOrWhiteSpace(blogUrl)
                || string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("Blog URL and API Key must be set.");
            }
            var url = blogUrl
                .AppendPathSegment("ghost/api/v2/content/posts/")
                .SetQueryParams(
                new
                {
                    include = "tags,authors",
                    limit = "all",
                    key = apiKey,
                    filter = $"updated_at:>='{since.ToString("s")}'",
                });
            var response = await url
                .GetAsync()
                .ReceiveJson()
                .ConfigureAwait(false);
            var results = new List<JoplinNote>();
            foreach (dynamic post in response.posts)
            {
                var timeline = new JoplinNote
                {
                    Title = post.title,
                    Content = post.html,
                    EventTime = post.published_at,
                    ExternalSystemId = post.id,
                    RawEventData = JsonConvert.SerializeObject(post),
                    EventType = "blogpost",
                    Url = post.url,
                };
                results.Add(timeline);
                var tags = new List<string>();
                foreach (dynamic tag in post.tags)
                {
                    tags.Add(tag.name);
                }
                timeline.Tags = tags;
            }
            return results;
        }
    }

    internal static class Joplin
    {
        private const string ConfigNoteId = "00000000000031337000000000000001";

        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static async Task Do(string url, string token)
        {
            //await UpdateConfigNote(url, token, ghostConfig);

            var allTags = await url
                .AppendPathSegment("tags")
                .SetQueryParam("token", token)
                .GetStringAsync();
            var tags = JsonConvert.DeserializeObject<List<Tag>>(allTags);
            var ghost = new GhostService();

            await ProcessSingleBlog(token, tags, ghost);
        }

        private static async Task UpdateConfigNote(string url, string token, GhoplinConfig ghostConfig)
        {
            var update = await url
                .AppendPathSegments("notes", ConfigNoteId)
                .SetQueryParam("token", token)
                .PutJsonAsync(new
                {
                    body = JsonConvert.SerializeObject(ghostConfig, Formatting.Indented)
                })
                .ReceiveJson();
        }

        private static async Task ProcessSingleBlog(string token, List<Tag> tags, GhostService ghost)
        {
            var zbleskEnNotebookId = "123";
            var ghostConfig = new BlogConfig
            {
                BlogUrl = "https://zblesk.net/blog",
                ApiKey = "123"
            };
            await ProcessBlog(ghost, ghostConfig, token, zbleskEnNotebookId, tags);
        }

        public static long ToUnixTimestampMiliseconds(this DateTime date) => (long)(date.Subtract(Epoch)).TotalMilliseconds;

        private static async Task ProcessBlog(GhostService ghost, BlogConfig ghostConfig, string token, string notebookId, List<Tag> tags)
        {
            foreach (var post in await ghost.LoadBlogPostsSince(ghostConfig.BlogUrl, ghostConfig.ApiKey, DateTime.Now.AddYears(-20)))
            {
                Console.WriteLine(post.Title);
                try
                {
                    dynamic res = await "http://localhost:41184/notes"
                        .SetQueryParam("token", token)
                        .PostJsonAsync(new
                        {
                            parent_id = notebookId,
                            title = post.Title,
                            body_html = post.Content,
                            source_url = post.Url,
                            base_url = new Uri(post.Url).GetLeftPart(UriPartial.Path),
                            user_created_time = post.EventTime?.ToUnixTimestampMiliseconds().ToString(),
                            user_updated_time = post.EventTime?.ToUnixTimestampMiliseconds().ToString(),
                        })
                        .ReceiveJson();
                    var noteId = res.id.ToString() as string;
                    await Task.Delay(900);

                    foreach (var tag in post.Tags)
                    {
                        var tagId = "";
                        if (tags.Any(f => f.Title == tag))
                        {
                            tagId = tags.First(t => t.Title == tag).Id;
                        }
                        else
                        {
                            dynamic newTag = await "http://localhost:41184/tags"
                               .SetQueryParam("token", token)
                               .PostJsonAsync(new
                               {
                                   title = tag,
                               })
                               .ReceiveJson();
                            tagId = newTag.id.ToString() as string;
                            tags.Add(new Tag { Id = tagId, Title = tag });
                        }
                        await $"http://localhost:41184/tags/{tagId}/notes"
                            .SetQueryParam("token", token)
                            .PostJsonAsync(new
                            {
                                id = noteId
                            });
                    }
                }
                catch (Exception ex)
                {
                    //   Log.Error(ex, "Error while doing {name}", post.Title);
                }
            }
        }
    }
}