using System.Security.Cryptography;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Services;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Validators;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.Trackers;

/// <summary>
/// Tracks the artifacts a preparation expects, checking and hashing each as it is claimed from the upload.
/// </summary>
/// <param name="files">The uploaded files by name.</param>
/// <param name="artifacts">The checks each artifact passes.</param>
public sealed class ReleaseArtifactTracker(
    IReadOnlyDictionary<string, byte[]> files,
    IDesignArtifactValidator artifacts)
{
    private readonly List<(string Name, string Sha256, string Type, byte[] Bytes)> stored = [];

    /// <summary>Gets the names of the artifacts claimed so far.</summary>
    public HashSet<string> Expected { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets each claimed artifact's SHA-256 by name.</summary>
    public Dictionary<string, string> Links { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets the claimed artifacts, in claim order.</summary>
    public IReadOnlyList<(string Name, string Sha256, string Type, byte[] Bytes)> Stored
    {
        get
        {
            return stored;
        }
    }

    /// <summary>Claims a PNG page preview of <paramref name="ratio"/>.</summary>
    /// <param name="name">The artifact's name.</param>
    /// <param name="max">The most bytes it may hold.</param>
    /// <param name="ratio">The width-to-height ratio the saved page declares.</param>
    public void AddPng(
        string name,
        int max,
        double ratio)
    {
        var bytes = Claim(name, max);
        artifacts.ValidatePng(bytes, ratio);
        Keep(
            name,
            "image/png",
            bytes);
    }

    /// <summary>Claims a PDF of <paramref name="pages"/>.</summary>
    /// <param name="name">The artifact's name.</param>
    /// <param name="max">The most bytes it may hold.</param>
    /// <param name="pages">The page sizes the saved design declares.</param>
    public void AddPdf(
        string name,
        int max,
        IReadOnlyList<CataloguePdfPageSize> pages)
    {
        var bytes = Claim(name, max);
        artifacts.ValidatePdf(bytes, pages);
        Keep(
            name,
            "application/pdf",
            bytes);
    }

    private byte[] Claim(string name, int max)
    {
        Expected.Add(name);
        CatalogueDesignValidator.Require(
            files.TryGetValue(name, out var bytes) && bytes.Length > 0 && bytes.Length <= max,
            $"Missing or oversized catalogue artifact: {name}.");
        return bytes!;
    }

    private void Keep(
        string name,
        string type,
        byte[] bytes)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        stored.Add((name, hash, type, bytes));
        Links[name] = hash;
    }
}
