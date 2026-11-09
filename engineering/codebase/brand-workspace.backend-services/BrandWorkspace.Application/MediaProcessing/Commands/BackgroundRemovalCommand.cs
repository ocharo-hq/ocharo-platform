using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.MediaProcessing.Commands;

/// <summary>
/// Represents a command to remove one image's background: no source URL, storage, approval or publication side effects.
/// </summary>
public sealed record BackgroundRemovalCommand : ICommand<AppResult<BackgroundRemovalResult>>
{
    /// <summary>Gets the image's bytes.</summary>
    public required byte[] Bytes { get; init; }

    /// <summary>Gets the image's declared content type.</summary>
    public required string ContentType { get; init; }
}
