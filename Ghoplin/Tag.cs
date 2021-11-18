using System.Collections.Generic;

namespace Ghoplin
{
    public class Tag
    {
        public string Id { get; set; }
        public string Title { get; set; }
    }

    public class TagPayload
    {
        public List<Tag> Items { get; set; }
        public bool Has_More { get; set; }
    }
}