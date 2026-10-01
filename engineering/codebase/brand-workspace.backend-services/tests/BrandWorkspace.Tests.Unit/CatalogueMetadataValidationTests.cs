using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

/// <summary>Exercises multilingual design metadata while preserving unbound legacy snapshots.</summary>
public sealed class CatalogueMetadataValidationTests
{
    private readonly DesignDocumentValidationService _validation = new();
    private static JsonObject Snapshot() => JsonNode.Parse("""
        {"kind":"catalogue","locale":"fr","catalogueSettings":{"mediaGroup":"model","priceDisplay":{"show":true,"showCurrency":true,"showQuantity":true,"showRetail":true,"showWholesale":false},"copyByLocale":{"fr":{"collection":"Collection","coverNote":"","header":"Ocharo","footer":"","sizes":"Tailles","pieces":"pièces","priceOnRequest":"Sur demande","prices":"Prix","materials":"Tissus","decoration":"Décoration"}}},"pages":[{"elements":[{"type":"text","productId":"nimcha-v01","binding":{"kind":"product","field":"price","index":0}}]}]}
        """)!.AsObject();
    private void Validate(JsonNode value) => _validation.ValidateInput(new("Catalogue", JsonSerializer.SerializeToElement(value)));

    [Fact]
    public void ValidSettings_PreserveIndependentFrenchCopyAndProductPriceBinding()
    {
        var source = Snapshot();
        var result = _validation.ValidateInput(new("Catalogue", JsonSerializer.SerializeToElement(source)));
        result.Snapshot.GetProperty("locale").GetString().Should().Be("fr");
        result.Snapshot.GetProperty("catalogueSettings").GetProperty("copyByLocale").GetProperty("fr").GetProperty("sizes").GetString().Should().Be("Tailles");
    }

    [Fact]
    public void LegacySnapshots_WithoutSettingsRemainValid()
    {
        Action action = () => Validate(JsonNode.Parse("""{"kind":"catalogue","locale":"uz","pages":[{"elements":[]}]}""")!);
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("locale")]
    [InlineData("copy")]
    [InlineData("long-copy")]
    [InlineData("group")]
    [InlineData("price-flag")]
    [InlineData("binding-index")]
    [InlineData("binding-field")]
    [InlineData("binding-product")]
    [InlineData("binding-type")]
    [InlineData("business-card")]
    public void UnsupportedMetadata_IsRejectedBeforePersistence(string fault)
    {
        var source = Snapshot();
        var settings = source["catalogueSettings"]!;
        var element = source["pages"]![0]!["elements"]![0]!;
        switch (fault)
        {
            case "locale": source["locale"] = "xx"; break;
            case "copy": settings["copyByLocale"]!["xx"] = settings["copyByLocale"]!["fr"]!.DeepClone(); break;
            case "long-copy": settings["copyByLocale"]!["fr"]!["header"] = new string('x', 1001); break;
            case "group": settings["mediaGroup"] = "other"; break;
            case "price-flag": settings["priceDisplay"]!["show"] = "true"; break;
            case "binding-index": element["binding"]!["index"] = -1; break;
            case "binding-field": element["binding"]!["field"] = "sourcePath"; break;
            case "binding-product": element.AsObject().Remove("productId"); break;
            case "binding-type": element["type"] = "image"; break;
            case "business-card": source["kind"] = "business-card"; break;
        }
        Action action = () => Validate(source);
        action.Should().Throw<PreparationException>();
    }

    [Fact]
    public void MultiProductPageBinding_CanNameItsOwnProduct()
    {
        var source = Snapshot(); var element = source["pages"]![0]!["elements"]![0]!;
        element.AsObject().Remove("productId"); element["binding"]!["productId"] = "nimcha-v02";
        Action action = () => Validate(source);
        action.Should().NotThrow();
    }
}
