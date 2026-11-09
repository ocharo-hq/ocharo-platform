using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.Serializers;

namespace OcharoBrand.Infrastructure.Brand.Mappers;

/// <summary>Maps brand documents onto the rows that store them.</summary>
public static class BrandRecordMapper
{
    /// <summary>The row that stores an appearance, its window and status kept beside the document.</summary>
    public static AppearanceRecord ToRecord(BrandAppearance appearance)
    {
        return new AppearanceRecord
        {
            Id = appearance.Id,
            StartsAt = appearance.StartsAt,
            EndsAt = appearance.EndsAt,
            Status = appearance.Status,
            Json = BrandJson.Write(appearance),
        };
    }
}
