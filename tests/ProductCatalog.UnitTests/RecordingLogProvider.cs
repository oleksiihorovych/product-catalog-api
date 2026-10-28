using Microsoft.Extensions.Logging;

namespace ProductCatalog.UnitTests;

internal sealed class RecordingLogProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();

    public List<Entry> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(this, categoryName);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;

    public void Dispose() { }

    internal sealed record Entry(string Category, LogLevel Level, Exception? Exception, string Message,
        IReadOnlyDictionary<string, object?> Properties, IReadOnlyDictionary<string, object?> Scope);

    private sealed class RecordingLogger(RecordingLogProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner.scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scope = new Dictionary<string, object?>();
            owner.scopes.ForEachScope((value, target) =>
            {
                if (value is IEnumerable<KeyValuePair<string, object?>> fields)
                {
                    foreach (var field in fields) target[field.Key] = field.Value;
                }
            }, scope);
            var properties = state is IEnumerable<KeyValuePair<string, object?>> fields
                ? fields.ToDictionary(field => field.Key, field => field.Value)
                : new Dictionary<string, object?>();
            owner.Entries.Add(new Entry(category, logLevel, exception, formatter(state, exception), properties, scope));
        }
    }
}
