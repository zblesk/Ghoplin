using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Ghoplin
{
    public class Notebook
    {
        public Notebook(string id)
        {
            Id = id;
        }

        public string Id { get; set; }
        public string? Title { get; set; }
        public string? ParentId { get; set; }
        public long NoteCount { get; set; }

        public Notebook? Parent { get; set; }

        public IEnumerable<Notebook> Children { get; set; } = Enumerable.Empty<Notebook>();

        /// <summary>
        /// In a recursive preorder walk, returns self, then children.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Notebook> SelfAndAllChildren()
        {
            Debug.Assert(Children != null);
            yield return this;
            foreach (var child in Children)
            {
                foreach (var grandchild in child.SelfAndAllChildren())
                {
                    yield return grandchild;
                }
            }
        }
    }
}