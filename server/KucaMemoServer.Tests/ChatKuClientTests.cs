using System.Net;
using System.Text;
using System.Text.Json;
using KucaMemoServer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace KucaMemoServer.Tests;

/// <summary>챗쿠 클라이언트가 설정대로 요청을 만들고 답을 읽는지 (실제 API 는 부르지 않음)</summary>
public class ChatKuClientTests
{
    sealed class Recorder(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Sent { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Sent = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    static ChatKuClient Make(Recorder handler, Dictionary<string, string?> settings) =>
        new(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), NullLogger<ChatKuClient>.Instance);

    static readonly Dictionary<string, string?> Configured = new()
    {
        ["Recommend:Endpoint"] = "https://chatku.example/v1/chat/completions",
        ["Recommend:ApiKey"] = "test-key",
        ["Recommend:Model"] = "chatku-1",
    };

    [Fact]
    public async Task 키가_없으면_부르지_않는다()
    {
        var handler = new Recorder(HttpStatusCode.OK, "{}");
        ChatKuClient client = Make(handler, new() { ["Recommend:Endpoint"] = "https://chatku.example" });
        Assert.False(client.IsConfigured);
        Assert.Null(await client.CompleteAsync("s", "u", default));
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Bearer_키와_메시지를_보내고_choices_의_답을_읽는다()
    {
        var handler = new Recorder(HttpStatusCode.OK, """{"choices":[{"message":{"role":"assistant","content":"안녕"}}]}""");
        string? answer = await Make(handler, Configured).CompleteAsync("시스템", "사용자", default);
        Assert.Equal("안녕", answer);
        Assert.Equal("Bearer", handler.Request!.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", handler.Request.Headers.Authorization.Parameter);
        using JsonDocument sent = JsonDocument.Parse(handler.Sent!);
        Assert.Equal("chatku-1", sent.RootElement.GetProperty("model").GetString());
        Assert.Equal("system", sent.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("사용자", sent.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task 헤더_이름을_바꿀_수_있고_오류_응답이면_null()
    {
        var settings = new Dictionary<string, string?>(Configured) { ["Recommend:AuthHeader"] = "X-API-Key", ["Recommend:AuthScheme"] = "" };
        var handler = new Recorder(HttpStatusCode.Unauthorized, """{"error":"bad key"}""");
        Assert.Null(await Make(handler, settings).CompleteAsync("s", "u", default));
        Assert.Equal("test-key", handler.Request!.Headers.GetValues("X-API-Key").Single());
    }
}
