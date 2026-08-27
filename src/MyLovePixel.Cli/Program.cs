using MyLovePixel.Commands;
using MyLovePixel.Commands.Pixel;
using MyLovePixel.Core.Document;
using MyLovePixel.Core.Pixel;
using MyLovePixel.Core.Validation;

var document = PixelDocumentFactory.CreateBlank(16, 16);
var cel = document.Cels.Single();
var surface = document.Resources.GetSurface(cel.SurfaceId);
var commands = new CommandBus(document);

commands.Execute(new PixelPatchCommand(cel.SurfaceId, [new PixelWrite(1, 1, new Rgba32(255, 64, 64))]));
DocumentValidator.ThrowIfInvalid(document);
Console.WriteLine($"After draw: {surface.GetPixel(1, 1)} | undo={commands.UndoCount}");

commands.Undo();
DocumentValidator.ThrowIfInvalid(document);
Console.WriteLine($"After undo: {surface.GetPixel(1, 1)} | redo={commands.RedoCount}");

commands.Redo();
DocumentValidator.ThrowIfInvalid(document);
Console.WriteLine($"After redo: {surface.GetPixel(1, 1)}");
