using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Preparation.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Preparation.Commands;

/// <summary>Represents a command to store one validated preparation image and its provenance.</summary>
public sealed record PreparationMediaUploadCommand : ICommand<AppResult<MediaAsset>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the image's original name.</summary>
    public required string FileName { get; init; }

    /// <summary>Gets the image's provenance.</summary>
    public required string Provenance { get; init; }

    /// <summary>Gets the image's bytes.</summary>
    public required byte[] Bytes { get; init; }
}
