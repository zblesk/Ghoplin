using System;

namespace Ghoplin
{
    public static class Helpers
    {
        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToUnixTimestampMiliseconds(this DateTime date) => (long)(date.Subtract(Epoch)).TotalMilliseconds;
    }
}