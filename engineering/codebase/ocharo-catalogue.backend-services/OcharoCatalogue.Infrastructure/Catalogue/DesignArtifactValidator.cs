using SkiaSharp;
using UglyToad.PdfPig;
using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Services;
namespace OcharoCatalogue.Infrastructure.Catalogue;

public sealed class DesignArtifactValidator : IDesignArtifactValidator
{
    public void ValidatePdf(byte[] bytes,IReadOnlyList<CataloguePdfPageSize> expectedPages)
    {
        CatalogueDesignValidation.Require(bytes.Length>=16&&bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8)&&bytes.AsSpan(Math.Max(0,bytes.Length-1024)).IndexOf("%%EOF"u8)>=0,"Catalogue PDF is incomplete.");
        try
        {
            using var pdf=PdfDocument.Open(bytes,new ParsingOptions{UseLenientParsing=false});
            CatalogueDesignValidation.Require(!pdf.IsEncrypted&&expectedPages.Count is >0 and <=100&&pdf.NumberOfPages==expectedPages.Count,"PDF page count differs from the prepared catalogue.");
            for(var index=0;index<expectedPages.Count;index++)
            {
                var page=pdf.GetPage(index+1);var expected=expectedPages[index];
                CatalogueDesignValidation.Require(Math.Abs((double)page.Width*25.4/72-expected.WidthMm)<=1&&Math.Abs((double)page.Height*25.4/72-expected.HeightMm)<=1,"PDF page dimensions differ from the saved design.");
            }
        }
        catch(CatalogueException){throw;}
        catch(Exception exception) when(exception is not OutOfMemoryException && exception is not StackOverflowException)
        {throw new CatalogueException("Catalogue PDF could not be parsed. Render it again from the saved design.");}
    }
    public void ValidatePng(byte[] bytes,double ratio)
    {
        ReadOnlySpan<byte> end=[0,0,0,0,73,69,78,68,174,66,96,130];
        CatalogueDesignValidation.Require(bytes.Length>=end.Length && bytes.AsSpan(bytes.Length-end.Length).SequenceEqual(end),"Catalogue PNG has an invalid ending.");
        using var data=SKData.CreateCopy(bytes);using var codec=SKCodec.Create(data);
        CatalogueDesignValidation.Require(codec is not null && codec.EncodedFormat==SKEncodedImageFormat.Png,"Catalogue page previews must be PNG images.");
        var info=codec!.Info;
        CatalogueDesignValidation.Require(info.Width is >0 and <=8192 && info.Height is >0 and <=8192 && (long)info.Width*info.Height<=32_000_000 && codec.FrameCount<=1,"Catalogue previews must be single images up to 8192 pixels and 32 megapixels.");
        CatalogueDesignValidation.Require(Math.Abs((double)info.Width/info.Height/ratio-1)<=0.02,"Page preview proportions differ from the saved page dimensions.");
        using var decoded=new SKBitmap(new SKImageInfo(info.Width,info.Height,SKColorType.Rgba8888,SKAlphaType.Premul));
        CatalogueDesignValidation.Require(codec.GetPixels(decoded.Info,decoded.GetPixels())==SKCodecResult.Success,"Catalogue PNG could not be completely decoded.");
    }
}
