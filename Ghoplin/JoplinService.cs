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
    internal class JoplinService
    {
        private const string ConfigNoteId = "00000000000031337000000000000001";
        private readonly string _apiUrl;
        private readonly string _token;
        private static ILogger l = new LoggerConfiguration()
                            .MinimumLevel.Verbose()
                            .WriteTo.File("chibi.log")
                            .CreateLogger();

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

        public async Task<GhoplinConfig> GetConfigNote()
        {
            var configNote = await _apiUrl
                .AppendPathSegments("notes", ConfigNoteId)
                .SetQueryParam("token", _token)
                .SetQueryParam("fields", "body")
                .GetJsonAsync().ConfigureAwait(false);
            if (configNote == null)
            {
                return null;
            }
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

        public async Task<NotebookList> GetNotebooks()
        {
            var notebookList = await _apiUrl
                .AppendPathSegments("folders")
                .SetQueryParam("token", _token)
                .GetJsonListAsync().ConfigureAwait(false);
       
            return new NotebookList(notebookList.Select(nb => (Notebook)CreateNotebook(nb)).ToList());
        }

        private Notebook CreateNotebook(dynamic notebookData, Notebook parent = null)
        {
            var notebook = new Notebook
            {
                Id = notebookData.id,
                Title = notebookData.title,
                NoteCount = notebookData.note_count,
                ParentId = notebookData.parent_id,
                Parent = parent,
                Children = Enumerable.Empty<Notebook>(),
            };
            Debug.Assert(string.IsNullOrWhiteSpace(notebook.ParentId) || notebook.ParentId == notebook.Parent.Id);
            try
            {
                var children = new List<Notebook>();
                foreach (var child in notebookData.children)
                {
                    children.Add(CreateNotebook(child, notebook));
                }
                notebook.Children = children;
            }
            catch (RuntimeBinderException ex) when (ex.Message == "'System.Dynamic.ExpandoObject' does not contain a definition for 'children'")
            {
                // No children. Do nothing.
            }
            return notebook;
        }

        public async Task<string> CreateNote(string notebookId, Note note)
        {
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
                    source_url = note.Url,
                    base_url = new Uri(note.Url).GetLeftPart(UriPartial.Path),
                    user_created_time = note.Timestamp?.ToUnixTimestampMiliseconds().ToString(),
                    user_updated_time = note.Timestamp?.ToUnixTimestampMiliseconds().ToString(),
                })
                .ReceiveJson().ConfigureAwait(false);
            note.Id = response.id.ToString() as string;
            return note.Id;

            }
            catch (Exception ex)
            {
                Log.Error("Note creation failed");
                l.Error(ex, "Note creation failed");
                l.Error(ex?.InnerException, "Note creation failed inner");
                var found = await _apiUrl
                    .AppendPathSegments("folders", notebookId, "notes")
                    .SetQueryParams(new
                    {
                        token = _token,
                        fields = "source_url,id"
                    })
                    .GetStringAsync();
                var a = (JArray)JsonConvert.DeserializeObject(found);
                var first = a.FirstOrDefault(i => i["source_url"].ToString() == note.Url);
                if (first != null)
                {
                    note.Id = first["id"].ToString();
                    Log.Warning("Found ID anyway: {id}", note.Id);
                    return note.Id; 
                }
            }
            return "";
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
                Log.Error("tag add failed");
                l.Error(ex, "tag add failed");
                l.Error(ex?.InnerException, "tag add failed inner");
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
    }
}