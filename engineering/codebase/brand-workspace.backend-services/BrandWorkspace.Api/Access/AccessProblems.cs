namespace BrandWorkspace.Api.Access;

/// <summary>Produces stable API failure codes without disclosing account state.</summary>
public static class AccessProblems
{
    public static IResult Create(int status, string title, string code) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
}
