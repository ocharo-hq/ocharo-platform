using OcharoTrends.Api.Access;
using OcharoTrends.Api.Persistence;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;

namespace OcharoTrends.Api.Trends;

internal static class TrendRoutes
{
    internal static void MapTrends(this WebApplication app, ReviewAccess access)
    {
        app.MapGet("/api/trends/public/guides", (TrendLibrary library) => library.PublishedGuides).AllowAnonymous();
        var review = app.MapGroup("/api/trends/review");
        if (!access.Enabled)
        {
            app.MapGet("/api/identity/session", () => Results.Ok(new { data = new { isAuthenticated = false } }));
            app.MapGet("/api/identity/sign-in", ReviewUnavailable);
            review.MapGet("/videos", ReviewUnavailable);
            review.MapPut("/videos/{id}", ReviewUnavailable);
            return;
        }

        app.MapIdentityBffEndpoints();
        review.RequireAuthorization(ReviewAccess.MemberPolicy);
        review.MapGet("/videos", async (TrendLibrary library, ReviewStore store, CancellationToken cancellationToken) =>
        {
            var decisions = await store.ListAsync(access.OrganizationId, cancellationToken);
            return library.Videos.Select(video => decisions.TryGetValue(video.Id, out var decision) ? video.Apply(decision) : video).ToArray();
        });
        review.MapPut("/videos/{id}", async (string id, Review decision, HttpContext context, TrendLibrary library,
            ReviewStore store, CancellationToken cancellationToken) =>
        {
            if (!decision.IsValid())
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["review"] = ["Choose unreviewed, useful or skipped and add a note of at most 4000 characters."],
                });

            var video = library.FindVideo(id);
            if (video is null)
                return Results.NotFound();

            await store.SaveAsync(access.OrganizationId, video.Id, decision, ReviewAccess.Subject(context.User)!, cancellationToken);
            return Results.Ok(video.Apply(decision));
        }).RequireAuthorization(ReviewAccess.EditorPolicy);
    }

    private static IResult ReviewUnavailable() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable, title: "Ocharo Trends review is unavailable",
        detail: "Configure Ocharo Identity, the organization and PostgreSQL before opening the review board.");
}
