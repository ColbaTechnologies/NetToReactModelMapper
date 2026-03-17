using TsWriter;
using Xunit;

namespace TsxWriter.Tests;

public class TypeScriptFileWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly TypeScriptFileWriter _writer = new();

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Write_EmptyDictionary_WritesNothing()
    {
        _writer.Write(new Dictionary<string, string>(), _dir);
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void Write_ContentWithExport_CreatesFile()
    {
        var models = new Dictionary<string, string>
        {
            ["User"] = "export interface User { id: number; }"
        };

        _writer.Write(models, _dir);

        var expected = Path.Combine(_dir, "User.tsx");
        Assert.True(File.Exists(expected));
        Assert.Contains("export interface User", File.ReadAllText(expected));
    }

    [Fact]
    public void Write_ContentWithoutExport_IsSkipped()
    {
        var models = new Dictionary<string, string>
        {
            ["NoExport"] = "// just a comment"
        };

        _writer.Write(models, _dir);

        Assert.False(File.Exists(Path.Combine(_dir, "NoExport.tsx")));
    }

    [Fact]
    public void Write_MultipleModels_AllWritten()
    {
        var models = new Dictionary<string, string>
        {
            ["Alpha"] = "export interface Alpha { }",
            ["Beta"]  = "export type Beta = 'x' | 'y';",
        };

        _writer.Write(models, _dir);

        Assert.True(File.Exists(Path.Combine(_dir, "Alpha.tsx")));
        Assert.True(File.Exists(Path.Combine(_dir, "Beta.tsx")));
    }

    [Fact]
    public void Write_FileHasNoBom()
    {
        var models = new Dictionary<string, string> { ["M"] = "export interface M { }" };
        _writer.Write(models, _dir);

        var bytes = File.ReadAllBytes(Path.Combine(_dir, "M.tsx"));
        // UTF-8 BOM is EF BB BF
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
    }

    [Fact]
    public void Write_Filename_DoesNotAppendModelSuffix()
    {
        var models = new Dictionary<string, string> { ["Order"] = "export interface Order { }" };
        _writer.Write(models, _dir);

        Assert.True(File.Exists(Path.Combine(_dir, "Order.tsx")));
        Assert.False(File.Exists(Path.Combine(_dir, "OrderModel.tsx")));
    }

    [Fact]
    public void Write_CreatesOutputDirectoryIfMissing()
    {
        var nested = Path.Combine(_dir, "deep", "path");
        var models = new Dictionary<string, string> { ["X"] = "export interface X { }" };
        _writer.Write(models, nested);
        Assert.True(Directory.Exists(nested));
    }
}
