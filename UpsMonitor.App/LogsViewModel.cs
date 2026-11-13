using System.ComponentModel;
using System.Runtime.CompilerServices;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

namespace UpsMonitor.App;

/// <summary>Owns only the Logs tab; all entry points and continuations run on the UI dispatcher.</summary>
public sealed class LogsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Func<EventQuery, CancellationToken, Task<EventQueryResult>>? _query;
    private readonly List<UpsEvent> _live = [];
    private CancellationTokenSource? _request;
    private EventQueryResult _saved = new([], false);
    private IReadOnlyList<UpsEventViewModel> _events = [];
    private bool _active;
    private bool _disposed;
    private int _rangeHours = 24;
    private UpsEventType? _eventType;
    private UpsEventSeverity? _severity;
    private string _search = string.Empty;
    private string _status = string.Empty;
    private bool _hasMore;
    private bool _loading;

    public LogsViewModel(SqliteTelemetryStore? store)
        : this(store is null ? null : store.QueryEventsAsync) { }

    internal LogsViewModel(Func<EventQuery, CancellationToken, Task<EventQueryResult>>? query)
    {
        _query = query;
        RefreshLanguage();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<LogRangeOption> RangeOptions { get; private set; } = [];
    public IReadOnlyList<LogTypeOption> TypeOptions { get; private set; } = [];
    public IReadOnlyList<LogSeverityOption> SeverityOptions { get; private set; } = [];
    public IReadOnlyList<UpsEventViewModel> FilteredEvents => _events.Where(MatchesSearch).ToArray();
    public string CountText => LocalizationManager.Format("LogCountFormat", FilteredEvents.Count, _events.Count);
    public string LimitText => _hasMore || _events.Count >= 500 ? LocalizationManager.Get("LogsLimitReached") : string.Empty;
    public string Status => _status;
    public bool IsLoading => _loading;

    public int RangeHours { get => _rangeHours; set { if (Set(ref _rangeHours, value)) Reload(); } }
    public UpsEventType? EventType { get => _eventType; set { if (Set(ref _eventType, value)) Reload(); } }
    public UpsEventSeverity? Severity { get => _severity; set { if (Set(ref _severity, value)) Reload(); } }
    public string SearchText
    {
        get => _search;
        set { if (Set(ref _search, value)) { Changed(nameof(FilteredEvents)); Changed(nameof(CountText)); } }
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (active) Reload(immediate: true);
        else { _request?.Cancel(); _request = null; _loading = false; Changed(nameof(IsLoading)); }
    }

    public void AddLive(UpsEvent value)
    {
        if (_disposed) return;
        if (!_live.Any(e => EventIdentity.Create(e) == EventIdentity.Create(value))) _live.Insert(0, value);
        if (_live.Count > 500) _live.RemoveAt(_live.Count - 1);
        Render(CurrentQuery());
        Reload();
    }

    private EventQuery CurrentQuery()
    {
        var now = DateTimeOffset.UtcNow;
        return new EventQuery
        {
            From = RangeHours == 0 ? null : now.AddHours(-RangeHours),
            EventTypes = EventType.HasValue ? new HashSet<UpsEventType> { EventType.Value } : null,
            Severity = Severity,
            Limit = 500,
        };
    }

    private void Reload(bool immediate = false)
    {
        _request?.Cancel();
        if (!_active || _disposed) return;
        var request = new CancellationTokenSource();
        _request = request;
        _ = RefreshAsync(request, immediate);
    }

    private async Task RefreshAsync(CancellationTokenSource request, bool immediate)
    {
        var token = request.Token;
        try
        {
            _loading = true;
            _status = LocalizationManager.Get("LogsLoading");
            Changed(nameof(IsLoading)); Changed(nameof(Status));
            if (!immediate) await Task.Delay(300, token);
            var query = CurrentQuery().Snapshot();
            var saved = _query is null ? new EventQueryResult([], false) :
                await Task.Run(() => _query(query, token), token);
            if (token.IsCancellationRequested || _request != request || _disposed) return;
            _saved = saved;
            Render(query);
            _status = _query is null ? LocalizationManager.Get("LogsHistoryDisabled") : string.Empty;
            Changed(nameof(Status));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (_request == request && !_disposed)
            {
                _status = LocalizationManager.Format("HistoryStorageErrorFormat", exception.Message);
                Changed(nameof(Status));
            }
        }
        finally
        {
            if (_request == request)
            {
                _request = null;
                _loading = false;
                Changed(nameof(IsLoading));
            }
            request.Dispose();
        }
    }

    private void Render(EventQuery query)
    {
        var result = EventQueryResults.Merge(query, _saved, _live);
        _events = result.Events.Select(e => new UpsEventViewModel(e)).ToArray();
        _hasMore = result.HasMore;
        Changed(nameof(FilteredEvents)); Changed(nameof(CountText)); Changed(nameof(LimitText));
    }

    private bool MatchesSearch(UpsEventViewModel value)
    {
        var text = SearchText.Trim();
        return text.Length == 0 || new[] { value.Type, value.Message, value.StateTransition, value.Timestamp }
            .Any(s => s.Contains(text, StringComparison.CurrentCultureIgnoreCase));
    }

    public void RefreshLanguage()
    {
        string L(string key) => LocalizationManager.Get(key);
        RangeOptions = [new(24, L("LogsRange24Hours")), new(168, L("LogsRange7Days")),
            new(720, L("LogsRange30Days")), new(0, L("LogsRangeAll"))];
        TypeOptions = new[] { new LogTypeOption(null, L("FilterAll")) }
            .Concat(Enum.GetValues<UpsEventType>().Select(t => new LogTypeOption(t,
                new UpsEventViewModel(new(DateTimeOffset.UnixEpoch, t, string.Empty,
                    UpsPowerState.Unknown, UpsPowerState.Unknown)).Type))).ToArray();
        SeverityOptions = [new(null, L("FilterAll")), new(UpsEventSeverity.Information, L("FilterInformation")),
            new(UpsEventSeverity.Warning, L("FilterWarning")), new(UpsEventSeverity.Critical, L("FilterCritical"))];
        foreach (var value in _events) value.RefreshLanguage();
        Changed(nameof(RangeOptions)); Changed(nameof(TypeOptions)); Changed(nameof(SeverityOptions));
        Changed(nameof(FilteredEvents)); Changed(nameof(CountText)); Changed(nameof(LimitText));
        Reload(immediate: true);
    }

    public void Dispose()
    {
        _disposed = true;
        _request?.Cancel();
        _request = null;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Changed(name); return true;
    }
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed record LogRangeOption(int Hours, string DisplayName);
public sealed record LogTypeOption(UpsEventType? Type, string DisplayName);
public sealed record LogSeverityOption(UpsEventSeverity? Severity, string DisplayName);
