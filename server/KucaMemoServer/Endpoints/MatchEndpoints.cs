using KucaMemoServer.Models;
using KucaMemoServer.Services;

namespace KucaMemoServer.Endpoints;

/// <summary>
/// 실시간 산책 매칭 API (docs/api.md 의 "매칭"). 모두 로그인 토큰이 필요하고,
/// 닉네임 · 성별까지 마친 계정(Account.Ready)만 쓸 수 있다. 앱은 GET /api/match 를 3초마다 부른다.
/// </summary>
public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this WebApplication app)
    {
        var match = app.MapGroup("/api/match").WithTags("Match");

        match.MapPost("/queue", (MatchJoinRequest? body, HttpRequest req, AccountStore accounts, MatchService service,
                                 IMatchExplainer explainer, CancellationToken ct) =>
            RunAsync(req, accounts, async user => Results.Ok(await service.JoinAsync(user, body, explainer, ct))));

        match.MapDelete("/queue", (HttpRequest req, AccountStore accounts, MatchService service) =>
            Run(req, accounts, user => { service.Leave(user.Id); return Results.NoContent(); }));

        match.MapGet("/", (HttpRequest req, AccountStore accounts, MatchService service, IMatchExplainer explainer, CancellationToken ct) =>
            RunAsync(req, accounts, async user => Results.Ok(await service.GetAsync(user.Id, explainer, ct))));

        match.MapPost("/{matchId}/accept", (string matchId, HttpRequest req, AccountStore accounts, MatchService service,
                                            ISmalltalkGenerator smalltalk, RouteRecommender recommender, CancellationToken ct) =>
            RunAsync(req, accounts, async user => Results.Ok(await service.AcceptAsync(user.Id, matchId, smalltalk, recommender, ct))));

        // 함께 걸을 코스: 둘 다 "이 코스 좋아요" 를 누르면 확정, "다른 코스" 는 3번까지
        match.MapPost("/{matchId}/course/agree", (string matchId, HttpRequest req, AccountStore accounts, MatchService service) =>
            Run(req, accounts, user => Results.Ok(service.AgreeCourse(user.Id, matchId))));

        match.MapPost("/{matchId}/course/next", (string matchId, HttpRequest req, AccountStore accounts, MatchService service,
                                                 RouteRecommender recommender, CancellationToken ct) =>
            RunAsync(req, accounts, async user => Results.Ok(await service.NextCourseAsync(user.Id, matchId, recommender, ct))));

        match.MapPost("/{matchId}/decline", (string matchId, HttpRequest req, AccountStore accounts, MatchService service) =>
            Run(req, accounts, user => Results.Ok(service.Decline(user.Id, matchId))));

        match.MapPut("/{matchId}/appearance", (string matchId, AppearanceRequest? body, HttpRequest req, AccountStore accounts,
                                               MatchService service, IContentModerator moderator, CancellationToken ct) =>
            RunAsync(req, accounts, async user => Results.Ok(await service.SetAppearanceAsync(user.Id, matchId, body?.Text, moderator, ct))));

        match.MapPost("/{matchId}/smalltalk", (string matchId, HttpRequest req, AccountStore accounts, MatchService service,
                                               ISmalltalkGenerator smalltalk, CancellationToken ct) =>
            RunAsync(req, accounts, async user => Results.Ok(await service.NextSmalltalkAsync(user.Id, matchId, smalltalk, ct))));

        match.MapPost("/{matchId}/arrived", (string matchId, ArrivedRequest? body, HttpRequest req, AccountStore accounts, MatchService service) =>
            Run(req, accounts, user => Results.Ok(service.Arrived(user.Id, matchId, body?.BuildingId))));

        match.MapPost("/{matchId}/cancel", (string matchId, MatchCancelRequest? body, HttpRequest req, AccountStore accounts, MatchService service) =>
            Run(req, accounts, user => Results.Ok(service.Cancel(user.Id, matchId, body?.Reason))));

        app.MapPost("/api/reports", (ReportRequest? body, HttpRequest req, AccountStore accounts, MatchService service) =>
            Run(req, accounts, user => { service.Report(user.Id, body); return Results.StatusCode(201); })).WithTags("Match");

        app.MapPost("/api/blocks", (BlockRequest? body, HttpRequest req, AccountStore accounts, MatchService service) =>
            Run(req, accounts, user => { service.Block(user.Id, body); return Results.StatusCode(201); })).WithTags("Match");
    }

    /// <summary>로그인 · 계정 설정을 확인하고, MatchException 을 {error, reason} 응답으로 바꾼다</summary>
    static IResult Run(HttpRequest req, AccountStore accounts, Func<Account, IResult> action)
    {
        if (Check(req, accounts, out Account? user) is { } denied)
            return denied;
        try
        {
            return action(user!);
        }
        catch (MatchException e)
        {
            return Error(e);
        }
    }

    static async Task<IResult> RunAsync(HttpRequest req, AccountStore accounts, Func<Account, Task<IResult>> action)
    {
        if (Check(req, accounts, out Account? user) is { } denied)
            return denied;
        try
        {
            return await action(user!);
        }
        catch (MatchException e)
        {
            return Error(e);
        }
    }

    static IResult? Check(HttpRequest req, AccountStore accounts, out Account? user)
    {
        user = AuthEndpoints.Current(req, accounts);
        if (user == null)
            return Results.Json(new { error = "로그인이 필요해요." }, statusCode: 401);
        if (!user.Ready)
            return Results.Json(new { error = "닉네임 · 성별 등 계정 설정을 먼저 마쳐 주세요.", reason = "not_ready" }, statusCode: 403);
        return null;
    }

    static IResult Error(MatchException e) =>
        Results.Json(e.Reason == null ? new { error = e.Message } : new { error = e.Message, reason = e.Reason }, statusCode: e.Status);
}
