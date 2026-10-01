using BrandWorkspace.Domain.Commerce;
namespace BrandWorkspace.Application.Commerce;

/// <summary>Explicit buyer DTOs; private documents are never serialized as the public response.</summary>
public static class PublicProjection
{
    private static object Release(CommerceRelease r) => new { id = r.Id, revision = r.Revision, publishedAt = r.PublishedAt };
    private static string MediaUrl(CommerceRelease r, string sha) => $"/api/public/v1/brands/{Uri.EscapeDataString(r.Snapshot.Slug)}/media/{sha}?release={r.Id:D}";
    public static object Brand(CommerceRelease r) => new {
        schemaVersion = 1, brandId = r.BrandId, slug = r.Snapshot.Slug, release = Release(r),
        identity = new {
            name = r.Snapshot.Identity.Name, tagline = r.Snapshot.Identity.Tagline, description = r.Snapshot.Identity.Descriptor,
            colors = r.Snapshot.Identity.Palette,
            logos = r.Snapshot.Logos.Where(l => r.Snapshot.MediaApprovals.GetValueOrDefault(l.Value.Sha256)?.ApprovedForPublication == true)
                .ToDictionary(l => l.Key, l => new { url = MediaUrl(r,l.Value.Sha256), sha256 = l.Value.Sha256, contentType = l.Value.ContentType,
                    alt = r.Snapshot.Identity.Name, provenance = r.Snapshot.MediaApprovals[l.Value.Sha256] }),
            links = r.Snapshot.Identity.Links.Where(l => r.Snapshot.VerifiedLinkKeys.Contains(l.Key))
                .Select(l => new { key = l.Key, label = l.Label, handle = l.Handle, url = l.Url, verified = true })
        }
    };
    public static object Catalogue(CommerceRelease r, string locale)
    {
        CommerceRules.Require(CatalogueLanguages.Supported(locale), "Choose a supported catalogue language.");
        var allowed = r.Snapshot.Products.Where(p => ProductPublicationEligibility.Eligible(p,locale)).ToArray();
        var visibleEditions = r.Snapshot.CatalogueEditions.Where(e => e.ApprovedForPublication && CommerceRules.EditionEligible(e,r.Snapshot)).ToArray();
        return new {
            schemaVersion = 1, brandId = r.BrandId, slug = r.Snapshot.Slug, release = Release(r), locale,
            availableLocales = visibleEditions.Select(e => e.Locale).Distinct(),
            edition = Edition(r,visibleEditions.FirstOrDefault(e=>e.Slug is null && e.Locale==locale)),
            catalogues = visibleEditions.Where(e=>e.Slug is not null && e.Locale==locale).Select(e=>new {
                id=e.Id,slug=e.Slug,title=e.Title,description=e.Description,locale=e.Locale,updatedAt=e.UpdatedAt ?? e.CreatedAt,
                availableLocales=visibleEditions.Where(version=>version.Slug==e.Slug).Select(version=>version.Locale).Distinct(),
                web=e.Web is null?null:new {theme=e.Web.Theme,mediaGroup=e.Web.MediaGroup,priceDisplay=e.Web.PriceDisplay,copy=e.Web.Copy,
                    productMedia=e.Web.ProductMedia.Where(entry=>allowed.Any(product=>product.Document.Id==entry.Key)).ToDictionary(entry=>entry.Key,entry=>entry.Value.Where(hash=>r.Snapshot.MediaApprovals.GetValueOrDefault(hash)?.ApprovedForPublication==true && allowed.Any(product=>product.Document.Id==entry.Key && product.Document.Media.Any(image=>image.Sha256==hash && image.Role!="reference")))),pages=e.Web.Pages.Where(page=>page.IsActive).Select(page=>page with {ProductIds=page.ProductIds.Where(id=>allowed.Any(product=>product.Document.Id==id)).ToArray()}).Where(page=>page.ProductIds.Count>0)
                    .Select(page=>new {id=page.Id,title=page.Title,layout=page.Layout,productIds=page.ProductIds})},
                pdf=Edition(r,e)
            }),
            products = allowed.OrderBy(p => p.Document.SortOrder).ThenBy(p => p.Document.Id).Select(p => {
                var d = p.Document; var copy = CatalogueLanguages.Copy(p, locale);
                return new { id = d.Id, slug = d.Id, revision = p.Revision, title = copy.Name, description = copy.Description,
                    fabric = copy.Fabric, decoration = copy.Decoration, category = d.GarmentType,
                    collectionIds = r.Snapshot.Collections.Where(c => c.IsPublished && c.ProductIds.Contains(d.Id)).Select(c => c.Id),
                    sizes = d.Sizes, availability = d.Availability,
                    images = d.Media.Where(m => m.Role != "reference" && r.Snapshot.MediaApprovals.GetValueOrDefault(m.Sha256)?.ApprovedForPublication == true)
                        .Select(m => new { url = MediaUrl(r,m.Sha256), alt = m.Alt ?? copy.Name ?? "", sha256 = m.Sha256, role = m.Role, groupIds = (p.MediaGroups ?? []).Where(g => g.MediaIds.Contains(m.Id)).Select(g => g.Id), provenance = r.Snapshot.MediaApprovals[m.Sha256] }),
                    pricing = d.Pricing is { Status: "confirmed" } confirmed ? new {currency=confirmed.Currency,tiers=confirmed.Tiers.Select(tier=>new{minimumQuantity=tier.MinimumQuantity,maximumQuantity=tier.MaximumQuantity,unitPrice=tier.UnitPrice})} : null,
                    price = d.Pricing is { Status: "confirmed", RetailPrice: {} price } ? new { amount = price, currency = d.Pricing.Currency } : null
                };
            }),
            collections = r.Snapshot.Collections.Where(c => c.IsPublished && CatalogueLanguages.Core(locale)).OrderBy(c => c.SortOrder).Select(c => new {
                id = c.Id, title = locale switch { "ru" => c.Names.Ru ?? c.Names.Uz, "en" => c.Names.En ?? c.Names.Uz, _ => c.Names.Uz },
                description = c.Description, productIds = c.ProductIds.Where(id => allowed.Any(p => p.Document.Id == id)) })
        };
    }
    private static object? Edition(CommerceRelease release,CatalogueEdition? edition)
    {
        if(edition is null || !CommerceRules.PdfEligible(edition,release.Snapshot))return null;
        string Url(string asset)=>$"/api/public/v1/brands/{Uri.EscapeDataString(release.Snapshot.Slug)}/catalogue-editions/{edition.Id:D}/{Uri.EscapeDataString(asset)}?release={release.Id:D}";
        return new {id=edition.Id,brandId=release.BrandId,sourceDesignId=edition.SourceDesignId,sourceRevision=edition.SourceRevision,
            title=edition.Title,locale=edition.Locale,templateId=edition.TemplateId,pageCount=edition.Pages.Count,releasedAt=release.PublishedAt,
            pdfUrl=Url("catalogue.pdf"),pages=edition.Pages.Select((p,index)=>new{id=p.Id,number=index+1,title=p.Title,widthMm=p.WidthMm,heightMm=p.HeightMm,
                productIds=p.ProductIds,imageUrl=Url($"{p.Id}.png"),pdfUrl=Url($"{p.Id}.pdf"),pngUrl=Url($"{p.Id}.png")})};
    }

}
