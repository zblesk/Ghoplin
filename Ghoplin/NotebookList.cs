using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Ghoplin
{
    /// <summary>
    /// Represents all notebooks in Joplin
    /// </summary>
    public class NotebookList : IEnumerable<Notebook>
    {
        private readonly IEnumerable<Notebook> _notebooks = Enumerable.Empty<Notebook>();

        /// <summary>
        /// Encapsulates an immutable collection of notebooks.
        /// </summary>
        /// <param name="notebooks"></param>
        public NotebookList(IEnumerable<Notebook> notebooks)
        {
            _notebooks = notebooks;
        }

        /// <summary>
        /// Enumerates all the notebooks in a flat list.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Notebook> AllNotebooks()
        {
            foreach (var notebook in _notebooks)
            {
                foreach (var child in notebook.SelfAndAllChildren())
                {
                    yield return child;
                }
            }
        }

        /// <summary>
        /// Gets the top-level notebooks. Access any child notebooks through the Children property.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Notebook> GetTopNotebooks()
        {
            return _notebooks.GetEnumerator();
        }

        /// <summary>
        /// Gets all Notebooks, same as calling AllNotebooks.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Notebook> GetEnumerator()
        {
            return AllNotebooks();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return AllNotebooks();
        }
    }
}