namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents a published edition's PDF output: the whole document and its pages.</summary>
public sealed record PublishedEditionPdf(string PdfUrl, IReadOnlyList<PublishedEditionPage> Pages);
