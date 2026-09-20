using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sovereign.Simulation;

/// <summary>A dated content interval, not necessarily an entire reign. All dates use the game's Gregorian calendar.</summary>
public sealed record LeaderDefinition(
    string Id, string CountryId, string Name, string Title,
    DateTime StartDate, DateTime EndDateExclusive, DateTime? BirthDate,
    string SourceUrl, string AppearanceKey, string Description,
    string? SecondaryOffice = null, string AppearanceNotes = "", string PortraitSourceUrl = "")
{
    public int? AgeOn(DateTime date)
    {
        if (BirthDate is not DateTime birth) return null;
        int years = date.Year - birth.Year;
        if (birth.AddYears(years) > date.Date) years--;
        return years;
    }
}

/// <summary>
/// Historical officeholders for the supported campaign only. Political reforms do not yet create alternate successions.
/// The selected figure is a head of state except Japan, where it is the governing shogun and the emperor is identified separately.
/// </summary>
public static class HistoricalLeaders
{
    public static DateTime CoverageStart { get; } = new(1836, 1, 1);
    public static DateTime CoverageEndExclusive { get; } = new(1846, 1, 2);
    private const string Start = "1836-01-01";
    private const string End = "1846-01-02";
    private const string UsTerms = "https://www.archives.gov/presidential-records/research/additional-research-resources";
    private const string Spain = "https://www.congreso.es/es/cem/regespartero";
    private const string Prussia = "https://www.dhm.de/lemo/jahreschronik/1840";
    private const string Japan = "https://shoryobu.kunaicho.go.jp/Ryobo/Detail/1000088960000?index=125371&searchtype=Freeword&sort=Title";
    private const string Provisional = "Provisional stylized reconstruction; identity and office are historical, but facial likeness, clothing details and colours require art review. ";

    public static IReadOnlyList<LeaderDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        L("GBR", "william_iv", "William IV", "King of the United Kingdom", Start, "1837-06-20", "1765-08-21",
            "https://www.royal.uk/william-iv", "Constitutional monarch and former naval officer. His niece Victoria succeeds him on 20 June 1837.",
            "Cabinet government; the monarch is distinct from the prime minister.",
            "Elderly man, receding silver hair, clean-shaven face, naval dress coat and gold epaulettes.",
            "https://www.hrp.org.uk/hampton-court-palace/history-and-stories/william-iv/"),
        L("GBR", "victoria", "Victoria", "Queen of the United Kingdom", "1837-06-20", End, "1819-05-24",
            "https://www.royal.uk/timeline-queen-victoria-and-prince-albert", "Victoria becomes queen at eighteen. This campaign depicts her early reign, before the later imperial titles and mourning dress.",
            "Cabinet government; the monarch is distinct from the prime minister.",
            "Young woman, centre-parted brown hair gathered at the sides, early Victorian court gown and small diadem; no widow's veil.",
            "https://www.royal.uk/encyclopedia/victoria-r-1837-1901"),
        L("PRU", "frederick_william_iii", "Frederick William III", "King of Prussia", Start, "1840-06-07", null,
            Prussia, "Prussia's reigning monarch at the campaign opening. Frederick William IV succeeds him following his death on 7 June 1840.", null,
            "Older, long-faced man, receding greying hair, clean-shaven chin; dark Prussian military coat with high collar and orders."),
        L("PRU", "frederick_william_iv", "Frederick William IV", "King of Prussia", "1840-06-07", End, "1795-10-15",
            Prussia, "Succeeds his father on 7 June 1840. The October homage ceremony is distinct from this succession.", null,
            "Middle-aged man, fuller cheeks, short side-parted hair and side whiskers, dark military coat and sash.",
            "https://www.dhm.de/lemo/biografie/friedrich-wilhelm-iv"),
        L("JAP", "tokugawa_ienari", "Tokugawa Ienari", "Shogun of the Tokugawa government", Start, "1837-10-01", null,
            Japan, "The shogun heads the military government in Edo. Ienari transfers the office to Ieyoshi in 1837 and retains influence after retirement.",
            "Emperor Ninko in Kyoto: imperial and ceremonial sovereign, distinct from the shogun.",
            "Older man in formal Japanese court robes and black ceremonial headgear; clean-shaven. Costume and likeness are provisional."),
        L("JAP", "tokugawa_ieyoshi", "Tokugawa Ieyoshi", "Shogun of the Tokugawa government", "1837-10-01", End, null,
            Japan, "Becomes the twelfth Tokugawa shogun on Tenpo 8, ninth month, second day (1 October 1837 Gregorian). Governs through the bakufu and senior councillors.",
            "Emperor Ninko in Kyoto: imperial and ceremonial sovereign, distinct from the shogun.",
            "Middle-aged man in formal court robes with broad shoulders and black ceremonial headgear; clean-shaven. Costume and likeness are provisional."),
        L("FRA", "louis_philippe", "Louis-Philippe I", "King of the French", Start, End, null,
            "https://www.chateauversailles.fr/decouvrir/ressources/bac-avec-versailles", "Head of the July Monarchy. He swore the constitutional oath in August 1830; government also depends on ministers and the chambers.", null,
            "Older man, swept greying hair and substantial side whiskers, blue military coat, sash and gold epaulettes.",
            "https://www.chateauversailles.fr/decouvrir/histoire/grands-personnages/louis-philippe-ier"),
        L("AUS", "ferdinand_i", "Ferdinand I", "Emperor of Austria", Start, End, "1793-04-19",
            "https://www.habsburger.net/de/personen/habsburger-herrscher/ferdinand-i-0", "Emperor from 1835. State affairs were strongly shaped by senior ministers and the State Conference; the emperor should not be mistaken for the sole policymaker.",
            "State Chancellor Metternich and the State Conference exercise substantial governing authority.",
            "Long-faced man with receding dark hair, clean-shaven chin, white Austrian military coat, red sash and gold orders.",
            "https://www.habsburger.net/de/personen/habsburger-herrscher/ferdinand-i-0"),
        L("RUS", "nicholas_i", "Nicholas I", "Emperor of Russia", Start, End, null,
            "https://www.loc.gov/resource/gdcwdl.wdl_17159/?st=pdf", "The reigning Russian emperor throughout this campaign. Imperial authority operates through ministers and the bureaucracy.", null,
            "Tall military bearing, receding brown hair and pronounced moustache, dark green military tunic with epaulettes and orders.",
            "https://www.si.edu/object/nicholas-i%3Anpg_S_NPG.68.2"),
        L("USA", "andrew_jackson", "Andrew Jackson", "President of the United States", Start, "1837-03-04", "1767-03-15",
            UsTerms, "Seventh president, serving his second term at the campaign opening. Presidency, Congress and the states are separate institutions.", null,
            "Elderly man with swept-back silver hair, lean face and high white collar under a dark civilian coat.",
            "https://www.whitehousehistory.org/bios/andrew-jackson"),
        L("USA", "martin_van_buren", "Martin Van Buren", "President of the United States", "1837-03-04", "1841-03-04", "1782-12-05",
            UsTerms, "Eighth president, inaugurated on 4 March 1837. The presidency does not replace the separate authority of Congress or the states.", null,
            "Receding pale hair, prominent pale side whiskers, high white collar and dark civilian coat.",
            "https://www.nps.gov/people/martin-van-buren-and-the-amistad.htm"),
        L("USA", "william_henry_harrison", "William Henry Harrison", "President of the United States", "1841-03-04", "1841-04-04", "1773-02-09",
            UsTerms, "Ninth president. His brief term ends with his death on 4 April 1841; Vice President John Tyler succeeds him.", null,
            "Older man, sparse swept grey hair, long face, clean-shaven chin and formal dark civilian coat.",
            "https://www.nps.gov/people/william-henry-harrison.htm"),
        L("USA", "john_tyler", "John Tyler", "President of the United States", "1841-04-04", "1845-03-04", "1790-03-29",
            UsTerms, "Succeeds Harrison on 4 April 1841 and takes the presidential oath on 6 April. The succession and oath are separate dates.", null,
            "Long narrow face, receding brown hair, clean-shaven chin, high collar and dark civilian coat.",
            "https://guides.loc.gov/john-tyler"),
        L("USA", "james_polk", "James K. Polk", "President of the United States", "1845-03-04", End, "1795-11-02",
            UsTerms, "Eleventh president, inaugurated on 4 March 1845. This content covers the opening portion of his presidency.", null,
            "Lean face, long swept-back dark hair, clean-shaven chin, white shirt and black civilian coat.",
            "https://www.whitehousehistory.org/bios/james-polk"),
        L("QNG", "daoguang", "Daoguang Emperor", "Emperor of the Qing dynasty", Start, End, null,
            "https://www.dpm.org.cn/court/lineage/226239.html", "The Qing emperor throughout the campaign. The Daoguang era begins in 1821, following his accession in 1820.", null,
            "Court portrait convention: Qing court hat with red crown, yellow robe, dark narrow moustache and small beard. Proportions remain an interpretation.",
            "https://www.dpm.org.cn/court/lineage/226239.html"),
        L("OTT", "mahmud_ii", "Mahmud II", "Sultan of the Ottoman Empire", Start, "1839-07-01", "1785-07-20",
            "https://islamansiklopedisi.org.tr/mahmud-ii--osmanli", "Reforming Ottoman sultan. His son Abdulmejid I accedes on 1 July 1839.", null,
            "Middle-aged man with dark beard and moustache, red fez and dark reformed military coat; exact decorations are provisional."),
        L("OTT", "abdulmejid_i", "Abdulmejid I", "Sultan of the Ottoman Empire", "1839-07-01", End, "1823-04-25",
            "https://islamansiklopedisi.org.tr/abdulmecid", "Accedes on 1 July 1839. The Tanzimat reform proclamation follows in November of that year.", null,
            "Young man, red fez, dark military-style coat and restrained moustache; avoid depicting the accession-aged sixteen-year-old as an elderly sultan.",
            "https://islamansiklopedisi.org.tr/abdulmecid"),
        Isabella(Start, "1840-10-12", "Regent: Maria Christina of the Two Sicilies. Isabella is a child monarch."),
        Isabella("1840-10-12", "1841-05-10", "Interim regency exercised by the ministry under Baldomero Espartero; his personal regency is formalised in May 1841."),
        Isabella("1841-05-10", "1843-07-30", "Regent: Baldomero Espartero, elected by the Cortes on 8 May and sworn in on 10 May 1841."),
        Isabella("1843-07-30", "1843-11-10", "Provisional government under Joaquin Maria Lopez after Espartero's fall; the transition ends with the queen's constitutional oath."),
        Isabella("1843-11-10", End, "Personal reign after the declaration of majority and constitutional oath in November 1843; ministers and Cortes remain distinct."),
        L("POR", "maria_ii", "Maria II", "Queen of Portugal and the Algarves", Start, End, "1819-04-04",
            "https://www.parlamento.pt/Parlamento/Paginas/Percursos-D-Maria-II.aspx", "Queen during the restored constitutional monarchy. She was declared of age in 1834 and remains sovereign throughout this campaign.", null,
            "Young woman, dark centre-parted hair in side curls, period court gown, jewellery and diadem; sixteen at the opening date.",
            "https://www.parlamento.pt/VisitaParlamento/Paginas/BiogDMariaII.aspx"),
        L("BEL", "leopold_i", "Leopold I", "King of the Belgians", Start, End, null,
            "https://www.monarchie.be/en/royal-family/history", "First king of the Belgians, taking the constitutional oath on 21 July 1831. He remains king throughout this campaign.",
            "Constitutional monarchy with ministers and parliament.",
            "Dark side-parted hair, side whiskers, clean-shaven chin, dark military coat with sash and gold orders.",
            "https://www.monarchie.be/en/royal-family/history")
    });

    public static LeaderDefinition Get(string countryId, DateTime date)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryId);
        DateTime day = date.Date;
        if (day < CoverageStart || day >= CoverageEndExclusive)
            throw new ArgumentOutOfRangeException(nameof(date), "Historical leaders are researched for 1 January 1836 through 1 January 1846 only.");
        return All.FirstOrDefault(x => x.CountryId == countryId && day >= x.StartDate && day < x.EndDateExclusive)
            ?? throw new ArgumentException($"No researched historical leader for country '{countryId}'.", nameof(countryId));
    }

    private static LeaderDefinition Isabella(string start, string end, string office) =>
        L("SPA", "isabella_ii", "Isabella II", "Queen of Spain", start, end, "1830-10-10", Spain,
            "Reigning queen since 1833, but a child at the campaign opening. Regency and subsequent personal rule are dated separately.", office,
            "Age must follow the date: five at campaign opening and thirteen in November 1843. Dark hair, child-appropriate court dress and small royal diadem; adult portrait proportions must not be used for the child.",
            "https://www.museodelprado.es/coleccion/obra-de-arte/isabel-ii/7e29b255-a21a-46bf-b457-925b14a5ea1e");

    private static LeaderDefinition L(string country, string id, string name, string title, string start, string end,
        string? birth, string source, string description, string? secondary, string appearance, string portrait = "") =>
        new(id, country, name, title, D(start), D(end), birth is null ? null : D(birth), source, id, description,
            secondary, Provisional + appearance, portrait);

    private static DateTime D(string value) => DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
