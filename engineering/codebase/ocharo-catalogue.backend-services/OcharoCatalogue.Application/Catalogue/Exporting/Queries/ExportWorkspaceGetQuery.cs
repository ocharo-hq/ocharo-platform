using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Exporting.Queries;

/// <summary>Represents a query to get a ZIP of per-product files in the workspace product schema.</summary>
public sealed record ExportWorkspaceGetQuery : IQuery<AppResult<byte[]>>;
