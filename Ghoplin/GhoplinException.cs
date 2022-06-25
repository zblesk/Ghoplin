using System;
using System.Runtime.Serialization;

namespace Ghoplin;

[Serializable]
internal class GhoplinException : Exception
{
    public GhoplinException()
    {
    }

    public GhoplinException(string message) : base(message)
    {
    }

    public GhoplinException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected GhoplinException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
