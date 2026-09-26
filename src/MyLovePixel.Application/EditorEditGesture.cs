using MyLovePixel.Commands;

namespace MyLovePixel.Application;

public sealed class EditorEditGesture : IDisposable
{
    private CommandTransaction? _transaction;
    internal EditorEditGesture(DocumentSession session, string name) => _transaction = session.Commands.BeginTransaction(name);
    public void Finish(bool commit = true)
    {
        var transaction = Interlocked.Exchange(ref _transaction, null);
        if (transaction is null) return;
        if (commit) transaction.Commit(); else transaction.Rollback();
        transaction.Dispose();
    }
    public void Dispose() => Finish(false);
}

public static class EditorEditingGestures
{
    public static EditorEditGesture BeginUserEdit(this DocumentSession session, string name)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new EditorEditGesture(session, name);
    }
}
