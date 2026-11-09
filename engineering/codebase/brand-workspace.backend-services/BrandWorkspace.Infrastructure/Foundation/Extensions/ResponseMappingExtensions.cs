using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Foundation.Extensions;

/// <summary>Extends Desk's handlers with response mapping: a persistence result becomes their response.</summary>
public static class ResponseMappingExtensions
{
    /// <summary>Answers the result's value, or carries its error.</summary>
    public static AppResult<T> ToAppResult<T>(this Result<T> result)
        where T : notnull
    {
        return result.Match(value => AppResult<T>.Ok(value), error => AppResult<T>.Fail(error));
    }

    /// <summary>Answers <paramref name="value"/> when the result succeeded, or carries its error.</summary>
    public static AppResult<T> ToAppResult<T>(this Result result, T value)
        where T : notnull
    {
        return result.Match(() => AppResult<T>.Ok(value), error => AppResult<T>.Fail(error));
    }
}
