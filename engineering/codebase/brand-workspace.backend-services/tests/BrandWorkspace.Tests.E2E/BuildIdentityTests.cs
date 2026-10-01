using BrandWorkspace.Api;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Prevents assembly source metadata from overflowing migration version stamps.</summary>
public sealed class BuildIdentityTests
{
    [Theory]
    [InlineData("dev+0123456789abcdef0123456789abcdef01234567", "dev")]
    [InlineData("0.1.7+0123456789abcdef0123456789abcdef01234567", "0.1.7")]
    [InlineData("0.1.8-feature-long-name.12+abcdef", "0.1.8-feature-long-name.12")]
    public void ProductVersion_PreservesPrereleaseAndDropsOnlyBuildMetadata(string informational, string product)
        => Assert.Equal(product, BuildIdentity.ProductVersionFrom(informational));
}
