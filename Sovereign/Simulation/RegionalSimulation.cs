using System;
using System.Collections.Generic;
using System.Linq;

namespace Sovereign.Simulation;

public sealed partial class SimulationEngine
{
    public IReadOnlyList<CityState> GetCities(string countryId) => State.Countries.Single(c => c.Id == countryId).Cities;
    public CityState GetCity(string cityId) => State.Countries.SelectMany(c => c.Cities).Single(c => c.Id == cityId);

    private static void InitializeGeography(CountryState country)
    {
        country.Regions = GeographyCatalog.Regions.Where(r => r.CountryId == country.Id)
            .Select(r => new RegionState { Id = r.Id, Name = r.Name }).ToList();
        var definitions = GeographyCatalog.Cities.Where(c => c.CountryId == country.Id).ToArray();
        decimal urbanScale = Math.Min(1m, country.Population * .65m / definitions.Sum(c => c.InitialPopulation));
        country.Cities = definitions.Select(d => new CityState
        {
            Id = d.Id, Name = d.Name, RegionId = d.RegionId,
            Population = Math.Max(100, (long)(d.InitialPopulation * urbanScale)),
            Literacy = Math.Min(99m, country.Literacy + 3m), LivingStandard = country.LivingStandard,
            Industries = Catalog.Industries.ToDictionary(i => i.Id, _ => 0),
            Production = Catalog.Goods.ToDictionary(g => g, _ => 0m)
        }).ToList();
        long rural = country.Population - country.Cities.Sum(c => c.Population);
        AllocateRuralPopulation(country, rural);
        foreach (var industry in Catalog.Industries)
        {
            // Allocate discrete productive levels by local labor pool; total capacity is exactly conserved.
            int remaining = country.Industries[industry.Id];
            while (remaining-- > 0)
            {
                var city = country.Cities.OrderBy(c => (c.Industries[industry.Id] + 1m) / c.Population)
                    .ThenBy(c => c.Id, StringComparer.Ordinal).First();
                city.Industries[industry.Id]++;
            }
        }
        // Every modeled city is a productive local center. Move existing capacity rather than creating free levels.
        foreach (var emptyCity in country.Cities.Where(c => c.Industries.Values.Sum() == 0).ToArray())
        {
            var donor = country.Cities.OrderByDescending(c => c.Industries.Values.Sum()).ThenBy(c => c.Id, StringComparer.Ordinal).First();
            var industry = donor.Industries.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).First().Key;
            donor.Industries[industry]--;
            emptyCity.Industries[industry]++;
        }
        foreach (var project in country.Construction)
            project.CityId = GeographyCatalog.Country(country.Id).CapitalCityId;
        SynchronizeGeography(country);
        foreach (var city in country.Cities) RefreshCityWorkforce(city);
    }

    private static void AllocateRuralPopulation(CountryState country, long ruralPopulation)
    {
        if (country.Id != "QNG")
        {
            foreach (var region in country.Regions) region.RuralPopulation = ruralPopulation / country.Regions.Count;
            country.Regions[0].RuralPopulation += ruralPopulation % country.Regions.Count;
            return;
        }
        int totalWeight = QingProvinceCatalog.Provinces.Sum(p => p.RuralWeight);
        long assigned = 0;
        for (int index = 0; index < country.Regions.Count; index++)
        {
            var region = country.Regions[index];
            region.RuralPopulation = index == country.Regions.Count - 1 ? ruralPopulation - assigned :
                (long)(ruralPopulation * (decimal)QingProvinceCatalog.Province(region.Id).RuralWeight / totalWeight);
            assigned += region.RuralPopulation;
        }
    }

    private static void InitializeCityEconomies(CountryState country)
    {
        foreach (var city in country.Cities)
        {
            foreach (var definition in Catalog.Industries)
                city.Production[definition.Produces] += definition.Output * city.Industries[definition.Id];
            city.Gdp = city.Production.Sum(p => p.Value * Catalog.BasePrices[p.Key]) * 365m;
        }
        SynchronizeGeography(country);
    }

    private static void RefreshCityWorkforce(CityState city)
    {
        if (city.Buildings.Count > 0) { RefreshProductionEmployment(city); return; }
        city.Workforce = (long)(city.Population * .45m);
        city.Dependents = city.Population - city.Workforce;
        // Artisans/services are a local baseline; completed industrial levels create additional jobs.
        city.Artisans = Math.Min(city.Workforce, (long)(city.Population * .22m));
        long jobs = city.Industries.Values.Sum(x => (long)x * 2500);
        city.IndustrialWorkers = Math.Min(city.Workforce - city.Artisans, jobs);
        city.ConstructionWorkers = Math.Min(city.Workforce - city.Artisans - city.IndustrialWorkers,
            (long)city.ConstructionSectors * ConstructionCatalog.WorkersPerSector);
        city.Employed = city.Artisans + city.IndustrialWorkers + city.ConstructionWorkers;
    }

    private static void SynchronizeGeography(CountryState country)
    {
        foreach (var industry in Catalog.Industries) country.Industries[industry.Id] = country.Cities.Sum(c => c.Industries[industry.Id]);
        foreach (var region in country.Regions)
        {
            region.UrbanPopulation = country.Cities.Where(c => c.RegionId == region.Id).Sum(c => c.Population);
            region.Gdp = country.Cities.Where(c => c.RegionId == region.Id).Sum(c => c.Gdp) + region.SubsistenceGdp;
        }
        country.Population = country.Regions.Sum(r => r.Population);
        foreach (var city in country.Cities) city.Construction = country.Construction.Where(p => p.CityId == city.Id).ToList();
    }

    private void TickDemographics(CountryState country, decimal growth)
    {
        foreach (var region in country.Regions) region.RuralPopulation += (long)(region.RuralPopulation * growth);
        foreach (var city in country.Cities)
        {
            city.Population += (long)(city.Population * growth);
            RefreshCityWorkforce(city);
            decimal employment = city.Workforce > 0 ? (decimal)city.Employed / city.Workforce : 0m;
            decimal targetLiving = Clamp(country.LivingStandard - .5m + employment * 2m, 0m, 30m);
            city.LivingStandard = Clamp(city.LivingStandard + (targetLiving - city.LivingStandard) * .008m, 0m, 30m);
            city.Literacy = Clamp(city.Literacy + (country.Literacy + 3m - city.Literacy) * .008m, 0m, 99m);
        }
        if (State.Date.AddDays(1).Day == 1)
        {
            foreach (var city in country.Cities) city.MigrationLastMonth = 0;
            foreach (var region in country.Regions)
            {
                foreach (var city in country.Cities.Where(c => c.RegionId == region.Id).OrderBy(c => c.Id, StringComparer.Ordinal))
                {
                    long jobs = city.Industries.Sum(x => (long)x.Value * (city.Buildings.Count > 0 ? ProductionCatalog.Get(x.Key, city.Buildings[x.Key].MethodId).JobsPerLevel : 2500));
                    long opportunities = Math.Max(0, jobs - city.IndustrialWorkers) + city.Artisans / 30;
                    long migrants = Math.Min(region.RuralPopulation / 2000, Math.Min(opportunities / 8, Math.Max(1, city.Population / 250)));
                    if (country.NeedsFulfilled < .35m) migrants = 0;
                    region.RuralPopulation -= migrants;
                    city.Population += migrants;
                    city.MigrationLastMonth = migrants;
                    RefreshCityWorkforce(city);
                }
            }
        }
        SynchronizeGeography(country);
    }
}
