using System.Text.Json;
using ZuLms.Application.Common.Abstractions;

namespace ZuLms.Infrastructure.University;

public sealed class UniversityApiClient(HttpClient http, ISettingsService settings) : IUniversityApiClient
{
    private const int MaxPages = 2000;

    public async Task<IReadOnlyList<JsonElement>> FetchAsync(UniversitySource source, CancellationToken ct)
    {
        string key = source switch
        {
            UniversitySource.Centers => "sync.centers_url",
            UniversitySource.Departments => "sync.departments_url",
            UniversitySource.Employees => "sync.employees_url",
            UniversitySource.Emails => "sync.emails_url",
            UniversitySource.Programs => "sync.programs_url",
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        string rawUrl = await settings.GetAsync<string>(key, ct);
        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme != Uri.UriSchemeHttps)
            throw new UniversitySourceException(source, "SOURCE_URL_INVALID");
        int limit = await settings.GetAsync<int>("sync.page_limit", ct);
        int timeoutSeconds = await settings.GetAsync<int>("sync.timeout_seconds", ct);
        var items = new List<JsonElement>();
        int offset = 0;
        for (int page = 0; page < MaxPages; page++)
        {
            var uri = new UriBuilder(baseUri);
            string existing = uri.Query.TrimStart('?');
            uri.Query = (existing.Length > 0 ? existing + "&" : "") + $"limit={limit}&offset={offset}";
            var result = await FetchPageWithRetriesAsync(source, uri.Uri, timeoutSeconds, ct);
            items.AddRange(result.Items);
            offset += result.Items.Count;
            if (!result.HasMore) return items;
            if (result.Items.Count == 0)
                throw new UniversitySourceException(source, "EMPTY_PAGE_WITH_MORE");
        }
        throw new UniversitySourceException(source, "PAGE_LIMIT_EXCEEDED");
    }

    private async Task<UniversityPage> FetchPageWithRetriesAsync(UniversitySource source,
        Uri uri, int timeoutSeconds, CancellationToken ct)
    {
        Exception? lastError = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (attempt > 0) await Task.Delay(TimeSpan.FromSeconds(1 << attempt), ct);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);
                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
                if (!json.RootElement.TryGetProperty("items", out var data) ||
                    data.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("SOURCE_ITEMS_INVALID");
                bool hasMore = false;
                if (json.RootElement.TryGetProperty("hasMore", out var more))
                {
                    if (more.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                        throw new InvalidDataException("SOURCE_HAS_MORE_INVALID");
                    hasMore = more.GetBoolean();
                }
                return new UniversityPage(data.EnumerateArray().Select(x => x.Clone()).ToList(), hasMore);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
                JsonException or InvalidDataException or OperationCanceledException)
            {
                lastError = ex;
            }
        }
        throw new UniversitySourceException(source, "SOURCE_PAGE_FAILED", lastError);
    }

    private sealed record UniversityPage(IReadOnlyList<JsonElement> Items, bool HasMore);
}

public sealed class UniversitySourceException(UniversitySource source, string code,
    Exception? inner = null) : Exception(code, inner)
{
    public UniversitySource UniversitySource { get; } = source;
    public string Code { get; } = code;
}
