using BrandWorkspace.Domain.Commerce;

namespace BrandWorkspace.Api.Commerce.Models;

/// <summary>Represents a brand's edited commerce draft at the revision the operator saw.</summary>
public sealed record CommerceSaveRequest(int ExpectedRevision, CommerceSnapshot Snapshot);
