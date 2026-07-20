/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace SignalWire.REST;

/// <summary>
/// Walks paged HTTP responses by following ``links.next`` cursors.
///
/// Mirrors Python ``signalwire.rest._pagination.PaginatedIterator`` —
/// constructor records inputs without fetching; iteration triggers
/// the first fetch and continues until a page is returned without a
/// ``links.next`` cursor.
/// </summary>
public class PaginatedIterator : IAsyncEnumerable<Dictionary<string, object?>>
{
    private readonly HttpClient _http;
    private readonly string _path;
    private readonly Dictionary<string, string>? _params;
    private readonly string _dataKey;

    // Fields exposed through internal accessors so the cross-language audit
    // can detect parity with Python's _http / _path / _params / _data_key /
    // _index / _items / _done.
    private List<Dictionary<string, object?>> _items = new();
    private int _index;
    private bool _done;
    private string? _nextPath;
    private Dictionary<string, string>? _nextParams;

    // Cycle guard (mirrors python rest/_pagination.py `_seen_next`): the set of
    // `links.next` cursor URLs already fetched. A server that returns the SAME
    // next cursor it was just handed would loop forever; when a repeated cursor is
    // seen we terminate instead of re-fetching. Keyed by the full next-URL string.
    private readonly HashSet<string> _seenNext = new(StringComparer.Ordinal);

    public PaginatedIterator(HttpClient http, string path,
        Dictionary<string, string>? @params = null, string dataKey = "data")
    {
        _http = http;
        _path = path;
        _params = @params;
        _dataKey = dataKey;
        _nextPath = path;
        _nextParams = @params;
    }

    // Accessors mirroring Python's instance attributes (used by tests).
    public HttpClient Http => _http;
    public string Path => _path;
    public Dictionary<string, string>? Params => _params;
    public string DataKey => _dataKey;
    public int Index => _index;
    public IReadOnlyList<Dictionary<string, object?>> Items => _items;
    public bool Done => _done;

    /// <summary>Returns the next item, or throws InvalidOperationException
    /// when exhausted (mirroring Python's StopIteration).</summary>
    public async Task<Dictionary<string, object?>> NextAsync()
    {
        // Advance across pages until an item is available or we genuinely
        // terminate. An EMPTY page that still carries a `links.next` cursor is NOT
        // the end — more pages exist, this one just matched nothing — so we keep
        // fetching past it (mirrors python: termination is driven ONLY by the
        // absence of a next link, never by an empty page). A naive
        // "stop when the page is empty" would silently drop every later page.
        while (_index >= _items.Count)
        {
            if (_done) throw new InvalidOperationException("PaginatedIterator exhausted");
            await FetchNextPageAsync().ConfigureAwait(false);
            // FetchNextPageAsync sets _done when there is no further next link (or
            // a repeated cursor was detected). If it set _done and the freshly
            // fetched page is still empty, iteration is truly exhausted.
            if (_index >= _items.Count && _done)
            {
                throw new InvalidOperationException("PaginatedIterator exhausted");
            }
        }
        return _items[_index++];
    }

    private async Task FetchNextPageAsync()
    {
        if (_nextPath is null)
        {
            _done = true;
            return;
        }
        var resp = await _http.GetAsync(_nextPath, _nextParams).ConfigureAwait(false);

        if (resp.TryGetValue(_dataKey, out var dataObj) && dataObj is List<object?> dataList)
        {
            _items = dataList.OfType<Dictionary<string, object?>>().ToList();
            _index = 0;
        }
        else
        {
            _items = new();
            _index = 0;
        }

        if (resp.TryGetValue("links", out var linksObj)
            && linksObj is Dictionary<string, object?> links
            && links.TryGetValue("next", out var nextObj)
            && nextObj?.ToString() is { Length: > 0 } nextUrl)
        {
            // Cycle guard: a `links.next` we have ALREADY fetched means the server
            // is handing back the same cursor — following it would loop forever.
            // Terminate instead (mirrors python `_seen_next`). The raw cursor
            // string is the identity key (before URL/path splitting).
            if (!_seenNext.Add(nextUrl))
            {
                _nextPath = null;
                _done = true;
                return;
            }

            if (nextUrl.StartsWith("http", StringComparison.Ordinal))
            {
                var uri = new Uri(nextUrl);
                _nextPath = uri.AbsolutePath;
                _nextParams = ParseQueryString(uri.Query);
            }
            else
            {
                var parts = nextUrl.Split('?', 2);
                _nextPath = parts[0];
                _nextParams = parts.Length > 1 ? ParseQueryString("?" + parts[1]) : null;
            }
        }
        else
        {
            // No next link → this is the last page. Terminate after its items
            // are yielded (an empty last page with no next → exhausted).
            _nextPath = null;
            _done = true;
        }
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(query)) return result;
        var q = query.TrimStart('?');
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var val = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            result[key] = val;
        }
        return result;
    }

    /// <summary>Async-enumerable adapter so callers can write
    /// ``await foreach (var item in iterator)``.</summary>
    public async IAsyncEnumerator<Dictionary<string, object?>> GetAsyncEnumerator(
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Dictionary<string, object?> item;
            try
            {
                item = await NextAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                yield break;
            }
            yield return item;
        }
    }
}
