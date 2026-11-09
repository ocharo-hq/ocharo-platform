using System.Text.Json;
using System.Text.Json.Nodes;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Designs.Commands;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.CommandHandlers;

/// <summary>Handles <see cref="DesignDuplicateCommand"/>.</summary>
public sealed class DesignDuplicateCommandHandler(IDesignRepository designs, DesignSaveService saves)
    : ICommandHandler<DesignDuplicateCommand, AppResult<CatalogueDesignDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CatalogueDesignDetail>> HandleAsync(
        DesignDuplicateCommand request,
        CancellationToken ct)
    {
        var record = await designs.GetAsync(request.Id, ct);
        if (record is null)
        {
            return AppResult<CatalogueDesignDetail>.Fail(CatalogueErrors.NotFound("Catalogue design"));
        }

        var source = DesignProjections.ToDetail(record);
        var node = JsonNode.Parse(source.Snapshot.GetRawText())!;
        node["catalogue"]!["isActive"] = false;
        var old = node["catalogue"]!["slug"]!.GetValue<string>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        node["catalogue"]!["slug"] = old[..Math.Min(old.Length, 80)].TrimEnd('-') + "-copy-" + suffix;
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? source.Title[..Math.Min(source.Title.Length, 150)] + " (copy)"
            : request.Title;
        var input = new CatalogueDesignInput(title, JsonSerializer.SerializeToElement(node, CatalogueJson.Options));
        return await saves.SaveAsync(
            null,
            input,
            ct);
    }
}
