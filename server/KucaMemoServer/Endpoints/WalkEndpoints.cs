using KucaMemoServer.Models;
using KucaMemoServer.Services;

namespace KucaMemoServer.Endpoints;

/// <summary>
/// 산책 · 완료 · 리뷰 · 보상 API (docs/api.md 의 "산책 · 완료 · 리뷰 · 보상"). 로그인 토큰이 필요하다.
/// 경험치 · 레벨은 서버가 계산하고 앱은 받은 값만 보여 준다.
/// </summary>
public static class WalkEndpoints
{
    public static void MapWalkEndpoints(this WebApplication app)
    {
        var walks = app.MapGroup("/api/walks").WithTags("Walks");

        walks.MapPost("/", (WalkStartRequest? body, HttpRequest req, AccountStore accounts, WalkService service) =>
            Run(req, accounts, user => Results.Json(service.Start(user.Id, body), statusCode: 201)));

        walks.MapPost("/{walkId}/finish", (string walkId, WalkFinishRequest? body, HttpRequest req, AccountStore accounts, WalkService service) =>
            Run(req, accounts, user => Results.Ok(service.Finish(user.Id, walkId, body))));

        walks.MapPost("/{walkId}/review", async (string walkId, WalkReviewRequest? body, HttpRequest req, AccountStore accounts,
                                                 WalkService service, IContentModerator moderator, CancellationToken ct) =>
        {
            if (AuthEndpoints.Current(req, accounts) is not { } user)
                return Results.Json(new { error = "로그인이 필요해요." }, statusCode: 401);
            string text = (body?.Text ?? "").Trim();
            if (text.Length > WalkRules.MaxReviewTextLength)
                return Results.BadRequest(new { error = $"text 는 {WalkRules.MaxReviewTextLength}자 이내로 써 주세요." });
            try
            {
                service.CheckCanReview(user.Id, walkId);
                if (text.Length > 0)
                {
                    // 메모와 같은 내용 검사. 리뷰는 검토 대기 없이 애매하면 거절
                    ModerationResult check = await moderator.CheckAsync(text, null, ct);
                    if (check.Verdict != Verdict.Allow)
                        return Results.Json(new { error = check.UserMessage, reason = check.Reason ?? ModerationReasons.Abusive }, statusCode: 422);
                }
                return Results.Json(service.Review(user.Id, walkId, body, text.Length > 0 ? text : null), statusCode: 201);
            }
            catch (WalkException e)
            {
                return Error(e);
            }
        });

        app.MapGet("/api/me/progress", (HttpRequest req, AccountStore accounts, WalkService service) =>
            Run(req, accounts, user => Results.Ok(service.ProgressOf(user.Id)))).WithTags("Walks");
    }

    static IResult Run(HttpRequest req, AccountStore accounts, Func<Account, IResult> action)
    {
        if (AuthEndpoints.Current(req, accounts) is not { } user)
            return Results.Json(new { error = "로그인이 필요해요." }, statusCode: 401);
        try
        {
            return action(user);
        }
        catch (WalkException e)
        {
            return Error(e);
        }
    }

    static IResult Error(WalkException e) =>
        Results.Json(e.Reason == null ? new { error = e.Message } : new { error = e.Message, reason = e.Reason }, statusCode: e.Status);
}
