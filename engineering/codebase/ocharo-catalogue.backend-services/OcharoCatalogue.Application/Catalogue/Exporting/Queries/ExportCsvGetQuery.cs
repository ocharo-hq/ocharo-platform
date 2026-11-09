using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Exporting.Queries;

/// <summary>Represents a query to get the CSV price sheet.</summary>
public sealed record ExportCsvGetQuery : IQuery<AppResult<byte[]>>;
