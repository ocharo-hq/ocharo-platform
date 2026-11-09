using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents a revision-checked change of a product's localized copy only.</summary>
public sealed record CopyInput(int ExpectedRevision, ProductLocales Locales);
