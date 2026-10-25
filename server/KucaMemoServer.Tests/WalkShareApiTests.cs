using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace KucaMemoServer.Tests;

/// <summary>산책 기록 카카오톡 공유: 지도 그림을 올리고 공유 페이지 (/w/{id}) 를 만든다</summary>
public class WalkShareApiTests : IDisposable
{
    static readonly byte[] TinyPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
    const string Library = "way-474085534", StudentHall = "way-474085535";

    readonly TestServer server = new();
    readonly HttpClient client;

    public WalkShareApiTests() => client = server.CreateClient();

    public void Dispose() => server.Dispose();

    async Task<string> GuestToken()
    {
        JsonElement r = await (await client.PostAsync("/api/auth/guest", null)).Content.ReadFromJsonAsync<JsonElement>();
        return r.GetProperty("token").GetString()!;
    }

    static MultipartFormDataContent Form(byte[]? image = null, string meters = "1240", string minutes = "18", string ids = Library + "," + StudentHall + ",없는-건물")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(meters), "meters" },
            { new StringContent(minutes), "minutes" },
            { new StringContent("course"), "kind" },
            { new StringContent(ids), "buildingIds" },
        };
        if (image != null)
        {
            var file = new ByteArrayContent(image);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "image", "walk.png");
        }
        return form;
    }

    async Task<HttpResponseMessage> Post(string? token, MultipartFormDataContent form)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/walks/share") { Content = form };
        if (token != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(req);
    }

    [Fact]
    public async Task 지도_그림을_올리면_공유_페이지가_생기고_장소는_서버_이름으로()
    {
        HttpResponseMessage res = await Post(await GuestToken(), Form(TinyPng));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        JsonElement r = await res.Content.ReadFromJsonAsync<JsonElement>();
        string page = r.GetProperty("pageUrl").GetString()!;
        string image = r.GetProperty("imageUrl").GetString()!;
        Assert.StartsWith("/w/", page);
        Assert.StartsWith("/photos/walk_", image);
        Assert.Equal("1.2km · 18분 · 중앙도서관 → 학생회관", r.GetProperty("description").GetString());   // 모르는 건물 ID 는 버림

        string html = await client.GetStringAsync(page);
        Assert.Contains("님의 캠퍼스 산책 코스", html);   // 계정 닉네임 (체험 계정은 "체험생-1234")
        Assert.Contains("중앙도서관 → 학생회관", html);
        Assert.Contains("og:image\" content=\"http://localhost" + image, html);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(image)).StatusCode);
    }

    [Fact]
    public async Task 로그인_없이_그림_없이_이상한_값이면_거절()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(null, Form(TinyPng))).StatusCode);
        string token = await GuestToken();
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(token, Form(null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(token, Form(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }))).StatusCode);   // PNG 아님
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(token, Form(TinyPng, meters: "-5"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/w/nothing")).StatusCode);
    }

    [Fact]
    public async Task 탈퇴하면_공유한_기록도_지워진다()
    {
        string token = await GuestToken();
        JsonElement r = await (await Post(token, Form(TinyPng))).Content.ReadFromJsonAsync<JsonElement>();
        var del = new HttpRequestMessage(HttpMethod.Delete, "/api/me");
        del.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(del)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(r.GetProperty("pageUrl").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(r.GetProperty("imageUrl").GetString())).StatusCode);
    }
}
