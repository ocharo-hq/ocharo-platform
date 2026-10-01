namespace OcharoCatalogue.Application.Catalogue.Services;
public sealed record CataloguePdfPageSize(double WidthMm,double HeightMm);
public interface IDesignArtifactValidator
{
    void ValidatePdf(byte[] bytes,IReadOnlyList<CataloguePdfPageSize> expectedPages);
    void ValidatePng(byte[] bytes, double ratio);
}
