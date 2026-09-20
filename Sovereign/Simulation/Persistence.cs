using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sovereign.Simulation;

public sealed partial class SimulationEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 40
    };
    private sealed class SaveEnvelope
    {
        public string Format { get; set; } = "Sovereign.Save";
        public int Version { get; set; } = 6;
        public string Checksum { get; set; } = "";
        public WorldState? State { get; set; }
    }

    public string CanonicalDigest() => Digest(CanonicalState(State));

    public string SaveJson()
    {
        Validate(State);
        return JsonSerializer.Serialize(new SaveEnvelope { State = State, Checksum = CanonicalDigest() }, JsonOptions);
    }

    public static SimulationEngine LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 8_000_000) throw new InvalidDataException("Save file is empty or exceeds 8 MB.");
        try
        {
            var envelope = JsonSerializer.Deserialize<SaveEnvelope>(json, JsonOptions);
            if (envelope == null || envelope.Format != "Sovereign.Save" || envelope.Version is not (1 or 2 or 3 or 4 or 5 or 6) || envelope.State == null)
                throw new InvalidDataException("Unsupported save format or version.");
            var rawState = JsonNode.Parse(json)!["State"]!;
            if (envelope.Checksum != Digest(Canonicalize(rawState, "").ToJsonString())) throw new InvalidDataException("Save checksum failed; the file is damaged or was modified.");
            if (envelope.Version != envelope.State.SaveVersion) throw new InvalidDataException("Save versions disagree.");
            if (envelope.Version == 1)
            {
                MigrateVersionOne(envelope.State);
                MigrateConstructionVersionThree(envelope.State);
                // Version-one geography was just created using the current province catalog.
                envelope.State.SaveVersion = 5;
            }
            if (envelope.Version == 2)
            {
                Validate(envelope.State, legacyVersionTwo: true);
                MigrateVersionTwo(envelope.State);
            }
            if (envelope.State.SaveVersion == 3)
            {
                Validate(envelope.State, legacyVersionThree: true);
                MigrateConstructionVersionThree(envelope.State);
            }
            if (envelope.State.SaveVersion == 4)
            {
                Validate(envelope.State, legacyVersionFour: true);
                MigrateQingProvincesVersionFour(envelope.State);
            }
            if (envelope.State.SaveVersion == 5)
            {
                Validate(envelope.State, legacyVersionFive: true);
                foreach (var country in envelope.State.Countries)
                    if (!country.PoliticsInitialized) InitializeOrderEconomy(country, preserveLegacySnapshot: true);
                envelope.State.SaveVersion = 6;
            }
            Validate(envelope.State);
            foreach (var country in envelope.State.Countries) SynchronizeGeography(country);
            return new SimulationEngine { State = envelope.State };
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NullReferenceException or InvalidOperationException or OverflowException)
        {
            throw new InvalidDataException("The save file contains invalid game data.", ex);
        }
    }

    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string CanonicalState(WorldState state)
    {
        var node = JsonSerializer.SerializeToNode(state, JsonOptions)!;
        return Canonicalize(node, "").ToJsonString();
    }

    private static JsonNode Canonicalize(JsonNode node, string name)
    {
        if (node is JsonObject obj)
        {
            var sorted = new JsonObject();
            foreach (var property in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                sorted.Add(property.Key, property.Value == null ? null : Canonicalize(property.Value, property.Key));
            return sorted;
        }
        if (node is JsonArray array)
        {
            IEnumerable<JsonNode?> items = array;
            if (name is "Reforms" or "Technologies" or "TradePacts" or "CompletedEvents")
                items = items.OrderBy(x => x?.ToJsonString(), StringComparer.Ordinal);
            return new JsonArray(items.Select(x => x == null ? null : Canonicalize(x, "")).ToArray());
        }
        return node.DeepClone();
    }


    private static void MigrateVersionOne(WorldState state)
    {
        if (state.Countries == null || state.Countries.Count != 3 ||
            !state.Countries.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "GBR", "JAP", "PRU" }))
            throw new InvalidDataException("Version 1 must contain its original three countries.");
        foreach (var country in state.Countries)
        {
            if (country.Population <= 0 || country.Construction == null || country.Industries == null ||
                !Catalog.Industries.All(i => country.Industries.TryGetValue(i.Id, out int count) && count >= 0 && count <= 50) ||
                country.Production == null || !Catalog.Goods.All(good => country.Production.TryGetValue(good, out decimal value) && value >= 0m) || country.Gdp < 0m)
                throw new InvalidDataException("Invalid legacy population or industry.");
            InitializeGeography(country);
            InitializeCityEconomies(country);
            // Retain the national snapshot while distributing it to the newly introduced local accounts.
            foreach (var good in Catalog.Goods)
            {
                var industry = Catalog.Industries.Single(i => i.Produces == good);
                var anchor = country.Cities.OrderByDescending(city => city.Industries[industry.Id]).First();
                decimal assigned = 0m;
                foreach (var city in country.Cities.Where(city => city != anchor))
                {
                    city.Production[good] = country.Industries[industry.Id] > 0 ?
                        country.Production[good] * city.Industries[industry.Id] / country.Industries[industry.Id] : 0m;
                    assigned += city.Production[good];
                }
                anchor.Production[good] = country.Production[good] - assigned;
            }
            decimal originalCityTotal = country.Cities.Sum(city => city.Gdp), assignedGdp = 0m;
            var gdpAnchor = country.Cities.OrderByDescending(city => city.Gdp).First();
            foreach (var city in country.Cities.Where(city => city != gdpAnchor))
            {
                city.Gdp = originalCityTotal > 0m ? country.Gdp * city.Gdp / originalCityTotal : 0m;
                assignedGdp += city.Gdp;
            }
            gdpAnchor.Gdp = country.Gdp - assignedGdp;
            SynchronizeGeography(country);
        }
        foreach (var d in GeographyCatalog.Countries.Where(d => state.Countries.All(c => c.Id != d.Id)))
        {
            var country = CreateCountry(d.Id, d.Name, d.Adjective, d.InitialPopulation, d.InitialTreasury, d.InitialLiteracy, d.InitialPrestige, d.IndustryLevels);
            RecordHistory(country, state.Date);
            state.Countries.Add(country);
        }
        foreach (var country in state.Countries)
        foreach (var other in state.Countries.Where(c => c.Id != country.Id))
        {
            country.Relations.TryAdd(other.Id, country.Id == "JAP" || other.Id == "JAP" ? -10 : 10);
            country.DiplomaticCooldowns.TryAdd(other.Id, 0);
        }
        state.SaveVersion = 3;
        // Existing stock, treasury, laws, diplomacy, event choices and research are preserved.
        // New countries enter at scenario baseline on the saved date; no invented historical catch-up is applied.
    }

    private static void MigrateConstructionVersionThree(WorldState state)
    {
        foreach (var country in state.Countries)
        {
            country.NextConstructionId = 1;
            country.ConstructionPaused = false;
            country.ConstructionSpending = country.ConstructionMaterialSpending = country.ConstructionWages = country.ConstructionPoints = 0m;
            foreach (var city in country.Cities)
            {
                // Existing saves gain no free buildings or surprise standing payroll.
                city.ConstructionSectors = 0; city.ConstructionMethodId = "wood";
                RefreshCityWorkforce(city);
            }
            foreach (var project in country.Construction)
            {
                project.Id = country.Id + "_construction_" + country.NextConstructionId++;
                project.RequiredPoints = ConstructionCatalog.Costs[project.IndustryId];
                project.ProgressPoints = project.RequiredPoints * (project.TotalDays - project.DaysRemaining) / project.TotalDays;
                project.Name = ConstructionCatalog.Name(project.IndustryId);
                project.LegacyPrepaid = true; project.Paused = false;
            }
        }
        state.SaveVersion = 4;
    }

    private static readonly HashSet<string> VersionTwoQingCities = new(StringComparer.Ordinal)
    {
        "QNG_beijing", "QNG_tianjin", "QNG_xian", "QNG_nanjing",
        "QNG_suzhou", "QNG_hangzhou", "QNG_canton", "QNG_fuzhou"
    };

    private static void MigrateVersionTwo(WorldState state)
    {
        var country = state.Countries.Single(c => c.Id == "QNG");
        foreach (var region in country.Regions)
        {
            var additions = GeographyCatalog.Cities.Where(c => c.CountryId == "QNG" && GeographyCatalog.LegacyRegionForCity(c.Id) == region.Id &&
                !VersionTwoQingCities.Contains(c.Id)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
            if (additions.Length == 0) continue;
            // Reclassify existing regional residents rather than inventing people or changing old cities.
            // A damaged/handcrafted legacy region that cannot supply one resident per settlement is rejected.
            if (region.RuralPopulation < additions.Length)
                throw new InvalidDataException("Legacy region has insufficient rural population for the expanded settlement catalog.");
            long desired = additions.Sum(c => c.InitialPopulation);
            long budget = Math.Min(region.RuralPopulation, desired);
            long allocatable = budget - additions.Length;
            long totalWeight = desired - additions.Length;
            long allocated = 0;
            for (int index = 0; index < additions.Length; index++)
            {
                var definition = additions[index];
                long population = index == additions.Length - 1 ? budget - allocated :
                    1 + (long)(allocatable * (decimal)(definition.InitialPopulation - 1) / totalWeight);
                var city = new CityState
                {
                    Id = definition.Id, Name = definition.Name, RegionId = region.Id,
                    Population = population, LivingStandard = country.LivingStandard,
                    Literacy = Math.Min(99m, country.Literacy + 3m),
                    Industries = Catalog.Industries.ToDictionary(i => i.Id, _ => 0),
                    Production = Catalog.Goods.ToDictionary(g => g, _ => 0m)
                };
                RefreshCityWorkforce(city);
                country.Cities.Add(city);
                allocated += population;
            }
            region.RuralPopulation -= allocated;
        }
        // Existing factories, goods, cash, GDP, local data, policies and queues are untouched.
        // Newly exposed settlements start without industrial capacity; players can build there.
        SynchronizeGeography(country);
        state.SaveVersion = 3;
    }

    private static void MigrateQingProvincesVersionFour(WorldState state)
    {
        var country = state.Countries.Single(c => c.Id == "QNG");
        long ruralPopulation = country.Regions.Sum(r => r.RuralPopulation);
        country.Regions = GeographyCatalog.Regions.Where(r => r.CountryId == "QNG")
            .Select(r => new RegionState { Id = r.Id, Name = r.Name }).ToList();
        foreach (var city in country.Cities) city.RegionId = QingProvinceCatalog.RegionForCity(city.Id);
        AllocateRuralPopulation(country, ruralPopulation);
        // Only administrative grouping changes. Never re-create cities, factories, queues or treasury.
        SynchronizeGeography(country);
        state.SaveVersion = 5;
    }

    private static void ValidateRegions(CountryState country, bool legacyVersionTwo = false, bool legacyConstruction = false, bool legacyGeography = false, bool legacyEconomy = false)
    {
        static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        static bool Keys<T>(Dictionary<string, T>? dictionary, IEnumerable<string> keys) =>
            dictionary != null && dictionary.Keys.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(keys.OrderBy(x => x, StringComparer.Ordinal));
        var regions = country.Id == "QNG" && legacyGeography ? GeographyCatalog.LegacyQingRegions.ToArray() :
            GeographyCatalog.Regions.Where(r => r.CountryId == country.Id).ToArray();
        var cities = GeographyCatalog.Cities.Where(c => c.CountryId == country.Id &&
            (!legacyVersionTwo || country.Id != "QNG" || VersionTwoQingCities.Contains(c.Id))).ToArray();
        Require(country.Regions != null && country.Regions.All(r => r != null) && country.Regions.Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(regions.Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal)), "Invalid region IDs.");
        Require(country.Cities != null && country.Cities.All(c => c != null) && country.Cities.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(cities.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal)), "Invalid city IDs.");
        foreach (var city in country.Cities)
        {
            var definition = GeographyCatalog.City(city.Id);
            string expectedRegion = country.Id == "QNG" && legacyGeography ? GeographyCatalog.LegacyRegionForCity(city.Id) : definition.RegionId;
            Require(city.RegionId == expectedRegion && city.Name == definition.Name && city.Population is > 0 and <= 2_000_000_000, "Invalid city identity or population.");
            Require(Keys(city.Industries, Catalog.Industries.Select(i => i.Id)) && city.Industries.Values.All(x => x is >= 0 and <= 100), "Invalid city industry.");
            Require(Keys(city.Production, Catalog.Goods) && city.Production.Values.All(x => x is >= 0 and <= 1_000_000_000_000m), "Invalid city output.");
            Require(city.Workforce == (long)(city.Population * .45m) && city.Dependents + city.Workforce == city.Population &&
                city.Artisans == (long)(city.Population * .22m) && city.Employed == city.Artisans + city.IndustrialWorkers + city.ConstructionWorkers &&
                (!legacyEconomy || city.IndustrialWorkers == Math.Min(city.Workforce - city.Artisans, city.Industries.Values.Sum(x => (long)x * 2500))) &&
                city.ConstructionWorkers == Math.Min(city.Workforce - city.Artisans - city.IndustrialWorkers, (long)city.ConstructionSectors * ConstructionCatalog.WorkersPerSector) &&
                city.Employed <= city.Workforce && city.Employed >= 0, "Invalid city workforce accounting.");
            Require(city.ConstructionSectors is >= 0 and <= 100 && ConstructionCatalog.Methods.Any(m => m.Id == city.ConstructionMethodId), "Invalid construction sectors.");
            Require(city.Literacy is >= 0m and <= 100m && city.LivingStandard is >= 0m and <= 30m && city.MigrationLastMonth >= 0 && city.MigrationLastMonth <= city.Population && city.Gdp is >= 0m and <= 1_000_000_000_000m, "Invalid city indicators.");
            if (legacyConstruction) Require(country.Construction.Count(p => p.CityId == city.Id) <= 5, "City construction capacity exceeded.");
            else foreach (var constructionDefinition in ConstructionCatalog.Costs)
            {
                int levels = GetCompletedConstructionLevels(country, city, constructionDefinition.Key);
                Require(levels + GetQueuedConstructionLevels(country, city, constructionDefinition.Key) <= (constructionDefinition.Key == ConstructionCatalog.RailwayId ? 20 : 100),
                    "Completed and queued levels exceed city capacity.");
            }
        }
        foreach (var region in country.Regions)
        {
            Require(region.Name == regions.Single(r => r.Id == region.Id).Name && region.RuralPopulation is >= 0 and <= 2_000_000_000 && region.UrbanPopulation == country.Cities.Where(c => c.RegionId == region.Id).Sum(c => c.Population), "Invalid regional population.");
            Require(region.Gdp == country.Cities.Where(c => c.RegionId == region.Id).Sum(c => c.Gdp) + region.SubsistenceGdp, "Invalid regional output.");
        }
        Require(country.Population == country.Regions.Sum(r => r.Population), "National and local population totals differ.");
        Require(Catalog.Industries.All(i => country.Industries[i.Id] == country.Cities.Sum(c => c.Industries[i.Id])), "National and local industry totals differ.");
        Require(country.Construction.All(p => cities.Any(c => c.Id == p.CityId)), "Construction targets an unknown or foreign city.");
        Require(Catalog.Goods.All(good => Math.Abs(country.Production[good] - country.Cities.Sum(city => city.Production[good]) - country.Regions.Sum(r => r.SubsistenceProduction.GetValueOrDefault(good))) < .000000001m), "National and local production totals differ.");
        Require(Math.Abs(country.Gdp - country.Cities.Sum(city => city.Gdp) - country.Regions.Sum(r => r.SubsistenceGdp)) < .000000001m, "National and local GDP totals differ.");
    }

    private static void Validate(WorldState state, bool legacyVersionTwo = false, bool legacyVersionThree = false, bool legacyVersionFour = false, bool legacyVersionFive = false)
    {
        static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        static bool Reasonable(decimal value) => value >= 0m && value <= 1_000_000_000_000m;
        static bool Keys<T>(Dictionary<string, T>? dictionary, IEnumerable<string> keys) =>
            dictionary != null && dictionary.Keys.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(keys.OrderBy(x => x, StringComparer.Ordinal));
        Require(state.SaveVersion == (legacyVersionTwo ? 2 : legacyVersionThree ? 3 : legacyVersionFour ? 4 : legacyVersionFive ? 5 : 6), "Unsupported simulation version.");
        Require(state.Date >= Catalog.StartDate && state.Date <= Catalog.EndDate && state.Date.TimeOfDay == TimeSpan.Zero, "Invalid date.");
        Require(state.DayNumber == (state.Date - Catalog.StartDate).Days, "Day counter does not match date.");
        Require(state.Finished == (state.Date == Catalog.EndDate), "Invalid campaign completion state.");
        Require(state.Countries != null && state.Countries.Count == GeographyCatalog.Countries.Count, "Save must contain all scenario countries.");
        Require(state.Countries!.All(c => c != null) && state.Countries.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(GeographyCatalog.Countries.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal)), "Invalid country IDs.");
        Require(state.Countries.Any(c => c.Id == state.PlayerCountryId), "Invalid player country.");
        Require(Keys(state.ExternalGoods, Catalog.Goods) && state.ExternalGoods.Values.All(Reasonable) && Reasonable(state.ExternalTreasury), "Invalid external market.");
        Require(state.Log != null && state.Log.Count <= 120 && state.Log.All(s => s != null && s.Length <= 2000), "Invalid journal.");
        var eventIds = new[] { "industrial_petition", "harvest_pressure", "chartists", "accession", "tenpo" };
        Require(state.CompletedEvents != null && state.CompletedEvents.All(eventIds.Contains), "Invalid event history.");
        foreach (var c in state.Countries)
        {
            Require(!string.IsNullOrEmpty(c.Name) && c.Name.Length <= 100 && !string.IsNullOrEmpty(c.Adjective), "Invalid country label.");
            Require(c.Population is > 0 and <= 2_000_000_000 && Reasonable(c.Treasury) && Reasonable(c.Debt) && Reasonable(c.Gdp), "Invalid economy totals.");
            Require(c.Literacy >= 0m && c.Literacy <= 100m && c.Unrest >= 0m && c.Unrest <= 100m && c.LivingStandard >= 0m && c.LivingStandard <= 30m, "Invalid social indicators.");
            Require(c.TaxRate is >= 5 and <= 50 && Reasonable(c.Prestige) && c.NeedsFulfilled >= 0m && c.NeedsFulfilled <= 1m, "Invalid policies.");
            foreach (var data in new[] { c.Goods, c.Prices, c.Production, c.Consumption })
                Require(Keys(data, Catalog.Goods) && data.Values.All(Reasonable), "Invalid market inventory.");
            Require(c.Prices.Values.All(p => p > 0m), "Prices must be positive.");
            int industryLimit = GeographyCatalog.Cities.Count(city => city.CountryId == c.Id) * 100;
            Require(Keys(c.Industries, Catalog.Industries.Select(x => x.Id)) && c.Industries.Values.All(x => x >= 0 && x <= industryLimit), "Invalid industries.");
            Require(Keys(c.Trade, Catalog.Goods) && c.Trade.Values.All(x => x is >= -1 and <= 1), "Invalid trade routes.");
            bool legacyConstruction = legacyVersionTwo || legacyVersionThree;
            if (legacyConstruction)
                Require(c.Construction != null && c.Construction.Count <= 20 && c.Construction.All(p => p != null && Catalog.Industries.Any(d =>
                    d.Id == p.IndustryId && p.TotalDays == d.Days && p.DaysRemaining > 0 && p.DaysRemaining <= d.Days)), "Invalid legacy construction queue.");
            else
            {
                Require(c.NextConstructionId is > 0 and < int.MaxValue && c.Construction != null && c.Construction.Count <= ConstructionCatalog.MaximumQueueLength,
                    "Invalid construction queue metadata.");
                Require(c.Construction.All(p => p != null && p.IndustryId != null && ConstructionCatalog.Costs.TryGetValue(p.IndustryId, out decimal cost) &&
                    p.RequiredPoints == cost && p.ProgressPoints >= 0m && p.ProgressPoints < cost && p.Name == ConstructionCatalog.Name(p.IndustryId) &&
                    p.Id != null && p.Id.StartsWith(c.Id + "_construction_", StringComparison.Ordinal) &&
                    int.TryParse(p.Id[(c.Id.Length + 14)..], out int number) && number > 0 && number < c.NextConstructionId), "Invalid construction project.");
                Require(c.Construction.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() == c.Construction.Count, "Duplicate construction project IDs.");
                Require(c.Construction.All(p => p.LegacyPrepaid ? Catalog.Industries.Any(d => d.Id == p.IndustryId &&
                    p.TotalDays == d.Days && p.DaysRemaining > 0 && p.DaysRemaining <= d.Days &&
                    p.ProgressPoints >= p.RequiredPoints * (p.TotalDays - p.DaysRemaining) / p.TotalDays) :
                    p.TotalDays == 0 && p.DaysRemaining == 0), "Invalid legacy construction credit.");
                Require(Reasonable(c.ConstructionSpending) && Reasonable(c.ConstructionMaterialSpending) && Reasonable(c.ConstructionWages) && Reasonable(c.ConstructionPoints) &&
                    c.ConstructionSpending == c.ConstructionMaterialSpending + c.ConstructionWages, "Invalid construction accounts.");
            }
            bool legacyEconomy = legacyVersionTwo || legacyVersionThree || legacyVersionFour || legacyVersionFive;
            ValidateRegions(c, legacyVersionTwo, legacyConstruction, legacyVersionTwo || legacyVersionThree || legacyVersionFour, legacyEconomy);
            if (!legacyEconomy) { ValidateOrderEconomy(c); ValidateProduction(c); ValidatePolitics(c); }
            Require(c.Reforms != null && c.Reforms.All(x => Catalog.Reforms.Any(r => r.Id == x)) && c.Technologies != null && c.Technologies.All(x => Catalog.Technologies.Any(t => t.Id == x)), "Invalid reforms or technologies.");
            if (!legacyConstruction) Require(c.Cities.All(city => city.ConstructionMethodId != "iron" || c.Technologies.Contains("mechanical_tools")), "Locked construction method.");
            Require(c.ResearchId != null && (c.ResearchId == "" || Catalog.Technologies.Any(t => t.Id == c.ResearchId)) && c.ResearchProgress >= 0m && c.ResearchProgress <= 600m, "Invalid research.");
            var otherIds = state.Countries.Where(o => o.Id != c.Id).Select(o => o.Id).ToArray();
            Require(Keys(c.Relations, otherIds) && c.Relations.Values.All(v => v is >= -100 and <= 100), "Invalid relations.");
            Require(Keys(c.DiplomaticCooldowns, otherIds) && c.DiplomaticCooldowns.Values.All(v => v is >= 0 and <= 90), "Invalid diplomatic cooldown.");
            Require(c.TradePacts != null && c.TradePacts.All(otherIds.Contains) && c.MobilizedAgainst != null && (c.MobilizedAgainst == "" || otherIds.Contains(c.MobilizedAgainst)), "Invalid diplomatic state.");
            Require(c.History != null && c.History.Count <= 150 && c.History.All(h => h != null && h.Date >= Catalog.StartDate && h.Date <= state.Date && Reasonable(h.Gdp) && Reasonable(h.Treasury) && h.LivingStandard is >= 0m and <= 30m), "Invalid history.");
        }
        if (state.PendingEvent != null)
        {
            var e = state.PendingEvent;
            Require(eventIds.Contains(e.Id) && !state.CompletedEvents!.Contains(e.Id) && e.Options != null && e.Options.Count == 3, "Invalid pending event.");
            Require(!string.IsNullOrEmpty(e.Title) && e.Options!.All(o => o != null && !string.IsNullOrEmpty(o.Id) && !string.IsNullOrEmpty(o.Label)), "Invalid event choices.");
        }
    }
}
