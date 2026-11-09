using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Commands;

/// <summary>Represents a command to store one supplier file once per organization.</summary>
public sealed record SourcingMediaUploadCommand : ICommand<AppResult<SourcingMediaUpload>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the file's original name.</summary>
    public string? FileName { get; init; }

    /// <summary>Gets the file's bytes.</summary>
    public required byte[] Bytes { get; init; }

    /// <summary>Gets the supplier the file came from; none puts it in the inbox.</summary>
    public Guid? SupplierId { get; init; }

    /// <summary>Gets how the file was captured.</summary>
    public string? Capture { get; init; }

    /// <summary>Gets the caption.</summary>
    public string? Caption { get; init; }
}
