namespace BrandWorkspace.Api.DesignDocuments.Requests;

/// <summary>Archives a document only when the persisted revision still matches.</summary>
public sealed record ArchiveDesignDocumentRequest(int ExpectedRevision);
