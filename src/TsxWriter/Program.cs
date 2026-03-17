using TsWriter;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: TsxWriter <assemblyPath> [outputDirectory]");
    return 1;
}

var reader = new AssemblyContentReader();
var  writer = new TypeScriptFileWriter();

var fallback = args.Length > 1 ? args[1] : "src/generated";
var (outputPath, models) = reader.Read(args[0], fallback);

writer.Write(models, outputPath);
return 0;
