using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KucaMemoServer.Services;

/// <summary>산책 추천에 쓰는 AI. 시스템 문장과 사용자 문장을 주면 AI 의 답 글자를 돌려준다 (실패하면 null).</summary>
public interface IRouteAi
{
    bool IsConfigured { get; }
    Task<string?> CompleteAsync(string system, string user, CancellationToken ct);
}

/// <summary>
/// 챗쿠 AI API 클라이언트. 설정(appsettings / Azure 앱 설정 / dotnet user-secrets)의 "Recommend" 절을 읽는다.
///
///   Recommend:Endpoint    채팅 API 전체 주소 (예: https://api.example.com/v1/chat/completions)
///   Recommend:ApiKey      API 키. 저장소에 넣지 말 것 (Azure 앱 설정 Recommend__ApiKey)
///   Recommend:Model       모델 이름 (API 가 요구하면)
///   Recommend:AuthHeader  키를 보낼 헤더 (기본 Authorization)
///   Recommend:AuthScheme  헤더 값 앞에 붙일 말 (기본 Bearer, 비우면 키만)
///
/// Endpoint 나 ApiKey 가 비어 있으면 IsConfigured = false 이고, 추천은 규칙 기반(fallback)으로 동작한다.
/// 요청·응답은 널리 쓰이는 채팅 API 모양({ model, messages } → choices[0].message.content)을 따른다.
/// 챗쿠 API 모양이 다르면 이 파일의 BuildBody / ReadAnswer 만 바꾸면 된다.
/// </summary>
public class ChatKuClient(HttpClient http, IConfiguration config, ILogger<ChatKuClient> log) : IRouteAi
{
    string Endpoint => config["Recommend:Endpoint"] ?? "";
    string ApiKey => config["Recommend:ApiKey"] ?? "";
    string Model => config["Recommend:Model"] ?? "";
    string AuthHeader => config["Recommend:AuthHeader"] is { Length: > 0 } h ? h : "Authorization";
    string AuthScheme => config["Recommend:AuthScheme"] ?? "Bearer";

    public bool IsConfigured => Endpoint.Length > 0 && ApiKey.Length > 0;

    public async Task<string?> CompleteAsync(string system, string user, CancellationToken ct)
    {
        if (!IsConfigured)
            return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(BuildBody(system, user).ToJsonString(), Encoding.UTF8, "application/json"),
            };
            string value = AuthScheme.Length > 0 ? $"{AuthScheme} {ApiKey}" : ApiKey;
            if (AuthHeader.Equals("Authorization", StringComparison.OrdinalIgnoreCase) && AuthScheme.Length > 0)
                req.Headers.Authorization = new AuthenticationHeaderValue(AuthScheme, ApiKey);
            else
                req.Headers.TryAddWithoutValidation(AuthHeader, value);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var res = await http.SendAsync(req, ct);
            string text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                log.LogWarning("챗쿠 응답 오류 {Status}: {Body}", (int)res.StatusCode, Trim(text));
                return null;
            }
            return ReadAnswer(text);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning("챗쿠 호출 실패: {Message}", e.Message);
            return null;
        }
    }

    JsonObject BuildBody(string system, string user)
    {
        var body = new JsonObject
        {
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = user },
            },
            ["temperature"] = 0.7,
        };
        if (Model.Length > 0)
            body["model"] = Model;
        return body;
    }

    /// <summary>choices[0].message.content (없으면 output_text·content·text 순으로 찾아본다)</summary>
    static string? ReadAnswer(string json)
    {
        JsonNode? root = JsonNode.Parse(json);
        return root?["choices"]?[0]?["message"]?["content"]?.GetValue<string>()
            ?? root?["output_text"]?.GetValue<string>()
            ?? root?["content"]?.GetValue<string>()
            ?? root?["text"]?.GetValue<string>();
    }

    static string Trim(string s) => s.Length <= 300 ? s : s[..300] + "…";
}
