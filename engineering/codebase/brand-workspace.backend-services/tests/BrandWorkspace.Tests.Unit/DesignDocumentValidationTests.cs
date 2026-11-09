using BrandWorkspace.Application.Foundation.Constants;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

/// <summary>Checks the bounded JSON trust boundary before editor snapshots enter persistence.</summary>
public sealed class DesignDocumentValidationTests
{
    private readonly DesignDocumentValidationService _validation = new();

    [Theory]
    [InlineData("catalogue")]
    [InlineData("business-card")]
    public void ValidateKind_ShouldAcceptOnlySupportedEditorKinds(string kind)
    {
        _validation.ValidateKind(kind).Should().Be(kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Catalogue")]
    [InlineData("../../private")]
    public void ValidateKind_ShouldRejectUnknownKinds(string? kind)
    {
        Action action = () => _validation.ValidateKind(kind);
        action.Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
    }

    [Fact]
    public void ValidateInput_ShouldDetachValidSnapshotsAndKeepUnicodeAndNestedContent()
    {
        DesignDocumentInput result;
        using (JsonDocument source = JsonDocument.Parse(
            """{"version":1,"text":"O‘zbekcha\nSecond line","pages":[{"width":210.5,"visible":true}]}"""))
        {
            result = _validation.ValidateInput(new DesignDocumentInput("  Catalogue  ", source.RootElement));
        }
        result.Title.Should().Be("Catalogue");
        result.Snapshot.GetProperty("text").GetString().Should().Be("O‘zbekcha\nSecond line");
        result.Snapshot.GetProperty("pages")[0].GetProperty("width").GetDecimal().Should().Be(210.5m);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"field\":1,\"field\":2}")]
    [InlineData("{\"nested\":{\"field\":1,\"field\":2}}")]
    [InlineData("{\"__proto__\":{}}")]
    [InlineData("{\"text\":\"\\u0000\"}")]
    [InlineData("{\"number\":1e1000}")]
    [InlineData("{\"number\":1000000000001}")]
    public void ValidateInput_ShouldRejectAmbiguousOrUnsupportedJson(string json)
    {
        using JsonDocument source = JsonDocument.Parse(json);
        Action action = () => _validation.ValidateInput(new DesignDocumentInput("Title", source.RootElement));
        action.Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Title\nInjected")]
    public void ValidateInput_ShouldRejectMissingOrControlCharacterTitles(string? title)
    {
        JsonElement snapshot = JsonSerializer.SerializeToElement(new { version = 1 });
        Action action = () => _validation.ValidateInput(new DesignDocumentInput(title, snapshot));
        action.Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
    }

    [Fact]
    public void ValidateInput_ShouldBoundTitleStringsCollectionsDepthAndTotalBytes()
    {
        JsonElement empty = JsonSerializer.SerializeToElement(new { });
        Action longTitle = () => _validation.ValidateInput(new DesignDocumentInput(new string('x', 161), empty));
        longTitle.Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
        JsonElement longText = JsonSerializer.SerializeToElement(new { text = new string('x', 65537) });
        Action text = () => _validation.ValidateInput(new DesignDocumentInput("Title", longText));
        text.Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
        JsonElement manyItems = JsonSerializer.SerializeToElement(new { items = new int[2001] });
        Action collection = () => _validation.ValidateInput(new DesignDocumentInput("Title", manyItems));
        collection.Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
        string deep = string.Concat(Enumerable.Repeat("{\"n\":", 33)) + "0" + new string('}', 33);
        using JsonDocument nested = JsonDocument.Parse(deep);
        Action depth = () => _validation.ValidateInput(new DesignDocumentInput("Title", nested.RootElement));
        depth.Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
        JsonElement large = JsonSerializer.SerializeToElement(new
        {
            texts = Enumerable.Repeat(new string('x', 65536), 17),
        });
        Action total = () => _validation.ValidateInput(new DesignDocumentInput("Title", large));
        total
            .Should()
            .Throw<AppException>().Which.Error.Metadata![DeskErrorReasons.Key]
            .Should()
            .Be(DeskErrorReasons.TooLarge);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public void ValidateRevision_ShouldRejectMissingNegativeAndOverflowingRevisions(int revision)
    {
        Action action = () => _validation.ValidateRevision(revision);
        action.Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
    }
}
