using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Core.Document;

namespace MyLovePixel.Commands.History;

internal sealed class AppliedCommand(ICommand command, IUndoToken undoToken)
{
    public ICommand Command { get; } = command;
    public IUndoToken UndoToken { get; set; } = undoToken;
}

internal sealed class HistoryEntry(string name, IReadOnlyList<AppliedCommand> commands)
{
    public string Name { get; } = name;
    public IReadOnlyList<AppliedCommand> Commands { get; } = commands;

    public IReadOnlyList<DocumentChange> Revert(PixelDocument document)
    {
        var changes = new List<DocumentChange>(Commands.Count);
        for (var i = Commands.Count - 1; i >= 0; i--)
        {
            var applied = Commands[i];
            changes.Add(applied.Command.Revert(document, applied.UndoToken));
        }
        return changes;
    }

    public IReadOnlyList<DocumentChange> Reapply(PixelDocument document)
    {
        var changes = new List<DocumentChange>(Commands.Count);
        foreach (var applied in Commands)
        {
            var result = applied.Command.Apply(document);
            applied.UndoToken = result.UndoToken;
            changes.Add(result.Change);
        }
        return changes;
    }
}
