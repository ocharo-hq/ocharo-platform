using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Exporting.Queries;

/// <summary>Represents a query to get the full JSON backup of the catalogue.</summary>
public sealed record ExportJsonGetQuery : IQuery<AppResult<byte[]>>;
