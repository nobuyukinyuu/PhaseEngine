namespace GdsFMJson
{
    public static class JsonExtensions
    {
        /// Slices a substring from start index to end.
        public static string Slice(this string instance, int start, int end)
        {
        if (end < 0) // Keep this for negative end support
            {
                end = instance.Length + end;
            }
            int len = end - start;  // Calculate length
            return instance.Substring(start, len); 
        }

    }
}