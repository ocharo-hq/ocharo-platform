using System.Text.Json;
using BrandWorkspace.Domain.Commerce;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace BrandWorkspace.Application.Commerce;

/// <summary>Freezes public web presentation choices alongside the saved edition.</summary>
public static class CatalogueWebSettings
{
    public static readonly string[] CopyKeys=["collection","coverNote","header","footer","sizes","pieces","priceOnRequest","prices","materials","decoration"];
    private static IReadOnlyDictionary<string,string> DefaultCopy(string locale)
    {
        string[] values=locale switch {
            "en"=>["2026 Collection","A collection, thoughtfully assembled.","Ocharo","","Sizes","pieces","Price on request","Prices","Fabric","Embellishment"],
            "uz"=>["2026 kolleksiyasi","Har bir tafsiloti o‘ylangan kolleksiya.","Ocharo","","O‘lchamlar","dona","Narx so‘rov bo‘yicha","Narxlar","Mato","Bezak"],
            "ru"=>["Коллекция 2026","Коллекция, продуманная до мелочей.","Ocharo","","Размеры","шт.","Цена по запросу","Цены","Ткань","Отделка"],
            _=>CopyKeys.Select(key=>key=="header"?"Ocharo":"").ToArray()
        };
        return CopyKeys.Zip(values).ToDictionary(pair=>pair.First,pair=>pair.Second);
    }
    public static CatalogueWebVersion Apply(CatalogueWebVersion web,JsonElement snapshot,string locale,CommerceSnapshot? commerce)
    {
        var copy=DefaultCopy(locale);var group="all";var prices=new CataloguePriceDisplay(true,true,true,true,true);
        if(snapshot.TryGetProperty("catalogueSettings",out var settings))
        {
            if(settings.TryGetProperty("mediaGroup",out var mediaGroup))group=mediaGroup.GetString()!;
            if(settings.TryGetProperty("priceDisplay",out var display))prices=new(display.GetProperty("show").GetBoolean(),display.GetProperty("showCurrency").GetBoolean(),display.GetProperty("showQuantity").GetBoolean(),display.GetProperty("showRetail").GetBoolean(),display.GetProperty("showWholesale").GetBoolean());
            if(settings.TryGetProperty("copyByLocale",out var localized)&&localized.TryGetProperty(locale,out var selected))copy=CopyKeys.ToDictionary(key=>key,key=>selected.GetProperty(key).GetString()!);
        }
        var ids=web.Pages.SelectMany(page=>page.ProductIds).Distinct();
        var selectedMedia=ids.ToDictionary(id=>id,id=>(IReadOnlyList<string>)(commerce?.Products.FirstOrDefault(product=>product.Document.Id==id) is {} product
            ? Images(product,group).Where(image=>commerce.MediaApprovals.GetValueOrDefault(image.Sha256)?.ApprovedForPublication==true).Select(image=>image.Sha256).Distinct().ToArray():[]));
        return web with {MediaGroup=group,PriceDisplay=prices,Copy=copy,ProductMedia=selectedMedia};
    }
    private static IEnumerable<ProductMedia> Images(CommerceProduct product,string group)
    {
        var images=product.Document.Media.Where(image=>image.Role!="reference").ToArray();
        if(group=="all")return images;
        if(product.MediaGroups is not {Count:>0})return images.Where(image=>(image.Role is "model" or "detail" or "fabric"?image.Role:"product")==group);
        return product.MediaGroups.Where(set=>set.Category==group).SelectMany(set=>set.MediaIds).Distinct()
            .Select(id=>images.FirstOrDefault(image=>image.Id==id)).Where(image=>image is not null).Select(image=>image!);
    }
}
