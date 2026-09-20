using System;
using System.Collections.Generic;

namespace Sovereign.Simulation;

public static class Catalog
{
    public static readonly DateTime StartDate = new(1836, 1, 1);
    public static readonly DateTime EndDate = new(1846, 1, 1);
    public static IReadOnlyList<string> Goods { get; } = new[] { "grain", "timber", "coal", "iron", "tools", "clothes" };
    public static IReadOnlyDictionary<string, decimal> BasePrices { get; } = new Dictionary<string, decimal>
    { ["grain"] = 2m, ["timber"] = 3m, ["coal"] = 4m, ["iron"] = 5m, ["tools"] = 12m, ["clothes"] = 7m };
    public static IReadOnlyList<IndustryDefinition> Industries { get; } = new[]
    {
        new IndustryDefinition("farm", "Commercial farms", "Grow grain for households. More food improves living standards.", 1400m, 45, "grain", 18m, Input(), 35m, 12m),
        new IndustryDefinition("lumber", "Logging camps", "Supply timber for heating, workshops and construction.", 1700m, 55, "timber", 12m, Input(), 40m, 16m),
        new IndustryDefinition("coal_mine", "Coal mines", "Extract coal used by iron mines and tool workshops.", 2600m, 75, "coal", 10m, Input(), 50m, 24m),
        new IndustryDefinition("iron_mine", "Iron mines", "Turn coal into iron ore and metal for the tool industry.", 2800m, 75, "iron", 7m, Input(("coal", 3m)), 55m, 25m),
        new IndustryDefinition("toolworks", "Tool workshops", "Use iron, coal and timber to make tools for industrial expansion.", 3800m, 90, "tools", 6m, Input(("iron", 4m), ("coal", 2m), ("timber", 2m)), 70m, 30m),
        new IndustryDefinition("textile", "Textile mills", "Use tools to produce the household clothing basket.", 3200m, 80, "clothes", 10m, Input(("tools", 0.7m)), 65m, 25m)
    };
    public static IReadOnlyList<ReformDefinition> Reforms { get; } = new[]
    {
        new ReformDefinition("primary_schools", "Primary education", "Literacy grows faster; research advances faster. Costs 12 per day.", 2200m, 12m),
        new ReformDefinition("labor_protection", "Factory safeguards", "Improves living standards and calms unrest; output falls by 4%. Costs 8 per day.", 1800m, 8m),
        new ReformDefinition("public_health", "Public health boards", "Improves living standards and population growth. Costs 16 per day.", 3000m, 16m)
    };
    public static IReadOnlyList<TechnologyDefinition> Technologies { get; } = new[]
    {
        new TechnologyDefinition("mechanical_tools", "Mechanical tools", "Raises all industrial output by 12%.", 300),
        new TechnologyDefinition("railways", "Early railways", "Raises output by 10% and trade capacity by 50%.", 480),
        new TechnologyDefinition("steam_power", "Improved steam engines", "Raises output by 15%. Requires Mechanical tools.", 600)
    };
    private static IReadOnlyDictionary<string, decimal> Input(params (string good, decimal amount)[] values)
    {
        var result = new Dictionary<string, decimal>();
        foreach (var value in values) result[value.good] = value.amount;
        return result;
    }
}
