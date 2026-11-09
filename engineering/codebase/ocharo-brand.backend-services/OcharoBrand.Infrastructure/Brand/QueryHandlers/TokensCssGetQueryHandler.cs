using System.Text;
using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="TokensCssGetQuery"/>: the latest kit's palette as <c>--ocharo-{key}</c> plus the two type stacks.</summary>
public sealed class TokensCssGetQueryHandler(PublicBrandService kit) : IQueryHandler<TokensCssGetQuery, AppResult<string>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<string>> HandleAsync(TokensCssGetQuery request, CancellationToken ct)
    {
        var built = await kit.BuildAsync(null, null, ct);
        return built.Match(brand =>
        {
            var css = new StringBuilder($"/* {brand.Name} brand tokens · release {brand.Release} */\n:root {{\n");
            foreach (var color in brand.Palette) css.Append($"  --ocharo-{color.Key}: {color.Hex};\n");
            css.Append($"  --ocharo-font-display: {Stack(brand.Typography.Display)}, Georgia, serif;\n");
            css.Append($"  --ocharo-font-body: {Stack(brand.Typography.Body)}, Arial, sans-serif;\n");
            css.Append("}\n");
            return AppResult<string>.Ok(css.ToString());
        }, error => AppResult<string>.Fail(error));

        static string Stack(string font) => font.Contains(' ') ? $"\"{font.Replace("\"", "")}\"" : font;
    }
}
