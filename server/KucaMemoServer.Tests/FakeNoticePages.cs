using KucaMemoServer.Services;

namespace KucaMemoServer.Tests;

/// <summary>테스트용 게시판: 주소 → HTML. 없는 주소는 null (읽지 못함). 읽은 주소를 남긴다</summary>
public sealed class FakeNoticePages : INoticePageFetcher
{
    public Dictionary<string, string> Pages { get; } = new();
    public List<string> Requested { get; } = new();

    public Task<string?> GetAsync(string url, CancellationToken ct)
    {
        Requested.Add(url);
        return Task.FromResult(Pages.TryGetValue(url, out string? html) ? html : null);
    }
}
