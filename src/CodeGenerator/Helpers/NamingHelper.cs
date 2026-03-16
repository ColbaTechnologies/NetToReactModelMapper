namespace CodeGenerator.Helpers;

internal static class NamingHelper
{
    public static string ToCamelCase(string s) =>
        s.Length == 0 || char.IsLower(s[0]) ? s :
            char.ToLowerInvariant(s[0]) + s.Substring(1);
}