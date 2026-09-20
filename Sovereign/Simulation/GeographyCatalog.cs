using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sovereign.Simulation;

public sealed record CountryDefinition(string Id, string Name, string Adjective, string CapitalCityId, string ColorHex,
    string ArchitectureProfile, string Description, long InitialPopulation, decimal InitialTreasury, decimal InitialLiteracy,
    decimal InitialPrestige, int[] IndustryLevels);
public sealed record RegionDefinition(string Id, string CountryId, string Name);
public sealed record CityDefinition(string Id, string CountryId, string RegionId, string Name, double Latitude, double Longitude,
    bool IsCapital, bool IsPort, long InitialPopulation);

/// <summary>
/// Curated scenario geography, not an exhaustive historical gazetteer. Coordinates are rounded geographic locations;
/// English historical exonyms are used where appropriate. Population, region groupings and industry allocations are
/// authored scenario estimates, NOT census observations. Overseas possessions and dependencies are not exhaustively modeled.
/// Geographic reference: https://www.naturalearthdata.com/downloads/10m-cultural-vectors/10m-populated-places/
/// Damascus is grouped under Ottoman nominal sovereignty; Egyptian administration 1832–1840 is not simulated.
/// </summary>
public static class GeographyCatalog
{
    public static IReadOnlyList<CountryDefinition> Countries { get; } = new[]
    {
        new CountryDefinition("GBR", "United Kingdom", "British", "GBR_london", "a84448", "british", "An industrial monarchy with maritime trade and expanding manufacturing.", 24_000_000, 26000m, 54m, 70m, new[]{12,4,5,4,3,6}),
        new CountryDefinition("PRU", "Prussia", "Prussian", "PRU_berlin", "557291", "central_european", "A disciplined kingdom connecting the Rhineland, Brandenburg and eastern provinces.", 14_000_000, 18000m, 60m, 45m, new[]{7,3,3,2,2,3}),
        new CountryDefinition("JAP", "Japan", "Japanese", "JAP_edo", "bc9a65", "japanese", "Tokugawa rule, a large domestic market and a network of castle towns.", 31_000_000, 18000m, 38m, 25m, new[]{15,4,2,2,2,5}),
        new CountryDefinition("FRA", "France", "French", "FRA_paris", "5375a7", "french", "The July Monarchy governs a populous countryside and influential commercial cities.", 33_000_000, 29000m, 45m, 65m, new[]{16,5,5,4,4,7}),
        new CountryDefinition("AUS", "Austrian Empire", "Austrian", "AUS_vienna", "d8c8a4", "central_european", "A diverse Habsburg monarchy spanning Alpine, Bohemian, Hungarian and Adriatic lands.", 35_000_000, 26000m, 35m, 55m, new[]{17,6,4,4,3,7}),
        new CountryDefinition("RUS", "Russian Empire", "Russian", "RUS_petersburg", "759369", "russian", "An extensive agrarian empire with major river ports and developing industry.", 60_000_000, 38000m, 15m, 65m, new[]{29,9,6,5,4,12}),
        new CountryDefinition("USA", "United States", "American", "USA_washington", "7c9fae", "american", "A federal republic with growing Atlantic cities and interior commercial routes.", 15_000_000, 22000m, 65m, 40m, new[]{8,4,3,3,3,4}),
        new CountryDefinition("QNG", "Qing Empire", "Qing", "QNG_beijing", "cba64d", "chinese", "A vast agrarian population and long-established regional markets under the Qing dynasty.", 400_000_000, 90000m, 20m, 55m, new[]{190,42,25,22,18,72}),
        new CountryDefinition("OTT", "Ottoman Empire", "Ottoman", "OTT_constantinople", "a96e62", "ottoman", "An imperial court overseeing Balkan, Anatolian and Arab commercial centers.", 25_000_000, 21000m, 20m, 45m, new[]{12,4,3,3,2,5}),
        new CountryDefinition("SPA", "Spain", "Spanish", "SPA_madrid", "c5a172", "iberian", "A monarchy balancing agrarian production, maritime commerce and regional manufacturing.", 13_000_000, 17000m, 25m, 35m, new[]{7,3,3,2,2,3}),
        new CountryDefinition("POR", "Portugal", "Portuguese", "POR_lisbon", "6e9a8b", "iberian", "An Atlantic kingdom with river trade, coastal towns and an agricultural interior.", 3_500_000, 13000m, 22m, 25m, new[]{3,2,1,1,1,2}),
        new CountryDefinition("BEL", "Belgium", "Belgian", "BEL_brussels", "a77e93", "central_european", "A young kingdom with concentrated textile, coal and metal industries.", 4_200_000, 17000m, 50m, 30m, new[]{3,2,3,2,2,3})
    };
    public static IReadOnlyList<RegionDefinition> Regions { get; } = new[]
    {
        R("GBR", "south", "Southern England"), R("GBR", "north", "Northern Britain"), R("GBR", "west", "Wales and Ireland"),
        R("PRU", "central", "Brandenburg and Pomerania"), R("PRU", "east", "Eastern provinces"), R("PRU", "west", "Rhineland and Westphalia"),
        R("JAP", "east", "Eastern Honshū"), R("JAP", "central", "Central Honshū"), R("JAP", "west", "Western Japan"),
        R("FRA", "north", "Northern France"), R("FRA", "east", "Eastern France"), R("FRA", "south", "Southern and Atlantic France"),
        R("AUS", "alpine", "Alpine and Bohemian lands"), R("AUS", "danube", "Danubian lands"), R("AUS", "adriatic", "Adriatic and Italian lands"),
        R("RUS", "baltic", "Baltic provinces"), R("RUS", "central", "Central Russia"), R("RUS", "south", "Southern provinces"),
        R("USA", "northeast", "Atlantic states and federal district"), R("USA", "south", "Southern states"), R("USA", "interior", "Interior states"),
        R("OTT", "balkans", "Balkan lands"), R("OTT", "anatolia", "Anatolia"), R("OTT", "arab", "Arab provinces"),
        R("SPA", "central", "Central Spain"), R("SPA", "east", "Eastern Spain"), R("SPA", "atlantic", "Atlantic and southern Spain"),
        R("POR", "central", "Central Portugal"), R("POR", "north", "Northern Portugal"), R("POR", "south", "Southern Portugal"),
        R("BEL", "brabant", "Brabant"), R("BEL", "flanders", "Flanders"), R("BEL", "wallonia", "Wallonia")
    }.Concat(QingProvinceCatalog.Provinces.Select(p => new RegionDefinition(p.Id, "QNG", p.Name))).ToArray();
    public static IReadOnlyList<CityDefinition> Cities { get; } = ParseCities();
    // Version 2–4 save geography. Kept solely for strict validation and conservation during migration.
    public static IReadOnlyList<RegionDefinition> LegacyQingRegions { get; } = new[]
    {
        R("QNG", "north", "Northern China"), R("QNG", "yangtze", "Yangtze basin"), R("QNG", "south", "Southern China")
    };
    public static string LegacyRegionForCity(string cityId)
    {
        if (!cityId.StartsWith("QNG_", StringComparison.Ordinal)) return City(cityId).RegionId;
        string city = cityId[4..];
        return "QNG_" + (new[] { "beijing", "tianjin", "xian", "jinan", "taiyuan", "kaifeng", "luoyang", "baoding", "zhengding", "chengde", "shengjing", "dengzhou" }.Contains(city) ? "north" :
            new[] { "nanjing", "suzhou", "hangzhou", "chengdu", "chongqing", "wuchang", "hankou", "jiujiang", "nanchang", "anqing", "wuhu", "yangzhou", "zhenjiang", "shanghai", "changsha" }.Contains(city) ? "yangtze" : "south");
    }
    public static CountryDefinition Country(string id) => Countries.Single(c => c.Id == id);
    public static CityDefinition City(string id) => Cities.Single(c => c.Id == id);
    private static RegionDefinition R(string country, string id, string name) => new(country + "_" + id, country, name);
    private static IReadOnlyList<CityDefinition> ParseCities()
    {
        const string data = """
GBR|london|south|London|51.51|-0.13|1700000|C|P
GBR|bristol|south|Bristol|51.45|-2.59|110000||P
GBR|manchester|north|Manchester|53.48|-2.24|270000||
GBR|liverpool|north|Liverpool|53.41|-2.98|250000||P
GBR|glasgow|north|Glasgow|55.86|-4.25|230000||P
GBR|edinburgh|north|Edinburgh|55.95|-3.19|140000||
GBR|cardiff|west|Cardiff|51.48|-3.18|12000||P
GBR|dublin|west|Dublin|53.35|-6.26|250000||P
PRU|berlin|central|Berlin|52.52|13.40|280000|C|
PRU|stettin|central|Stettin|53.43|14.55|40000||P
PRU|breslau|east|Breslau|51.11|17.04|90000||
PRU|konigsberg|east|Königsberg|54.71|20.51|65000||P
PRU|danzig|east|Danzig|54.35|18.65|55000||P
PRU|cologne|west|Cologne|50.94|6.96|65000||
PRU|essen|west|Essen|51.46|7.01|11000||
PRU|aachen|west|Aachen|50.78|6.08|40000||
JAP|edo|east|Edo|35.68|139.76|1050000|C|P
JAP|sendai|east|Sendai|38.27|140.87|55000||
JAP|kyoto|central|Kyōto|35.01|135.77|350000||
JAP|osaka|central|Ōsaka|34.69|135.50|400000||P
JAP|nagoya|central|Nagoya|35.18|136.91|100000||
JAP|hiroshima|west|Hiroshima|34.39|132.46|55000||P
JAP|nagasaki|west|Nagasaki|32.75|129.88|60000||P
JAP|kagoshima|west|Kagoshima|31.60|130.56|50000||P
FRA|paris|north|Paris|48.86|2.35|900000|C|
FRA|lille|north|Lille|50.63|3.06|75000||
FRA|rouen|north|Rouen|49.44|1.10|90000||P
FRA|lyon|east|Lyon|45.76|4.84|185000||
FRA|strasbourg|east|Strasbourg|48.58|7.75|65000||
FRA|marseille|south|Marseille|43.30|5.37|150000||P
FRA|bordeaux|south|Bordeaux|44.84|-0.58|120000||P
FRA|nantes|south|Nantes|47.22|-1.55|80000||P
AUS|vienna|alpine|Vienna|48.21|16.37|330000|C|
AUS|prague|alpine|Prague|50.08|14.44|115000||
AUS|graz|alpine|Graz|47.07|15.44|45000||
AUS|pest|danube|Pest|47.50|19.07|70000||
AUS|pressburg|danube|Pressburg|48.15|17.11|35000||
AUS|trieste|adriatic|Trieste|45.65|13.77|65000||P
AUS|venice|adriatic|Venice|45.44|12.33|110000||P
AUS|milan|adriatic|Milan|45.46|9.19|150000||
RUS|petersburg|baltic|Saint Petersburg|59.93|30.34|450000|C|P
RUS|riga|baltic|Riga|56.95|24.11|65000||P
RUS|reval|baltic|Reval|59.44|24.75|15000||P
RUS|moscow|central|Moscow|55.76|37.62|350000||
RUS|nizhny|central|Nizhny Novgorod|56.33|44.01|30000||
RUS|kazan|central|Kazan|55.79|49.12|40000||
RUS|kiev|south|Kiev|50.45|30.52|50000||
RUS|odessa|south|Odessa|46.48|30.72|65000||P
USA|washington|northeast|Washington|38.91|-77.04|20000|C|
USA|newyork|northeast|New York|40.71|-74.01|280000||P
USA|philadelphia|northeast|Philadelphia|39.95|-75.16|190000||P
USA|boston|northeast|Boston|42.36|-71.06|85000||P
USA|charleston|south|Charleston|32.78|-79.93|35000||P
USA|neworleans|south|New Orleans|29.95|-90.07|70000||P
USA|cincinnati|interior|Cincinnati|39.10|-84.51|35000||
USA|stlouis|interior|Saint Louis|38.63|-90.20|13000||
QNG|beijing|north|Beijing|39.90|116.41|1100000|C|
QNG|tianjin|north|Tianjin|39.13|117.20|350000||P
QNG|xian|north|Xi'an|34.34|108.94|300000||
QNG|nanjing|yangtze|Nanjing|32.06|118.80|500000||
QNG|suzhou|yangtze|Suzhou|31.30|120.59|650000||
QNG|hangzhou|yangtze|Hangzhou|30.27|120.15|500000||
QNG|canton|south|Canton|23.13|113.26|800000||P
QNG|fuzhou|south|Fuzhou|26.07|119.30|350000||P
QNG|jinan|north|Jinan|36.664|117.022|180000||
QNG|taiyuan|north|Taiyuan|37.872|112.558|140000||
QNG|kaifeng|north|Kaifeng|34.797|114.308|160000||
QNG|luoyang|north|Luoyang|34.683|112.477|100000||
QNG|baoding|north|Baoding|38.857|115.491|100000||
QNG|zhengding|north|Zhengding|38.149|114.572|65000||
QNG|chengde|north|Chengde|40.980|117.939|60000||
QNG|shengjing|north|Shengjing|41.795|123.449|130000||
QNG|dengzhou|north|Dengzhou|37.824|120.752|45000||P
QNG|chengdu|yangtze|Chengdu|30.664|104.066|350000||
QNG|chongqing|yangtze|Chongqing|29.558|106.578|220000||P
QNG|wuchang|yangtze|Wuchang|30.546|114.301|180000||P
QNG|hankou|yangtze|Hankou|30.583|114.288|300000||P
QNG|jiujiang|yangtze|Jiujiang|29.729|115.993|120000||P
QNG|nanchang|yangtze|Nanchang|28.678|115.893|150000||
QNG|anqing|yangtze|Anqing|30.505|117.047|120000||P
QNG|wuhu|yangtze|Wuhu|31.326|118.376|80000||P
QNG|yangzhou|yangtze|Yangzhou|32.395|119.440|300000||
QNG|zhenjiang|yangtze|Zhenjiang|32.212|119.449|140000||P
QNG|shanghai|yangtze|Shanghai|31.227|121.491|220000||P
QNG|changsha|yangtze|Changsha|28.194|112.974|180000||
QNG|ningbo|south|Ningbo|29.873|121.551|250000||P
QNG|shaoxing|south|Shaoxing|30.000|120.582|180000||
QNG|wenzhou|south|Wenzhou|28.015|120.655|100000||P
QNG|xiamen|south|Xiamen|24.455|118.077|140000||P
QNG|quanzhou|south|Quanzhou|24.908|118.587|160000||P
QNG|zhangzhou|south|Zhangzhou|24.511|117.649|120000||
QNG|chaozhou|south|Chaozhou|23.666|116.641|150000||
QNG|foshan|south|Foshan|23.031|113.112|250000||
QNG|guilin|south|Guilin|25.281|110.296|100000||
QNG|kunming|south|Kunming|25.043|102.708|140000||
QNG|guiyang|south|Guiyang|26.579|106.713|90000||
OTT|constantinople|balkans|Constantinople|41.01|28.97|650000|C|P
OTT|salonica|balkans|Salonica|40.64|22.94|65000||P
OTT|adrianople|balkans|Adrianople|41.68|26.56|70000||
OTT|smyrna|anatolia|Smyrna|38.42|27.14|150000||P
OTT|bursa|anatolia|Bursa|40.19|29.06|70000||
OTT|ankara|anatolia|Ankara|39.93|32.86|30000||
OTT|damascus|arab|Damascus|33.51|36.29|120000||
OTT|baghdad|arab|Baghdad|33.32|44.37|85000||
SPA|madrid|central|Madrid|40.42|-3.70|220000|C|
SPA|valladolid|central|Valladolid|41.65|-4.73|30000||
SPA|barcelona|east|Barcelona|41.39|2.17|130000||P
SPA|valencia|east|Valencia|39.47|-0.38|80000||P
SPA|zaragoza|east|Zaragoza|41.65|-0.89|40000||
SPA|seville|atlantic|Seville|37.39|-5.98|95000||P
SPA|cadiz|atlantic|Cádiz|36.53|-6.29|55000||P
SPA|bilbao|atlantic|Bilbao|43.26|-2.94|16000||P
POR|lisbon|central|Lisbon|38.72|-9.14|210000|C|P
POR|coimbra|central|Coimbra|40.20|-8.41|18000||
POR|santarem|central|Santarém|39.24|-8.69|10000||
POR|porto|north|Porto|41.16|-8.63|65000||P
POR|braga|north|Braga|41.55|-8.43|16000||
POR|guimaraes|north|Guimarães|41.44|-8.29|8000||
POR|evora|south|Évora|38.57|-7.91|14000||
POR|faro|south|Faro|37.02|-7.93|8000||P
BEL|brussels|brabant|Brussels|50.85|4.35|110000|C|
BEL|antwerp|brabant|Antwerp|51.22|4.40|80000||P
BEL|leuven|brabant|Leuven|50.88|4.70|26000||
BEL|ghent|flanders|Ghent|51.05|3.72|96000||P
BEL|bruges|flanders|Bruges|51.21|3.22|42000||
BEL|liege|wallonia|Liège|50.63|5.57|65000||
BEL|namur|wallonia|Namur|50.47|4.87|22000||
BEL|mons|wallonia|Mons|50.45|3.95|25000||
""";
        return data.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            var p = line.Trim().Split('|');
            return new CityDefinition(p[0]+"_"+p[1], p[0], p[0] == "QNG" ? QingProvinceCatalog.RegionForCity(p[0]+"_"+p[1]) : p[0]+"_"+p[2], p[3],
                double.Parse(p[4], CultureInfo.InvariantCulture), double.Parse(p[5], CultureInfo.InvariantCulture),
                p[7] == "C", p[8] == "P", long.Parse(p[6], CultureInfo.InvariantCulture));
        }).ToArray();
    }
}
