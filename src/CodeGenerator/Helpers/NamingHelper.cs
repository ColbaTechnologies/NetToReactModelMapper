using System.Linq;

namespace CodeGenerator.Helpers;

internal static class NamingHelper
{
    public static string ToCamelCase(string s)
    {
        if (s.Length == 0 || char.IsLower(s[0]))
        {
            return s;
        }

        // All-uppercase acronym (e.g. "ID", "URL") → fully lowercase
        if (IsAllUpperCase(s))
        {
            return s.ToLowerInvariant();
        }

        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    private static bool IsAllUpperCase(string s) => s.All(char.IsUpper);
}
