namespace ClassTimetable.Core;

public sealed class NotificationQueue(Func<DateTimeOffset>? clock = null) : IDisposable
{
    private readonly Func<DateTimeOffset> now = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Queue<string> waiting = new();
    private DateTimeOffset visibleSince;
    private bool disposed;
    public string Current { get; private set; } = "";
    public event Action? Changed;
    public void Enqueue(string message)
    {
        if (disposed || string.IsNullOrWhiteSpace(message) || Current == message || waiting.Contains(message)) return;
        if (Current.Length == 0) { Current = message; visibleSince = now(); Changed?.Invoke(); }
        else waiting.Enqueue(message);
    }
    public void Advance()
    {
        if (disposed || Current.Length == 0 || now() - visibleSince < TimeSpan.FromSeconds(5)) return;
        Current = waiting.Count == 0 ? "" : waiting.Dequeue(); visibleSince = now(); Changed?.Invoke();
    }
    public void Dispose() { disposed = true; waiting.Clear(); Current = ""; Changed = null; }
}
