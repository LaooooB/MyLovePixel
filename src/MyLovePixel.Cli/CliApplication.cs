using MyLovePixel.Core.Document;
using MyLovePixel.Export;
using MyLovePixel.Persistence;

namespace MyLovePixel.Cli;

public static class CliApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            if (args.Length == 0)
            {
                WriteUsage(error);
                return 2;
            }

            return args[0] switch
            {
                "export" => RunExport(args, output),
                "import-png" => RunImportPng(args, output),
                "preset-template" => RunPresetTemplate(args, output),
                "help" or "--help" or "-h" => ShowHelp(output),
                _ => UnknownCommand(args[0], error),
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or PixelProjectException)
        {
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int RunExport(string[] args, TextWriter output)
    {
        if (args.Length != 4) throw new ArgumentException("Usage: mylovepixel export <project.pixelproj> <preset.json> <output-directory>");
        var project = PixelProjectFile.Load(args[1]);
        var preset = ExportPresetJson.Deserialize(File.ReadAllBytes(args[2]));
        var snapshot = DocumentSnapshot.Capture(project.Document);
        var bundle = ExportPipeline.CreateDefault().Execute(new ExportRequest(snapshot, preset));
        ExportBundleWriter.WriteToDirectory(bundle, args[3]);
        output.WriteLine($"Exported {bundle.Artifacts.Count} artifact(s) to '{Path.GetFullPath(args[3])}'.");
        return 0;
    }

    private static int RunImportPng(string[] args, TextWriter output)
    {
        if (args.Length != 3) throw new ArgumentException("Usage: mylovepixel import-png <input.png> <output.pixelproj>");
        var bytes = File.ReadAllBytes(args[1]);
        var document = ImportPipeline.CreateDefault().Execute(
            BuiltinImporterIds.Png,
            new ImportRequest(Path.GetFileName(args[1]), bytes));
        PixelProjectFile.Save(args[2], new PixelProject(document));
        output.WriteLine($"Imported '{args[1]}' to '{Path.GetFullPath(args[2])}'.");
        return 0;
    }

    private static int RunPresetTemplate(string[] args, TextWriter output)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: mylovepixel preset-template <output.json>");
        File.WriteAllBytes(args[1], ExportPresetJson.Serialize(new ExportPreset()));
        output.WriteLine($"Wrote export preset template to '{Path.GetFullPath(args[1])}'.");
        return 0;
    }

    private static int ShowHelp(TextWriter output)
    {
        WriteUsage(output);
        return 0;
    }

    private static int UnknownCommand(string command, TextWriter error)
    {
        error.WriteLine($"Unknown command '{command}'.");
        WriteUsage(error);
        return 2;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("MyLovePixel CLI");
        writer.WriteLine("  export <project.pixelproj> <preset.json> <output-directory>");
        writer.WriteLine("  import-png <input.png> <output.pixelproj>");
        writer.WriteLine("  preset-template <output.json>");
    }
}
