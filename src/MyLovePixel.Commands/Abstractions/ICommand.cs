using MyLovePixel.Core.Document;

namespace MyLovePixel.Commands.Abstractions;

public interface IUndoToken { }

public sealed record CommandApplication(IUndoToken UndoToken, DocumentChange Change);

public interface ICommand
{
    string Name { get; }
    CommandApplication Apply(PixelDocument document);
    DocumentChange Revert(PixelDocument document, IUndoToken undoToken);
}
