using OcharoCatalogue.Application.Catalogue.Designs.Commands;

namespace OcharoCatalogue.Api.Catalogue.Designs.Requests;

/// <summary>Represents the body of a request to copy a catalogue design.</summary>
/// <param name="Title">The copy's title; derived from the source when empty.</param>
public sealed record DuplicateDesignApiRequest(string? Title);

/// <summary>Extends <see cref="DuplicateDesignApiRequest"/> with its mapping to the duplicate command.</summary>
public static class DuplicateDesignApiRequestExtensions
{
    /// <summary>Maps the request to the command copying design <paramref name="id"/>.</summary>
    /// <param name="request">The request body.</param>
    /// <param name="id">The design id from the route.</param>
    public static DesignDuplicateCommand ToCommand(this DuplicateDesignApiRequest request, Guid id)
    {
        return new DesignDuplicateCommand { Id = id, Title = request.Title };
    }
}
