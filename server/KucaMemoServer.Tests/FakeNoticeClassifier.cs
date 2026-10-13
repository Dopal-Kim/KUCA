using KucaMemoServer.Models;
using KucaMemoServer.Services;

namespace KucaMemoServer.Tests;

/// <summary>
/// 테스트용 공지 판별기. 실제 AI 를 부르지 않는다.
/// Next 를 정하면 그 결과를, 아니면 이름 찾기(KeywordNoticeClassifier)를 쓴다.
/// </summary>
public sealed class FakeNoticeClassifier : INoticeClassifier
{
    public NoticeClassification? Next { get; set; }
    public IReadOnlyList<Spot> Spots { get; set; } = Array.Empty<Spot>();
    public int Calls { get; private set; }

    public Task<NoticeClassification> ClassifyAsync(string title, string body, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(Next ?? KeywordNoticeClassifier.Classify(title, body, Spots));
    }
}
