using OcharoCatalogue.Application.Catalogue.Settings.Commands;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Api.Catalogue.Settings.Requests;

/// <summary>Represents the body of a request to replace the catalogue settings.</summary>
/// <param name="Settings">The new settings.</param>
/// <param name="ExpectedRevision">The revision the caller edited; 0 before the first save.</param>
public sealed record UpdateSettingsApiRequest(CatalogueSettings Settings, int ExpectedRevision);

/// <summary>Extends <see cref="UpdateSettingsApiRequest"/> with its mapping to the settings command.</summary>
public static class UpdateSettingsApiRequestExtensions
{
    /// <summary>Maps the request to the command replacing the settings.</summary>
    /// <param name="request">The request body.</param>
    public static SettingsUpdateCommand ToCommand(this UpdateSettingsApiRequest request)
    {
        return new SettingsUpdateCommand { Settings = request.Settings, ExpectedRevision = request.ExpectedRevision };
    }
}
