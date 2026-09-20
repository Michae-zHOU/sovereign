using System;
using System.Linq;
using Sovereign.Simulation;

/// <summary>Historical content contract, independent of rendering and simulation tick order.</summary>
public static class LeaderChecks
{
    public static void Run()
    {
        string[] countries = { "GBR", "PRU", "JAP", "FRA", "AUS", "RUS", "USA", "QNG", "OTT", "SPA", "POR", "BEL" };
        Check(countries.Order().SequenceEqual(HistoricalLeaders.All.Select(x => x.CountryId).Distinct().Order()),
            "Historical leaders cover exactly the twelve supported countries.");
        Check(HistoricalLeaders.All.Select(x => x.Id).Distinct().Count() == 20, "Twenty distinct historical identities.");
        Check(HistoricalLeaders.All.Count == 24, "Twenty-four office-context intervals.");

        int checkedDays = 0;
        foreach (string country in countries)
        for (DateTime day = HistoricalLeaders.CoverageStart; day < HistoricalLeaders.CoverageEndExclusive; day = day.AddDays(1))
        {
            var matching = HistoricalLeaders.All.Where(x => x.CountryId == country && day >= x.StartDate && day < x.EndDateExclusive).ToArray();
            Check(matching.Length == 1, $"Exactly one leader interval for {country} on {day:yyyy-MM-dd}; found {matching.Length}.");
            Check(HistoricalLeaders.Get(country, day) == matching[0], "Lookup returns the active interval.");
            checkedDays++;
        }
        Check(checkedDays == 43_848, "All 3,654 campaign dates, including the terminal day, checked for each country.");

        Transition("GBR", new(1837, 6, 20), "william_iv", "victoria");
        Transition("PRU", new(1840, 6, 7), "frederick_william_iii", "frederick_william_iv");
        Transition("JAP", new(1837, 10, 1), "tokugawa_ienari", "tokugawa_ieyoshi");
        Transition("OTT", new(1839, 7, 1), "mahmud_ii", "abdulmejid_i");
        Transition("USA", new(1837, 3, 4), "andrew_jackson", "martin_van_buren");
        Transition("USA", new(1841, 3, 4), "martin_van_buren", "william_henry_harrison");
        Transition("USA", new(1841, 4, 4), "william_henry_harrison", "john_tyler");
        Transition("USA", new(1845, 3, 4), "john_tyler", "james_polk");
        Check(HistoricalLeaders.Get("USA", new(1841, 4, 5)).Id == "john_tyler", "Tyler succeeds before taking the oath on April 6.");

        var openingIsabella = HistoricalLeaders.Get("SPA", new(1836, 1, 1));
        Check(openingIsabella.AgeOn(new(1836, 1, 1)) == 5, "Isabella is five, not an adult at campaign opening.");
        Check(openingIsabella.AgeOn(new(1836, 10, 9)) == 5 && openingIsabella.AgeOn(new(1836, 10, 10)) == 6,
            "Age changes on the birthday, not January 1.");
        Check(HistoricalLeaders.Get("GBR", new(1837, 6, 20)).AgeOn(new(1837, 6, 20)) == 18, "Victoria is eighteen at accession.");
        Check(HistoricalLeaders.Get("OTT", new(1839, 7, 1)).AgeOn(new(1839, 7, 1)) == 16, "Abdulmejid is sixteen at accession.");
        Check(HistoricalLeaders.Get("POR", new(1836, 1, 1)).AgeOn(new(1836, 1, 1)) == 16, "Maria II is sixteen at campaign opening.");

        Office("SPA", new(1840, 10, 11), "Maria Christina");
        Office("SPA", new(1840, 10, 12), "Interim");
        Office("SPA", new(1841, 5, 9), "Interim");
        Office("SPA", new(1841, 5, 10), "Regent: Baldomero Espartero");
        Office("SPA", new(1843, 7, 29), "Regent: Baldomero Espartero");
        Office("SPA", new(1843, 7, 30), "Provisional");
        Office("SPA", new(1843, 11, 9), "Provisional");
        Office("SPA", new(1843, 11, 10), "Personal reign");
        Check(HistoricalLeaders.All.Where(x => x.CountryId == "SPA").All(x => x.Id == "isabella_ii"),
            "A regency change does not replace the reigning Spanish monarch.");
        Check(HistoricalLeaders.All.Where(x => x.CountryId == "JAP").All(x => x.SecondaryOffice?.Contains("Emperor Ninko", StringComparison.Ordinal) == true),
            "Japan distinguishes the emperor from the governing shogun.");

        foreach (LeaderDefinition leader in HistoricalLeaders.All)
        {
            Check(leader.StartDate < leader.EndDateExclusive, "Leader interval is nonempty.");
            Check(Uri.TryCreate(leader.SourceUrl, UriKind.Absolute, out var source) && source.Scheme == "https", "Every identity has a historical source URL.");
            Check(!string.IsNullOrWhiteSpace(leader.AppearanceKey), "Every identity has a rendering key.");
            Check(leader.AppearanceNotes.Contains("Provisional", StringComparison.Ordinal), "Likeness is labelled as provisional.");
            if (leader.BirthDate == null) Check(leader.AgeOn(leader.StartDate) == null, "Missing birth date must not invent an exact age.");
        }
        Check(HistoricalLeaders.Get("GBR", new(1846, 1, 1, 23, 59, 59)).Id == "victoria", "Terminal date is covered regardless of time component.");
        Reject<ArgumentOutOfRangeException>(() => HistoricalLeaders.Get("GBR", new(1835, 12, 31)));
        Reject<ArgumentOutOfRangeException>(() => HistoricalLeaders.Get("GBR", new(1846, 1, 2)));
        Reject<ArgumentException>(() => HistoricalLeaders.Get("XYZ", new(1836, 1, 1)));
        Reject<ArgumentException>(() => HistoricalLeaders.Get(" ", new(1836, 1, 1)));
    }

    private static void Transition(string country, DateTime date, string previous, string current)
    {
        Check(HistoricalLeaders.Get(country, date.AddDays(-1)).Id == previous, $"Correct incumbent before {country} succession.");
        Check(HistoricalLeaders.Get(country, date).Id == current, $"Correct incumbent on {country} succession day.");
    }

    private static void Office(string country, DateTime date, string expected) =>
        Check(HistoricalLeaders.Get(country, date).SecondaryOffice?.Contains(expected, StringComparison.Ordinal) == true,
            $"Correct governing context for {country} on {date:yyyy-MM-dd}.");

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Historical leader check: " + message);
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name} from invalid historical lookup.");
    }
}
