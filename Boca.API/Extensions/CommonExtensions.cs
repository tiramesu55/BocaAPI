namespace BocaAPI.Extensions
{
    public static class CommonExtensions
    {
        public static string StringJoin(this IEnumerable<string> input, string separator = ";") => string.Join(separator, input);
    }
}
