namespace BrandWorkspace.Application.Files.Models;

/// <summary>
/// Where one route's bytes come from: a short-lived Files link to redirect to, or bytes to answer with.
/// </summary>
public sealed record StoredContent(string ContentType, Uri? Location, byte[]? Bytes);
