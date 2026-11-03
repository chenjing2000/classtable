namespace ClassSchedule.Core;

public sealed class NotificationQueue(Func<DateTimeOffset>? clock = null) : IDisposable
{
    public static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan ExitDuration = TimeSpan.FromSeconds(.6);

    public sealed record Notice(string Text, object? Operation = null);
    private sealed record Ordinary(Notice Notice, TimeSpan AddedAt);

    private readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly List<Ordinary> ordinary = [];
    private readonly List<Notice> errors = [];
    private DateTimeOffset? lastUpdate;
    private TimeSpan ordinaryTime;
    private DateTimeOffset exitUntil;
    private bool disposed;

    public Notice? Exiting { get; private set; }
    public IReadOnlyList<Notice> Visible => Exiting?.Operation is not null ? []
        : errors.Count > 0 ? [errors[0]] : ordinary.Select(item => item.Notice).ToArray();
    public string Current => Visible.FirstOrDefault()?.Text ?? "";
    public event Action? Changed;

    private void UpdateClock(DateTimeOffset time)
    {
        if (lastUpdate is { } previous && errors.Count == 0)
        {
            if (Exiting?.Operation is not null && previous < exitUntil) previous = exitUntil;
            if (time > previous) ordinaryTime += time - previous;
        }
        lastUpdate = time;
    }

    public void Enqueue(string message)
    {
        if (disposed || string.IsNullOrWhiteSpace(message)) return;
        var time = now();
        UpdateClock(time);
        ordinary.Insert(0, new(new(message), ordinaryTime));
        if (ordinary.Count > 3)
        {
            var oldest = ordinary[^1].Notice;
            ordinary.RemoveAt(ordinary.Count - 1);
            if (errors.Count == 0 && Exiting is null) BeginExit(oldest, time);
        }
        Changed?.Invoke();
    }

    public void Error(string message, object operation)
    {
        if (disposed || string.IsNullOrWhiteSpace(message)) return;
        UpdateClock(now());
        if (errors.Any(error => Equals(error.Operation, operation) && error.Text == message)) return;
        if (Exiting?.Operation is null) Exiting = null;
        errors.Add(new(message, operation));
        Changed?.Invoke();
    }

    public void DismissError(Notice notice) => RemoveErrors(error => ReferenceEquals(error, notice));
    public void ResolveErrors(object operation) => RemoveErrors(error => Equals(error.Operation, operation));

    private void RemoveErrors(Predicate<Notice> remove)
    {
        if (disposed || errors.Count == 0) return;
        var time = now();
        UpdateClock(time);
        var current = errors[0];
        if (errors.RemoveAll(remove) == 0) return;
        if (Exiting?.Operation is null && remove(current)) BeginExit(current, time);
        Changed?.Invoke();
    }

    private void BeginExit(Notice notice, DateTimeOffset time)
    {
        Exiting = notice;
        exitUntil = time + ExitDuration;
    }

    public void Advance()
    {
        if (disposed) return;
        var time = now();
        UpdateClock(time);
        bool changed = Exiting is not null && time >= exitUntil;
        if (changed) Exiting = null;
        if (Exiting is null && errors.Count == 0 && ordinary.Count > 0 && ordinaryTime - ordinary[^1].AddedAt >= DisplayDuration)
        {
            BeginExit(ordinary[^1].Notice, time);
            ordinary.RemoveAt(ordinary.Count - 1);
            changed = true;
        }
        if (changed) Changed?.Invoke();
    }

    public void Dispose()
    {
        disposed = true;
        ordinary.Clear();
        errors.Clear();
        Exiting = null;
        Changed = null;
    }
}
