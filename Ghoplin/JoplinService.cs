using Flurl;
using Flurl.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ghoplin
{
    internal class JoplinService
    {
        private const string ConfigNoteId = "00000000000031337000000000000001";
        private readonly string _apiUrl;
        private readonly string _token;

        public JoplinService(string apiUrl, string token)
        {
            _apiUrl = apiUrl ?? throw new ArgumentNullException(nameof(apiUrl));
            _token = token ?? throw new ArgumentNullException(nameof(token));
        }

        public async Task<GhoplinConfig> CreateConfigNote()
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
                .ReceiveJson().ConfigureAwait(false);
            return new GhoplinConfig();
        }

        public async Task UpdateConfigNote(GhoplinConfig ghostConfig)
        {
            await _apiUrl
                .AppendPathSegments("notes", ConfigNoteId)
                .SetQueryParam("token", _token)
                .PutJsonAsync(new
                {
                    body = JsonConvert.SerializeObject(ghostConfig, Formatting.Indented)
                })
                .ReceiveJson().ConfigureAwait(false);
        }

        public async Task<GhoplinConfig> GetConfigNote()
        {
            var configNote = await _apiUrl
                .AppendPathSegments("notes", ConfigNoteId)
                .SetQueryParam("token", _token)
                .SetQueryParam("fields", "body")
                .GetJsonAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<GhoplinConfig>(configNote.body);
        }

        public async Task<List<Tag>> LoadTags()
        {
            var allTags = await _apiUrl
                            .AppendPathSegment("tags")
                            .SetQueryParam("token", _token)
                            .GetStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<List<Tag>>(allTags);
        }

        public async Task<string> CreateNote(string notebookId, Note note)
        {
            var response = await _apiUrl
                .AppendPathSegment("notes")
                .SetQueryParam("token", _token)
                .PostJsonAsync(new
                {
                    parent_id = notebookId,
                    title = note.Title,
                    body_html = note.Content,
                    source_url = note.Url,
                    base_url = new Uri(note.Url).GetLeftPart(UriPartial.Path),
                    user_created_time = note.Timestamp?.ToUnixTimestampMiliseconds().ToString(),
                    user_updated_time = note.Timestamp?.ToUnixTimestampMiliseconds().ToString(),
                })
                .ReceiveJson().ConfigureAwait(false);
            note.Id = response.id.ToString() as string;
            return note.Id;
        }

        public async Task<Tag> CreateTag(string tag)
        {
            dynamic newTag = await _apiUrl
               .AppendPathSegment("tags")
               .SetQueryParam("token", _token)
               .PostJsonAsync(new
               {
                   title = tag,
               })
               .ReceiveJson().ConfigureAwait(false);
            return new Tag
            {
                Id = newTag.id.ToString() as string,
                Title = tag
            };
        }

        public async Task AssignTag(Tag tag, Note note)
        {
            await _apiUrl
                .AppendPathSegments("tags", tag.Id, "notes")
                .SetQueryParam("token", _token)
                .PostJsonAsync(new
                {
                    id = note.Id
                })
                .ConfigureAwait(false);
        }
    }
}