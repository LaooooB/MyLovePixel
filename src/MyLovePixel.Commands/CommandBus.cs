using MyLovePixel.Commands.Abstractions;
using MyLovePixel.Commands.History;
using MyLovePixel.Core.Document;

namespace MyLovePixel.Commands;

public sealed class CommandBus
{
    private readonly PixelDocument _document;
    private readonly UndoStack _history;
    private TransactionState? _transaction;

    public CommandBus(PixelDocument document, UndoHistoryOptions? historyOptions = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _history = new UndoStack(historyOptions ?? new UndoHistoryOptions());
    }

    public event EventHandler<DocumentChange>? Changed;

    public bool CanUndo => _transaction is null && _history.CanUndo;
    public bool CanRedo => _transaction is null && _history.CanRedo;
    public int UndoCount => _history.UndoCount;
    public int RedoCount => _history.RedoCount;
    public UndoHistoryDiagnostics HistoryDiagnostics => _history.Diagnostics();

    public DocumentChange Execute(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var result = command.Apply(_document);
        var applied = new AppliedCommand(command, result.UndoToken);

        if (_transaction is not null)
            _transaction.Commands.Add(applied);
        else
            _history.Push(new HistoryEntry(command.Name, [applied]));

        Changed?.Invoke(this, result.Change);
        return result.Change;
    }

    public CommandTransaction BeginTransaction(string name)
    {
        if (_transaction is not null) throw new InvalidOperationException("Nested transactions are not supported.");
        _transaction = new TransactionState(string.IsNullOrWhiteSpace(name) ? "Transaction" : name);
        return new CommandTransaction(this, _transaction);
    }

    public void Undo()
    {
        EnsureNoActiveTransaction();
        if (!_history.CanUndo) return;
        var entry = _history.PeekUndo();
        var changes = entry.Revert(_document);
        _history.CommitUndo(entry);
        foreach (var change in changes) Changed?.Invoke(this, change);
    }

    public void Redo()
    {
        EnsureNoActiveTransaction();
        if (!_history.CanRedo) return;
        var entry = _history.PeekRedo();
        var previousCost = _history.Estimate(entry);
        var changes = entry.Reapply(_document);
        _history.RefreshEntryCost(entry, previousCost);
        _history.CommitRedo(entry);
        foreach (var change in changes) Changed?.Invoke(this, change);
    }

    internal void Commit(TransactionState state)
    {
        EnsureCurrent(state);
        if (state.Commands.Count > 0)
            _history.Push(new HistoryEntry(state.Name, state.Commands.ToArray()));
        _transaction = null;
    }

    internal void Rollback(TransactionState state)
    {
        EnsureCurrent(state);
        for (var i = state.Commands.Count - 1; i >= 0; i--)
        {
            var applied = state.Commands[i];
            var change = applied.Command.Revert(_document, applied.UndoToken);
            Changed?.Invoke(this, change);
        }
        _transaction = null;
    }

    private void EnsureNoActiveTransaction()
    {
        if (_transaction is not null) throw new InvalidOperationException("Cannot change history while a transaction is active.");
    }

    private void EnsureCurrent(TransactionState state)
    {
        if (!ReferenceEquals(_transaction, state)) throw new InvalidOperationException("Transaction is no longer active.");
    }

    internal sealed class TransactionState(string name)
    {
        public string Name { get; } = name;
        public List<AppliedCommand> Commands { get; } = [];
    }
}

public sealed class CommandTransaction : IDisposable
{
    private CommandBus? _bus;
    private CommandBus.TransactionState? _state;
    private bool _completed;

    internal CommandTransaction(CommandBus bus, CommandBus.TransactionState state)
    {
        _bus = bus;
        _state = state;
    }

    public void Commit()
    {
        if (_completed) throw new InvalidOperationException("Transaction already completed.");
        _bus!.Commit(_state!);
        _completed = true;
        _bus = null;
        _state = null;
    }

    public void Rollback()
    {
        if (_completed) throw new InvalidOperationException("Transaction already completed.");
        _bus!.Rollback(_state!);
        _completed = true;
        _bus = null;
        _state = null;
    }

    public void Dispose()
    {
        if (!_completed) Rollback();
    }
}
