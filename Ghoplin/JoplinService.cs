using Flurl;
using Flurl.Http;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Ghoplin
{
    public class JoplinService
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
            ghostConfig.LastRun = DateTime.UtcNow;
            await _apiUrl
                .AppendPathSegments("notes", ConfigNoteId)
                .SetQueryParam("token", _token)
                .PutJsonAsync(new
                {
                    body = JsonConvert.SerializeObject(ghostConfig, Formatting.Indented)
                })
                .ReceiveJson().ConfigureAwait(false);
        }

        public async Task<GhoplinConfig?> GetConfigNote()
        {
            var configNote1 = _apiUrl
                .AppendPathSegments("notes", ConfigNoteId)
                .SetQueryParam("token", _token)
                .SetQueryParam("fields", "body");
            var configNote = await configNote1
                .GetJsonAsync().ConfigureAwait(false);
            if (configNote == null)
            {
                return null;
            }
            return JsonConvert.DeserializeObject<GhoplinConfig>(configNote.body);
        }

        public async Task<List<Tag>> LoadTags()
        {
            var allTags = new List<Tag>();
            bool cont;
            int page = 0;
            do
            {
                page++;
                var response = await _apiUrl
                                .AppendPathSegment("tags")
                                .SetQueryParam("token", _token)
                                .SetQueryParam("page", page)
                                .GetStringAsync().ConfigureAwait(false);
                var a = JsonConvert.DeserializeObject<TagPayload>(response);
                cont = a.Has_More;
                allTags.AddRange(a.Items);
            } while (cont);
            return allTags;
        }

        public async Task<NotebookList> GetNotebooks()
        {
            var notebookList = await _apiUrl
                .AppendPathSegments("folders")
                .SetQueryParam("token", _token)
                .GetJsonListAsync().ConfigureAwait(false);

            return new NotebookList(notebookList.Select(nb => (Notebook)BuildNotebookFromResponse(nb)).ToList());
        }

        public async Task<string> CreateNote(string notebookId, Note note)
        {
            var url = "";
            if (!string.IsNullOrWhiteSpace(note.Url))
            {
                url = new Uri(note.Url).GetLeftPart(UriPartial.Path);
            }
            try
            {
                var response = await _apiUrl
                    .AppendPathSegment("notes")
                    .SetQueryParam("token", _token)
                    .PostJsonAsync(new
                    {
                        parent_id = notebookId,
                        title = note.Title,
                        body_html = note.Content,
                        
                        source_url = note.Url ?? "",
                        base_url = url,
                        user_created_time = note.Timestamp?.ToUnixTimestampMiliseconds().ToString(),
                        user_updated_time = note.Timestamp?.ToUnixTimestampMiliseconds().ToString(),
                    })
                    .ReceiveJson().ConfigureAwait(false);
                note.Id = response.id.ToString() as string;
                Trace.Assert(string.IsNullOrWhiteSpace(note.Id));
#pragma warning disable CS8603 // Possible null reference return.
                return note.Id;
#pragma warning restore CS8603 // Possible null reference return.
            }
            catch (FlurlHttpException)
            {
                Log.Debug("Note creation failed, trying a fetch");
                await Task.Delay(1500);
                var found = await _apiUrl
                    .AppendPathSegments("folders", notebookId, "notes")
                    .SetQueryParams(new
                    {
                        token = _token,
                        fields = "source_url,id"
                    })
                    .GetStringAsync();
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                var a = (JArray)JsonConvert.DeserializeObject(found);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                var first = a?.FirstOrDefault(i => i["source_url"]?.ToString() == note.Url);
                if (first != null)
                {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    note.Id = first["id"].ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    Log.Debug("Found ID anyway: {id}", note.Id);
                    return note.Id;
                }
            }
            throw new GhoplinException($"Note creation failed for '{note.Title}'");
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
            return new Tag(
#pragma warning disable CS8604 // Possible null reference argument.
                newTag.id.ToString() as string,
#pragma warning restore CS8604 // Possible null reference argument.
                tag);
        }

        public async Task AssignTag(Tag tag, Note note)
        {
            try
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
            catch (Exception ex)
            {
                Log.Error(ex, "Tag add failed ('{tag}' to '{noteTitle}' - {noteId}", tag.Title, note.Title, note.Id);
            }
        }

        public async Task<Notebook> GetNotebookById(string id)
        {
            var notebookResponse = await _apiUrl
                .AppendPathSegments("folders", id)
                .SetQueryParam("token", _token)
                .GetStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<Notebook>(notebookResponse);
        }

        private Notebook BuildNotebookFromResponse(dynamic notebookData, Notebook? parent = null)
        {
            var notebook = new Notebook(notebookData.id)
            {
                Title = notebookData.title,
                NoteCount = notebookData.note_count,
                ParentId = notebookData.parent_id,
                Parent = parent,
                Children = Enumerable.Empty<Notebook>(),
            };
            Debug.Assert(string.IsNullOrWhiteSpace(notebook.ParentId) || notebook.ParentId == notebook.Parent?.Id);
            try
            {
                var children = new List<Notebook>();
                foreach (var child in notebookData.children)
                {
                    children.Add(BuildNotebookFromResponse(child, notebook));
                }
                notebook.Children = children;
            }
            catch (RuntimeBinderException ex) when (ex.Message == "'System.Dynamic.ExpandoObject' does not contain a definition for 'children'")
            {
                // No children. Do nothing.
            }
            return notebook;
        }
    }
}