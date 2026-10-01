using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Application.Commerce;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Persistence.Commerce;
using OcharoBrand.Domain.Brand;
using OcharoCatalogue.Domain.Catalogue.Models;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

public sealed class CommerceSerializationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CreativeInput_PreservesLocalAndSavedRevisionEvidence(int revision)
    {
        var input = Input(revision);
        CommerceRules.ValidateCreativeInput(input);
        input.Source.Revision.Should().Be(revision);
    }
    [Fact]
    public void CreativeInput_RejectsNegativeRevision()
    {
        var validate = () => CommerceRules.ValidateCreativeInput(Input(-1));
        validate.Should().Throw<BrandWorkspace.Application.Preparation.Models.PreparationException>();
    }
    private static CreativeImportInput Input(int revision) => new(Guid.NewGuid(),null,
        new("ocharo-studio","workspace","look",revision),[],[],new(Guid.NewGuid(),new string('a',64),"image/png"),null);

    [Fact]
    public void Commerce_RoundtripPreservesSizeIdentifiersForPublication()
    {
        var product = new ProductDocument { Id="garment-v01",GarmentType="garment",VariantCode="v01",Status="active",Sizes=["M","XL"],
            Stock=new Dictionary<string,int>{{"M",7},{"XL",2}},Locales=new(ProductLocale.Empty(),ProductLocale.Empty(),new("approved","Garment",null,null,null)) };
        var snapshot = new CommerceSnapshot { Slug="brand",Identity=new BrandIdentity{Name="Brand"},Products=[new(product)] };
        var stored = JsonSerializer.Serialize(snapshot,CommerceJson.Options);
        var restored = JsonSerializer.Deserialize<CommerceSnapshot>(stored,CommerceJson.Options)!;
        restored.Products[0].Document.Stock!.Keys.Should().BeEquivalentTo("M","XL");
        var validate = () => CommerceRules.Validate(restored);
        validate.Should().NotThrow();
        var response = JsonSerializer.SerializeToElement(new {data=new CommerceDraft(Guid.NewGuid(),1,restored,DateTimeOffset.UtcNow)},CommerceJson.Options);
        response.GetProperty("data").GetProperty("snapshot").GetProperty("products")[0].GetProperty("document").GetProperty("stock").GetProperty("M").GetInt32().Should().Be(7);
        var release = new CommerceRelease(Guid.NewGuid(),Guid.NewGuid(),1,1,DateTimeOffset.UtcNow,"Reviewed",restored);
        var projection = JsonSerializer.SerializeToElement(PublicProjection.Catalogue(release,"en"));
        projection.GetProperty("products")[0].GetProperty("availability").GetString().Should().Be("in-stock");
    }
}
