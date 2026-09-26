using BrandWorkspace.Application.Preparation.Models;

namespace BrandWorkspace.Application.Preparation.Services;

/// <summary>Builds a bounded manual package without contacting external services.</summary>
public interface IManualPackageBuilder
{
    byte[] Build(PackageSnapshot snapshot);
}
