using CodeGenerator.Helpers;
using Xunit;

namespace CodeGenerator.Tests;

public class NamingHelperTests
{
    [Theory]
    [InlineData("MyProperty",   "myProperty")]
    [InlineData("Name",         "name")]
    [InlineData("myProperty",   "myProperty")]  // already camelCase
    [InlineData("",             "")]             // empty
    [InlineData("A",            "a")]            // single char
    [InlineData("ID",           "id")]           // all-uppercase acronym
    [InlineData("URL",          "url")]          // all-uppercase acronym
    [InlineData("UserId",       "userId")]       // mixed — only first char lowered
    [InlineData("XMLParser",    "xMLParser")]    // partial uppercase — first char only
    public void ToCamelCase_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToCamelCase(input));
    }
}
