namespace OcharoCatalogue.Application.Catalogue.Core.Services;

/// <summary>Defines the facts about the request's caller that catalogue reads decide on.</summary>
public interface ICatalogueCaller
{
    /// <summary>
    /// Gets whether the caller is signed in as a member of the organization this catalogue belongs to.
    /// </summary>
    bool IsMember { get; }
}
