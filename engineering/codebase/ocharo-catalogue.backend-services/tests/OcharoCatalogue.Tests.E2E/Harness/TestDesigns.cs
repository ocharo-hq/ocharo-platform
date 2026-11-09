using OcharoCatalogue.Application.Catalogue.Designs.Commands;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Designs.Queries;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>Design commands and queries.</summary>
public sealed class TestDesigns(CatalogueClient client)
{
    public Task<IReadOnlyList<CatalogueDesignSummary>> ListAsync(bool archived, CancellationToken ct) => client.SendAsync(new DesignGetAllQuery { Archived = archived }, ct);
    public Task<CatalogueDesignDetail> GetAsync(Guid id, CancellationToken ct) => client.SendAsync(new DesignGetByIdQuery { Id = id }, ct);
    public Task<CatalogueDesignDetail> SaveAsync(Guid? id, CatalogueDesignInput input, CancellationToken ct) => id is null
        ? client.SendAsync(new DesignCreateCommand { Input = input }, ct)
        : client.SendAsync(new DesignUpdateCommand { Id = id.Value, Input = input }, ct);
    public Task<CatalogueDesignDetail> DuplicateAsync(Guid id, string? title, CancellationToken ct) => client.SendAsync(new DesignDuplicateCommand { Id = id, Title = title }, ct);
    public Task<Guid> ArchiveAsync(Guid id, int expectedRevision, CancellationToken ct) => client.SendAsync(new DesignArchiveCommand { Id = id, ExpectedRevision = expectedRevision }, ct);
    public Task<IReadOnlyList<DesignRevisionSummary>> RevisionsAsync(Guid id, CancellationToken ct) => client.SendAsync(new DesignRevisionGetAllQuery { Id = id }, ct);
    public Task<CatalogueDesignDetail> RevisionAsync(Guid id, int revision, CancellationToken ct) => client.SendAsync(new DesignRevisionGetByIdQuery { Id = id, Revision = revision }, ct);
}
