using OcharoCatalogue.Application.Catalogue.Collections.Commands;
using OcharoCatalogue.Application.Catalogue.Collections.Models;
using OcharoCatalogue.Application.Catalogue.Collections.Queries;

namespace OcharoCatalogue.Tests;

/// <summary>Collection commands and queries.</summary>
public sealed class TestCollections(CatalogueClient client)
{
    public Task<IReadOnlyList<CollectionDetail>> ListAsync(CancellationToken ct) => client.SendAsync(new CollectionGetAllQuery(), ct);
    public Task<CollectionDetail> GetAsync(string id, CancellationToken ct) => client.SendAsync(new CollectionGetByIdQuery { Id = id }, ct);
    public Task<CollectionDetail> SaveAsync(string? id, CollectionInput input, CancellationToken ct) => id is null
        ? client.SendAsync(new CollectionCreateCommand { Input = input }, ct)
        : client.SendAsync(new CollectionUpdateCommand { Id = id, Input = input }, ct);
    public Task<string> DeleteAsync(string id, CancellationToken ct) => client.SendAsync(new CollectionDeleteCommand { Id = id }, ct);
}
