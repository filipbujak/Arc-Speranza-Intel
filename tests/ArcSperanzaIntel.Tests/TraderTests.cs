using ArcSperanzaIntel.Core.Api;
using ArcSperanzaIntel.Core.Traders;

namespace ArcSperanzaIntel.Tests;

public class TraderTests
{
    private const string Json = """
        {"success":true,"data":{
          "Lance":[
            {"id":"combat-mk-1","name":"Combat Mk. 1","item_type":"Augment","rarity":"Uncommon","value":640,"trader_price":1920},
            {"id":"combat-mk-2","name":"Combat Mk. 2","item_type":"Augment","rarity":"Rare","value":2000,"trader_price":6000}],
          "Apollo":[
            {"id":"looting-mk-1","name":"Looting Mk. 1","item_type":"Augment","value":640,"trader_price":1920}]},
         "ermal":{"offers":[{"id":"x","offer_title":"Anvil II","coin_cost":20000,"accepted_items":[]}],"services":[]}}
        """;

    [Fact]
    public void LanceMk1AugmentsAlsoCostFreeLoadoutAugment()
    {
        var data = MetaForgeParser.ParseTraders(Json);
        var lance = data.Traders.Single(t => t.Name == "Lance");
        var mk1 = lance.Offers.Single(o => o.ItemId == "combat-mk-1");
        Assert.Equal(1920, mk1.Price);
        Assert.NotNull(mk1.AltPrice);
        Assert.Equal(AlternativePrices.FreeLoadoutAugmentId, mk1.AltPrice!.ItemId);
        Assert.Equal(1, mk1.AltPrice.Amount);
        Assert.Null(lance.Offers.Single(o => o.ItemId == "combat-mk-2").AltPrice);
    }

    [Fact]
    public void OtherTradersGetNoAltPrice()
    {
        var data = MetaForgeParser.ParseTraders(Json);
        Assert.Null(data.Traders.Single(t => t.Name == "Apollo").Offers[0].AltPrice);
    }

    [Fact]
    public void ErmalKeyStillParsed()
    {
        Assert.NotNull(MetaForgeParser.ParseTraders(Json).Ermal);
    }
}
