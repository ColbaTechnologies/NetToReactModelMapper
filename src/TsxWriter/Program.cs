using TsWriter;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: <assemblyPath> [outputDirectory]");
    return 1;
}

var reader = new AssemblyContentReader();
var writer = new TypeScriptFileWriter();

var fallback   = args.Length > 1 ? args[1] : "src/generated";
var outputPath = reader.GetOutputPath(args[0], fallback);
var models     = reader.Read(args[0]);

writer.Write(models, outputPath);
return 0;