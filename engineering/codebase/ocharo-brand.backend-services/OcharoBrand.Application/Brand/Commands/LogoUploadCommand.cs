using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Commands;

/// <summary>Represents a command to upload a logo file as a new version of a slot.</summary>
public sealed record LogoUploadCommand : ICommand<AppResult<LogoVersion>>
{
    /// <summary>Gets the slot the file is for.</summary>
    public required string Slot { get; init; }

    /// <summary>Gets the file's bytes.</summary>
    public required byte[] Bytes { get; init; }

    /// <summary>Gets the uploaded file's name.</summary>
    public required string FileName { get; init; }

    /// <summary>Gets the uploader's note, if any.</summary>
    public string? Note { get; init; }

    /// <summary>Gets the seed file's path, recorded as provenance; none for an upload.</summary>
    public string? SourcePath { get; init; }

    /// <summary>Gets the status the new version starts in.</summary>
    public string Status { get; init; } = LogoStatus.Draft;
}
