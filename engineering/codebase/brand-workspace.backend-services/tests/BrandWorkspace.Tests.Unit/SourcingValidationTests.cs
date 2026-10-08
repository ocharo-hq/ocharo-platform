using AwesomeAssertions;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

/// <summary>Verifies sourcing field normalization without I/O.</summary>
public sealed class SourcingValidationTests
{
    private readonly SourcingValidationService _validation = new();

    [Theory]
    [InlineData("whatsapp", "+91 (000) 000-0001", "+910000000001")]
    [InlineData("phone", "998 90 000 00 00", "998900000000")]
    [InlineData("email", " Sales@Example.TEST ", "sales@example.test")]
    [InlineData("website", "https://example.test/a?b=1", "https://example.test/a?b=1")]
    [InlineData("instagram", "@ocharo.example", "@ocharo.example")]
    public void Route_ShouldNormalizeDestinationPerChannel(string channel, string destination, string expected)
    {
        _validation.Route(channel, destination, null, null, false, false, null, null).Destination.Should().Be(expected);
    }

    [Theory]
    [InlineData("whatsapp", "call me")]
    [InlineData("email", "not-an-email")]
    [InlineData("form", "javascript:alert(1)")]
    [InlineData("marketplace", "ftp://example.test/listing")]
    [InlineData("carrier-pigeon", "anything")]
    public void Route_ShouldRejectUnusableDestinations(string channel, string destination)
    {
        Action act = () => _validation.Route(channel, destination, null, null, false, false, null, null);
        act.Should().Throw<PreparationException>().Which.Code.Should().Be("validation");
    }

    [Fact]
    public void Supplier_ShouldTrimBlankOptionalsToNull_AndUppercaseCountry()
    {
        var input = _validation.Supplier("  Kashweave ", null, "in", " ", "", null);
        input.Name.Should().Be("Kashweave");
        input.Stage.Should().Be("lead");
        input.Country.Should().Be("IN");
        input.City.Should().BeNull();
        input.Website.Should().BeNull();
        input.Notes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\photos\\rack:1.jpg", "rack_1.jpg")]
    [InlineData("..", "upload")]
    [InlineData("", "upload")]
    public void OriginalName_ShouldKeepOnlyASafeFinalSegment(string value, string expected)
    {
        _validation.OriginalName(value).Should().Be(expected);
    }

    [Fact]
    public void Key_ShouldAcceptOnlyLowercaseSlugs()
    {
        _validation.Key("lucky-alternate").Should().Be("lucky-alternate");
        Action act = () => _validation.Key("Lucky Alternate");
        act.Should().Throw<PreparationException>();
    }
}
