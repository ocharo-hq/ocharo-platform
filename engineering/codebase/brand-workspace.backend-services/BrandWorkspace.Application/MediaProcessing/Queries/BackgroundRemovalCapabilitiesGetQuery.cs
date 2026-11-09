using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.MediaProcessing.Queries;

/// <summary>Represents a query to report whether the image worker removes backgrounds.</summary>
public sealed record BackgroundRemovalCapabilitiesGetQuery : IQuery<AppResult<BackgroundRemovalStatus>>
{
}
