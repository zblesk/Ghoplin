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

    public class GhostService
    {
        public async Task<IList<Note>> LoadBlogPostsSince(string blogUrl, string apiKey, DateTime since)
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
            var results = new List<Note>();
            foreach (dynamic post in response.posts)
            {
                var note = new Note
                {
                    Title = post.title,
                    Content = post.html,
                    Timestamp = post.published_at,
                    Url = post.url,
                };
                results.Add(note);
                var tags = new List<string>();
                foreach (dynamic tag in post.tags)
                {
                    tags.Add(tag.name);
                }
                note.Tags = tags;
            }
            return results;
        }
    }
}
