using System.Text.Json;
using System.Text.Json.Nodes;
using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Domain.Catalogue.Entities;
namespace OcharoCatalogue.Application.Catalogue.Services;

public sealed class CatalogueDesignService(IDesignRepository designs,ICatalogueRepository products,IClock clock)
{
    public async Task<IReadOnlyList<CatalogueDesignSummary>> ListAsync(bool archived,CancellationToken ct)=>(await designs.ListAsync(archived,ct)).Select(Summary).ToArray();
    public async Task<CatalogueDesignDetail> GetAsync(Guid id,CancellationToken ct)=>Detail(await designs.GetAsync(id,ct)??throw CatalogueException.NotFound("Catalogue design"));
    public async Task<CatalogueDesignDetail> SaveAsync(Guid? id,CatalogueDesignInput input,CancellationToken ct)
    {
        var snapshot=CatalogueDesignValidation.Validate(input);var definition=CatalogueDesignValidation.Definition(snapshot);
        var references=CatalogueDesignValidation.References(snapshot);var known=await products.GetProductsAsync(ct);
        CatalogueDesignValidation.Require(references.All(id=>known.Any(p=>p.Id==id)),"Catalogue pages must reference existing products.");
        foreach(var hash in CatalogueDesignValidation.ImageHashes(snapshot))CatalogueDesignValidation.Require(await products.FindMediaAsync(hash,ct) is not null&&known.Any(row=>CatalogueJson.Read<OcharoCatalogue.Domain.Catalogue.Models.ProductDocument>(row.Json).Media.Any(image=>image.Sha256==hash)),"Catalogue images must belong to the product media library.");
        if(id is not null&&input.ExpectedRevision is null)throw new CatalogueException("Send the catalogue revision you edited.");
        var now=clock.UtcNow;var saved=await designs.SaveAsync(new(){Id=id??Guid.NewGuid(),Title=input.Title.Trim(),Slug=definition.Slug,Locale=snapshot.GetProperty("locale").GetString()!,Json=snapshot.GetRawText(),CreatedAtUtc=now,UpdatedAtUtc=now},id is null?null:input.ExpectedRevision,ct);return Detail(saved);
    }
    public async Task<CatalogueDesignDetail> DuplicateAsync(Guid id,string? title,CancellationToken ct)
    {
        var source=await GetAsync(id,ct);var node=JsonNode.Parse(source.Snapshot.GetRawText())!;
        node["catalogue"]!["isActive"]=false;
        var old=node["catalogue"]!["slug"]!.GetValue<string>();node["catalogue"]!["slug"]=old[..Math.Min(old.Length,80)].TrimEnd('-')+"-copy-"+Guid.NewGuid().ToString("N")[..8];
        var newTitle=string.IsNullOrWhiteSpace(title)?source.Title[..Math.Min(source.Title.Length,150)]+" (copy)":title;
        return await SaveAsync(null,new(newTitle,JsonSerializer.SerializeToElement(node,CatalogueJson.Options)),ct);
    }
    public Task ArchiveAsync(Guid id,int expected,CancellationToken ct)=>designs.ArchiveAsync(id,expected,clock.UtcNow,ct);
    public async Task<object> RevisionsAsync(Guid id,CancellationToken ct){_=await GetAsync(id,ct);return(await designs.RevisionsAsync(id,ct)).Select(r=>new {id=r.DesignId,r.Revision,r.Title,r.SavedAtUtc}).ToArray();}
    public async Task<CatalogueDesignDetail> RevisionAsync(Guid id,int revision,CancellationToken ct)
    {
        var current=await designs.GetAsync(id,ct)??throw CatalogueException.NotFound("Catalogue design");var r=await designs.RevisionAsync(id,revision,ct)??throw CatalogueException.NotFound("Catalogue revision");
        return new(id,r.Title,r.Revision,current.Archived,current.CreatedAtUtc,r.SavedAtUtc,CatalogueJson.Read<JsonElement>(r.Json));
    }
    public static CatalogueDesignDetail Detail(CatalogueDesignRecord r)=>new(r.Id,r.Title,r.Revision,r.Archived,r.CreatedAtUtc,r.UpdatedAtUtc,CatalogueJson.Read<JsonElement>(r.Json));
    public static CatalogueDesignSummary Summary(CatalogueDesignRecord r)
    {
        var snapshot=CatalogueJson.Read<JsonElement>(r.Json);var d=CatalogueDesignValidation.Definition(snapshot);
        return new(r.Id,r.Title,r.Revision,r.Archived,r.CreatedAtUtc,r.UpdatedAtUtc,r.Locale,new(d.Slug,d.Description,d.IsActive,d.Outputs,new(d.Theme,d.WebPages.Count(p=>p.IsActive)),snapshot.GetProperty("pages").EnumerateArray().Count(CatalogueDesignValidation.Active)));
    }
}
