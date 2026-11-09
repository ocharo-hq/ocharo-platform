using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.Extensions;

/// <summary>Carries an inner <see cref="Result{T}"/> across the handler boundary.</summary>
public static class BrandResultExtensions
{
    /// <summary>The handler's <see cref="AppResult{TSuccess}"/> for an inner result: its value, or its error.</summary>
    public static AppResult<T> ToAppResult<T>(this Result<T> result) where T : notnull
    {
        return result.Match(value => AppResult<T>.Ok(value), error => AppResult<T>.Fail(error));
    }
}
