using System.IO.Compression;
using System.Text.Json;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Exporting.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Exporting.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Exporting.QueryHandlers;

/// <summary>Handles <see cref="ExportWorkspaceGetQuery"/>.</summary>
public sealed class ExportWorkspaceGetQueryHandler(ICatalogueRepository repository)
    : IQueryHandler<ExportWorkspaceGetQuery, AppResult<byte[]>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<byte[]>> HandleAsync(ExportWorkspaceGetQuery request, CancellationToken ct)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(
            output,
            ZipArchiveMode.Create,
            leaveOpen: true))
        {
            var records = (await repository.GetProductsAsync(ct)).OrderBy(record => record.Id, StringComparer.Ordinal);
            foreach (var record in records)
            {
                var document = CatalogueJson.Read<ProductDocument>(record.Json);
                var entry = zip.CreateEntry($"{document.Id}/och-{document.Id}.json", CompressionLevel.Optimal);
                entry.LastWriteTime = record.UpdatedAt;
                await using var stream = entry.Open();
                await JsonSerializer.SerializeAsync(
                    stream,
                    WorkspaceSchemaMapper.ToWorkspaceSchema(document, record.UpdatedAt),
                    CatalogueJson.Indented,
                    ct);
            }
        }

        return AppResult<byte[]>.Ok(output.ToArray());
    }
}
