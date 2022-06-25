using System.Collections.Generic;

namespace Ghoplin;

public record Tag(string Id, string Title);

public class TagPayload
{
    public List<Tag> Items { get; set; } = new List<Tag>();
    public bool Has_More { get; set; }
}
