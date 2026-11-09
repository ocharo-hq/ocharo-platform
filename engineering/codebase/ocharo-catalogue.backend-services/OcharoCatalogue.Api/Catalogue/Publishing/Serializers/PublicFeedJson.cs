using System.Text.Json;

namespace OcharoCatalogue.Api.Catalogue.Publishing.Serializers;

/// <summary>Holds the JSON options of the public feed's frozen wire shape: camelCase with null fields written, the
/// shape
/// Marketing and Blog read before the SDK preset, which omits nulls.</summary>
/// <remarks>Marketing's catalogue sync reads <c>pricing</c> and its <c>retail</c> without checking they exist, so an
/// omitted null would fail its sync; the feed keeps this shape until its consumers move together.</remarks>
public static class PublicFeedJson
{
    /// <summary>Holds the ASP.NET web defaults the feed was served with.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
