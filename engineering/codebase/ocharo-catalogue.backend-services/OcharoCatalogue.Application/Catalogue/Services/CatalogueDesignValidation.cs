using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Validates portable local catalogue snapshots without resolving remote URLs or file paths.</summary>
public static partial class CatalogueDesignValidation
{
    public static IReadOnlyList<string> Languages=>CatalogueLocales.All;
    public static readonly string[] CopyFields=["collection","coverNote","header","footer","sizes","pieces","priceOnRequest","prices","materials","decoration"];
    public static readonly string[] PriceFields=["show","showCurrency","showQuantity","showRetail","showWholesale"];
    public static readonly string[] MediaGroups=["all","model","product","detail","fabric"];
    private static readonly string[] Reserved=["api","admin","edit","new","pages","editions","collections","products","catalogue","catalogues","assets","media","download","downloads","settings","login","logout"];
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")] private static partial Regex SlugPattern();
    [GeneratedRegex("^/api/catalogue/media/([a-f0-9]{64})(?:/preview)?$")] private static partial Regex MediaPattern();
    [GeneratedRegex("^#[a-fA-F0-9]{6}$")] private static partial Regex ColorPattern();
    public static bool ValidSlug(string value)=>value.Length is >0 and <=100 && SlugPattern().IsMatch(value)&&!Reserved.Contains(value);
    public static bool SafeTemplate(string source)=>source.StartsWith("/templates/catalogue-original/assets/",StringComparison.Ordinal)&&!source.Split('/').Any(part=>part is "." or "..")&&source.All(c=>char.IsAsciiLetterOrDigit(c)||c is '/' or '-' or '_' or '.')&&new[]{".png",".jpg",".jpeg",".webp",".svg"}.Any(ext=>source.EndsWith(ext,StringComparison.OrdinalIgnoreCase));
    public static string? ImageHash(string source)=>MediaPattern().Match(source) is {Success:true} match ? match.Groups[1].Value : null;
    public static void Require(bool valid,string message){if(!valid)throw new CatalogueException(message);}
    public static string Text(JsonElement v,string key,int max=160,bool empty=false)
    {
        Require(v.ValueKind==JsonValueKind.Object&&v.TryGetProperty(key,out var x)&&x.ValueKind==JsonValueKind.String,$"Catalogue {key} must be text.");
        var result=v.GetProperty(key).GetString()!;
        Require(result.Length<=max&&(empty||!string.IsNullOrWhiteSpace(result))&&!result.Any(c=>c=='\0'||!empty&&char.IsControl(c)),$"Catalogue {key} is invalid.");return result;
    }
    public static double Number(JsonElement v,string key,double min,double max)
    {
        Require(v.TryGetProperty(key,out var x)&&x.ValueKind==JsonValueKind.Number&&x.TryGetDouble(out var n)&&double.IsFinite(n)&&n>=min&&n<=max,$"Catalogue {key} is invalid.");return v.GetProperty(key).GetDouble();
    }
    public static bool Flag(JsonElement v,string key)=>v.TryGetProperty(key,out var x)&&x.ValueKind is JsonValueKind.True or JsonValueKind.False ? x.GetBoolean():throw new CatalogueException($"Catalogue {key} must be a boolean.");
    public static bool Active(JsonElement page)=>!page.TryGetProperty("isActive",out var active)||active.ValueKind==JsonValueKind.True;
    public static CatalogueDefinition Definition(JsonElement snapshot)
    {
        Require(snapshot.TryGetProperty("catalogue",out var c)&&c.ValueKind==JsonValueKind.Object,"Catalogue metadata is required.");
        var slug=Text(c,"slug",100);Require(ValidSlug(slug),"Choose an available lowercase catalogue slug.");
        var description=Text(c,"description",2000,true);var active=Flag(c,"isActive");
        Require(c.TryGetProperty("outputs",out var o)&&o.ValueKind==JsonValueKind.Object,"Choose catalogue outputs.");
        var outputs=new CatalogueOutputs(Flag(o,"web"),Flag(o,"pdf"));Require(outputs.Web||outputs.Pdf,"Enable a web or PDF output.");
        Require(c.TryGetProperty("web",out var web)&&web.ValueKind==JsonValueKind.Object,"Web settings are required.");
        var theme=Text(web,"theme",20);Require(theme is "editorial" or "grid","Choose a supported web theme.");
        Require(web.TryGetProperty("pages",out var pages)&&pages.ValueKind==JsonValueKind.Array&&pages.GetArrayLength()<=100,"Web pages must be an array of up to 100 items.");
        var result=new List<CatalogueWebPage>();var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var page in pages.EnumerateArray())
        {
            var id=Text(page,"id",100);Require(SafeId(id)&&ids.Add(id),"Page IDs must be safe and distinct.");
            var title=Text(page,"title");var layout=Text(page,"layout",20);Require(layout is "spread" or "grid" or "collage","Choose a supported web layout.");
            var products=ProductArray(page,"productIds",1000);result.Add(new(id,title,layout,Flag(page,"isActive"),products));
        }
        return new(slug,description,active,outputs,theme,result);
    }
    public static JsonElement Validate(CatalogueDesignInput input)
    {
        Require(!string.IsNullOrWhiteSpace(input.Title)&&input.Title.Length<=160&&!input.Title.Any(char.IsControl),"Title must contain 1 to 160 characters.");
        var s=input.Snapshot;Require(s.ValueKind==JsonValueKind.Object,"Snapshot must be an object.");
        Require(Encoding.UTF8.GetByteCount(s.GetRawText())<=1024*1024,"Snapshot exceeds 1 MiB.");
        var nodes=0;ValidateTree(s,0,ref nodes);
        Require(Number(s,"schemaVersion",1,1)==1&&Text(s,"kind")=="catalogue","Choose a version 1 catalogue snapshot.");
        Require(Languages.Contains(Text(s,"locale",20)),"Choose a supported catalogue language.");Text(s,"templateId",120);
        _=Definition(s);
        Require(s.TryGetProperty("pages",out var pages)&&pages.ValueKind==JsonValueKind.Array&&pages.GetArrayLength()<=100,"PDF pages must be an array of up to 100 items.");
        var ids=new HashSet<string>();
        foreach(var page in pages.EnumerateArray())
        {
            var id=Text(page,"id",100);Require(SafeId(id)&&ids.Add(id),"PDF page IDs must be safe and distinct.");Text(page,"title");
            Number(page,"width",10,1000);Number(page,"height",10,1000);Color(page,"background");
            if(page.TryGetProperty("isActive",out _))Flag(page,"isActive");
            if(page.TryGetProperty("productIds",out _))ProductArray(page,"productIds",300);
            Require(page.TryGetProperty("elements",out var elements)&&elements.ValueKind==JsonValueKind.Array&&elements.GetArrayLength()<=300,"PDF elements must be an array of up to 300 items.");
            var elementIds=new HashSet<string>();
            foreach(var e in elements.EnumerateArray())
            {
                var elementId=Text(e,"id",256);Require(elementIds.Add(elementId),"Element IDs must be distinct.");Text(e,"label",256);
                var type=Text(e,"type",20);Require(type is "text" or "image" or "shape","Choose a supported element type.");
                Number(e,"x",-1000,1000);Number(e,"y",-1000,1000);Number(e,"width",0.1,2000);Number(e,"height",0.1,2000);
                if(e.TryGetProperty("src",out _))Require(ImageHash(Text(e,"src",300)) is not null||SafeTemplate(Text(e,"src",300)),"Images must use uploaded catalogue media or the bundled original template.");
                foreach(var key in new[]{"color","background","borderColor"})if(e.TryGetProperty(key,out _))Color(e,key);
                if(e.TryGetProperty("text",out _))Text(e,"text",10000,true);
                if(e.TryGetProperty("direction",out _))Require(Text(e,"direction") is "ltr" or "rtl","Invalid text direction.");
                foreach(var entry in new[]{("fontSize",0.5,200.0),("lineHeight",0.5,4.0),("letterSpacing",-5.0,30.0),("cropX",0.0,100.0),("cropY",0.0,100.0),("zoom",1.0,5.0),("opacity",0.0,1.0),("rotation",-360.0,360.0),("radius",0.0,1000.0),("borderWidth",0.0,20.0)})if(e.TryGetProperty(entry.Item1,out _))Number(e,entry.Item1,entry.Item2,entry.Item3);
                if(e.TryGetProperty("binding",out var b))
                {
                    Require(type=="text"&&b.ValueKind==JsonValueKind.Object,"Only text supports field bindings.");var kind=Text(b,"kind",20);var field=Text(b,"field",30);
                    Require(kind=="shared"?CopyFields.Contains(field):kind=="product"&&new[]{"name","fabric","decoration","description","details","notes","sizes","price","price-label","currency"}.Contains(field),"Unknown catalogue binding.");
                    if(kind=="product")Require(b.TryGetProperty("productId",out _)||e.TryGetProperty("productId",out _),"Product bindings need a product.");
                    if(b.TryGetProperty("index",out _)||field is "price" or "price-label")Require(Number(b,"index",0,49)%1==0,"Price index must be an integer.");
                }
            }
        }
        if(s.TryGetProperty("catalogueSettings",out var settings))
        {
            Require(settings.ValueKind==JsonValueKind.Object,"Catalogue settings must be an object.");
            Require(MediaGroups.Contains(Text(settings,"mediaGroup",20)),"Choose a supported image group.");
            Require(settings.TryGetProperty("priceDisplay",out var display)&&display.ValueKind==JsonValueKind.Object,"Price display settings are required.");foreach(var key in PriceFields)Flag(display,key);
            Require(settings.TryGetProperty("copyByLocale",out var copies)&&copies.ValueKind==JsonValueKind.Object&&copies.EnumerateObject().Count()<=Languages.Count,"Catalogue copy must contain supported languages.");
            foreach(var item in copies.EnumerateObject()){Require(Languages.Contains(item.Name),"Unsupported copy language.");foreach(var key in CopyFields)Text(item.Value,key,1000,true);}
        }
        if(s.TryGetProperty("productRevisions",out var revisions))
        {
            Require(revisions.ValueKind==JsonValueKind.Object&&revisions.EnumerateObject().Count()<=1000,"Product revision evidence must be an object.");
            foreach(var entry in revisions.EnumerateObject())Require(SlugPattern().IsMatch(entry.Name)&&entry.Value.ValueKind==JsonValueKind.Number&&entry.Value.TryGetInt32(out var revision)&&revision>0,"Invalid product revision evidence.");
        }
        _=References(s);return s.Clone();
    }
    public static IReadOnlyList<string> References(JsonElement snapshot)=>Definition(snapshot).WebPages.SelectMany(p=>p.ProductIds).Concat(snapshot.GetProperty("pages").EnumerateArray().SelectMany(ReferencesOnPage)).Distinct(StringComparer.Ordinal).ToArray();
    public static IReadOnlyList<string> ReferencesOnPage(JsonElement page)
    {
        var ids=new HashSet<string>(StringComparer.Ordinal);
        void Add(JsonElement v,string key){if(v.TryGetProperty(key,out _)){var id=Text(v,key,120);Require(SlugPattern().IsMatch(id),"Invalid product reference.");ids.Add(id);}}
        if(page.TryGetProperty("productIds",out _))foreach(var id in ProductArray(page,"productIds",300))ids.Add(id);
        if(page.TryGetProperty("catalogueSource",out var source)){Require(source.ValueKind==JsonValueKind.Object,"Invalid catalogue source.");Add(source,"productId");}
        foreach(var e in page.GetProperty("elements").EnumerateArray()){Add(e,"productId");if(e.TryGetProperty("binding",out var b))Add(b,"productId");}
        return ids.ToArray();
    }
    public static IReadOnlyList<string> ImageHashes(JsonElement snapshot)=>snapshot.GetProperty("pages").EnumerateArray().SelectMany(p=>p.GetProperty("elements").EnumerateArray()).Where(e=>e.TryGetProperty("src",out _)).Select(e=>ImageHash(e.GetProperty("src").GetString()!)).Where(hash=>hash is not null).Select(hash=>hash!).Distinct().ToArray();
    private static IReadOnlyList<string> ProductArray(JsonElement value,string key,int max)
    {
        Require(value.TryGetProperty(key,out var a)&&a.ValueKind==JsonValueKind.Array&&a.GetArrayLength()<=max,"Product IDs must be an array.");
        var result=a.EnumerateArray().Select(e=>e.ValueKind==JsonValueKind.String?e.GetString()!:"").ToArray();
        Require(result.All(id=>id.Length is >0 and <=120&&SlugPattern().IsMatch(id))&&result.Distinct().Count()==result.Length,"Product IDs must be distinct valid slugs.");return result;
    }
    private static bool SafeId(string value)=>value.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');
    private static void Color(JsonElement v,string key)=>Require(ColorPattern().IsMatch(Text(v,key,7)),"Colors must use six-digit hex values.");
    private static void ValidateTree(JsonElement value,int depth,ref int nodes)
    {
        Require(depth<=32&&++nodes<=20000,"Snapshot exceeds its depth or node limit.");
        if(value.ValueKind==JsonValueKind.Object){var names=new HashSet<string>();foreach(var item in value.EnumerateObject()){Require(names.Add(item.Name)&&names.Count<=2000&&item.Name.Length is >0 and <=256&&!item.Name.Any(char.IsControl)&&item.Name is not("__proto__" or "prototype" or "constructor"),"Invalid snapshot keys.");ValidateTree(item.Value,depth+1,ref nodes);}}
        if(value.ValueKind==JsonValueKind.Array){Require(value.GetArrayLength()<=2000,"Snapshot arrays exceed 2000 items.");foreach(var item in value.EnumerateArray())ValidateTree(item,depth+1,ref nodes);}
        if(value.ValueKind==JsonValueKind.String)Require(value.GetString()!.Length<=65536&&!value.GetString()!.Contains('\0'),"Invalid snapshot text.");
        if(value.ValueKind==JsonValueKind.Number)Require(value.TryGetDouble(out var n)&&double.IsFinite(n)&&Math.Abs(n)<=1e12,"Invalid snapshot number.");
    }
}
