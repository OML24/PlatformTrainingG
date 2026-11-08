using System.Net;
using System.Text;
using ZuLms.Application.Common.Abstractions;
using ZuLms.Application.Common.Results;
using ZuLms.Infrastructure.University;

namespace ZuLms.Infrastructure.Tests;

public sealed class UniversityApiClientTests
{
    [Fact]
    public async Task FetchesAcademicCatalogFromItsOwnConfiguredEndpoint()
    {
        var handler = new StubHandler((_, _) => """{"items":[{"departmentid":"0101"}]}""");
        using var http = new HttpClient(handler);
        var client = new UniversityApiClient(http, new StubSettings());
        var rows = await client.FetchAsync(UniversitySource.Programs, CancellationToken.None);
        Assert.Single(rows);
        Assert.Contains("/program?", handler.Requests.Single());
        Assert.Equal("0101", rows[0].GetProperty("departmentid").GetString());
    }

    [Fact]
    public async Task FetchesAllOrdsPagesUsingReceivedCountForOffset()
    {
        var handler = new StubHandler((_, count) => count switch
        {
            1 => """{"items":[{"side":320,"center_id":1},{"side":320,"center_id":2}],"hasMore":true}""",
            _ => """{"items":[{"side":320,"center_id":3}]}"""
        });
        using var http = new HttpClient(handler);
        var client = new UniversityApiClient(http, new StubSettings());
        var rows = await client.FetchAsync(UniversitySource.Centers, CancellationToken.None);
        Assert.Equal(3, rows.Count);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("limit=500", handler.Requests[0]);
        Assert.Contains("offset=0", handler.Requests[0]);
        Assert.Contains("offset=2", handler.Requests[1]);
        Assert.Contains("foo=bar", handler.Requests[1]);
    }

    [Fact]
    public async Task RejectsEmptyPageThatClaimsMoreData()
    {
        var handler = new StubHandler((_, _) => """{"items":[],"hasMore":true}""");
        using var http = new HttpClient(handler);
        var client = new UniversityApiClient(http, new StubSettings());
        var error = await Assert.ThrowsAsync<UniversitySourceException>(() =>
            client.FetchAsync(UniversitySource.Employees, CancellationToken.None));
        Assert.Equal("EMPTY_PAGE_WITH_MORE", error.Code);
    }

    private sealed class StubHandler(Func<Uri, int, string> body) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri uri = request.RequestUri!;
            Requests.Add(uri.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body(uri, Requests.Count), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubSettings : ISettingsService
    {
        public Task<T> GetAsync<T>(string key, CancellationToken cancellationToken)
        {
            object value = key switch
            {
                "sync.centers_url" or "sync.employees_url" => "https://test.example/ords?foo=bar",
                "sync.programs_url" => "https://test.example/program",
                "sync.page_limit" => 500,
                "sync.timeout_seconds" => 60,
                _ => throw new InvalidOperationException(key)
            };
            return Task.FromResult((T)value);
        }
        public Task<Result<IReadOnlyList<SystemSettingDto>>> ListAsync(long actorUserId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> SetAsync(string key, string value, uint expectedVersion,
            long actorUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Invalidate(string key) { }
    }
}
