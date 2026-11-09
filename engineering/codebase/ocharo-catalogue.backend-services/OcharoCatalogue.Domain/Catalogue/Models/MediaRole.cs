namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>What an image shows; the first cutout is the default catalogue image.</summary>
public static class MediaRole
{
    public const string Cutout = "cutout";
    public const string Model = "model";
    public const string Reference = "reference";
    public const string Photo = "photo";
    public const string Detail = "detail";
    public const string Fabric = "fabric";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Cutout, Model, Reference, Photo, Detail, Fabric };
}
