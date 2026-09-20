# Historical officeholders: campaign content register

Checked 19 September 2026. The playable historical content currently covers **12 countries, 20 distinct people, 24 dated leader records, 1 January 1836–1 January 1846 inclusive**. This is a bounded scenario roster, not every historical country or leader for 1836–1936.

`Simulation/HistoricalLeaders.cs` is the executable register. `Get(countryId, date)` selects a record on the Gregorian game calendar. Its `StartDate` and `EndDateExclusive` delimit the **supported content interval**, not a complete reign. Incumbents are clipped to the campaign opening and records end on 2 January 1846 exclusively so the terminal campaign day remains covered. Out-of-range dates and unknown countries are rejected instead of inventing a ruler.

The current succession track follows the historical calendar. Player reforms do not yet create alternate dynasties, elections or deaths. A full political character simulation remains future work.

## Office and succession evidence

| Country | Included identities and changes | Principal evidence |
|---|---|---|
| United Kingdom | William IV → Victoria, 20 June 1837. Heads of state, distinct from the prime minister. | [Royal Archives: accession and coronation records](https://www.royal.uk/royal-archives-coronation), [William IV biography](https://www.royal.uk/william-iv), [Victoria chronology](https://www.royal.uk/timeline-queen-victoria-and-prince-albert). |
| Prussia | Frederick William III → Frederick William IV, 7 June 1840. | [Deutsches Historisches Museum, 1840 chronology](https://www.dhm.de/lemo/jahreschronik/1840). This gives succession at the father's death; the October homage ceremony must not postpone the change. |
| Japan | Tokugawa Ienari → Tokugawa Ieyoshi, 1 October 1837 Gregorian. The selected ruler is the governing shogun. Emperor Ninko is named separately as imperial and ceremonial sovereign in Kyoto. | [Imperial Household Agency primary catalogue record](https://shoryobu.kunaicho.go.jp/Ryobo/Detail/1000088960000?index=125371&searchtype=Freeword&sort=Title) dates Ieyoshi's appointment to Tenpo 8, ninth month, second day; [Kasugai municipal historical journal](https://www.city.kasugai.lg.jp/bunka/bunkazai/1023948/kyodoshikasugai/1004471/kyodoshi17.html) confirms that succession date in the original calendar. [National Diet Library study](https://dl.ndl.go.jp/view/download/digidepo_11095235_po_080801.pdf?contentNo=1) identifies Ninko's 1817–1846 reign. |
| France | Louis-Philippe I, King of the French. | [Palace of Versailles, July Monarchy account](https://www.chateauversailles.fr/decouvrir/ressources/bac-avec-versailles). |
| Austria | Ferdinand I; the text identifies Metternich and the State Conference rather than assigning every governing action personally to the emperor. | [World of the Habsburgs / Schönbrunn, Ferdinand I](https://www.habsburger.net/de/personen/habsburger-herrscher/ferdinand-i-0). |
| Russia | Nicholas I. | [Library of Congress / Russian State Archive portrait record and biographical context](https://www.loc.gov/resource/gdcwdl.wdl_17159/?st=pdf). |
| United States | Jackson → Van Buren, 4 March 1837; Harrison, 4 March 1841; Tyler, 4 April 1841; Polk, 4 March 1845. | [National Archives, presidential administrations](https://www.archives.gov/presidential-records/research/additional-research-resources). [1840 election record](https://www.archives.gov/electoral-college/1840) distinguishes Harrison's death and Tyler's succession from Tyler's oath on 6 April. |
| Qing | Daoguang Emperor. | [Palace Museum, Daoguang lineage and chronology](https://www.dpm.org.cn/court/lineage/226239.html). The accession in 1820 is distinct from the era name beginning in 1821. |
| Ottoman Empire | Mahmud II → Abdulmejid I, 1 July 1839. | [TDV Islamic Research Centre: Mahmud II](https://islamansiklopedisi.org.tr/mahmud-ii--osmanli), [Abdulmejid](https://islamansiklopedisi.org.tr/abdulmecid). The transition follows the latter's explicitly dated accession. |
| Spain | Isabella II remains queen; government context changes across regencies, provisional administration and personal rule. | [Congress of Deputies, regency history](https://www.congreso.es/es/cem/regespartero), [Royal Matritense Academy study of nineteenth-century governments](https://www.ramhg.es/wp-content/uploads/2024/10/anales-26_2023-04_de_francisco_olmos.pdf). |
| Portugal | Maria II, a reigning constitutional queen, already declared of age before campaign opening. | [Portuguese Parliamentary Historical Archive, documents of Maria II](https://www.parlamento.pt/Parlamento/Paginas/Percursos-D-Maria-II.aspx). |
| Belgium | Leopold I, constitutional King of the Belgians. | [Belgian Monarchy, historical record](https://www.monarchie.be/en/royal-family/history). |

Japan's original lunisolar date is converted to **1 October**, not read as Gregorian 2 September. The Gregorian date is also given in W. G. Beasley's *The Modern History of Japan*, opening discussion of the late Tokugawa period ([digitised text](https://cristoraul.org/english/readinghall/Doors-of-Wisdom/History_of_Japan/library/The-Modern-History-Of-Japan.pdf)). Calendar conversion is an editorial normalisation; it does not change the primary record's notation.

Spain uses these office-context boundaries: Maria Christina to 12 October 1840; interim ministerial regency to 10 May 1841; Espartero's personal regency to 30 July 1843; provisional government to 10 November 1843; then the queen's personal reign. The election of Espartero on 8 May is distinguished from his oath on 10 May. The final boundary denotes Isabella's constitutional oath, **not** her birth, original accession, or the parliamentary declaration of majority. Congress's page body dates the oath to 10 November; an image caption on that page says October. We follow the body and the contemporary proceedings, not that inconsistent caption. The [Gaceta of 9 November 1843](https://www.boe.es/gazeta/dias/1843/11/09/pdfs/GMD-1843-3340.pdf) records the debates on her majority immediately before the oath. The interim administration is not represented as a new monarch.

## Birth dates and age

Exact dates are populated only where checked; other records keep `BirthDate = null` and the UI should omit an exact age. Appearance may suggest an age range, but that is art direction, not a substituted birth date.

- William IV: [Royal Collection Trust archival biography](https://www.rct.uk/collection/royal-archives/georgian-papers-in-the-royal-archives/william-iv/william-iv).
- Victoria: [Royal Household biography](https://www.royal.uk/encyclopedia/victoria-r-1837-1901).
- Frederick William IV: [DHM biography](https://www.dhm.de/lemo/biografie/friedrich-wilhelm-iv).
- Ferdinand I, Mahmud II, Abdulmejid I and Maria II: their institutional biographies above.
- Jackson: [White House Historical Association](https://www.whitehousehistory.org/bios/andrew-jackson); Van Buren: [National Park Service](https://www.nps.gov/people/martin-van-buren-and-the-amistad.htm); Harrison: [National Park Service](https://www.nps.gov/people/william-henry-harrison.htm); Tyler: [Library of Congress](https://guides.loc.gov/john-tyler); Polk: [North Carolina Historic Sites](https://historicsites.nc.gov/all-sites/president-james-k-polk/history).
- Isabella II: [Revista de las Cortes Generales, Prado collection study](https://revista.cortesgenerales.es/rcg/article/download/797/1261/). She is five at the campaign opening; Maria II is sixteen; Victoria is eighteen and Abdulmejid sixteen at their accessions in this scenario. The renderer must respect these differences.

## Visual identity and evidence limits

The 3D figures use original procedural geometry. They are **provisional stylized reconstructions**, not scanned faces, verified physical likenesses, or finished portrait assets. No external portrait image is included as a texture. Clothing colours, face proportions, ornaments and animation remain art interpretation and need historical and visual review.

`AppearanceKey` gives every individual a stable identity. `AppearanceNotes` is a design brief and explicitly identifies its provisional status. `PortraitSourceUrl`, where populated, links to an institutional reference page for later visual research; a blank value means no portrait reference was selected. A linked portrait does not imply that its image was downloaded, copied, licensed for use, or that its costume is appropriate for every year. In particular, the linked Prado portrait of Isabella is based on her later appearance and **must not** define the child seen in 1836.

Additional collection references used in the register include the [Smithsonian's 1840 Nicholas I portrait](https://www.si.edu/object/nicholas-i%3Anpg_S_NPG.68.2), [Prado's Isabella II record](https://www.museodelprado.es/coleccion/obra-de-arte/isabel-ii/7e29b255-a21a-46bf-b457-925b14a5ea1e), [Versailles' Louis-Philippe collection context](https://www.chateauversailles.fr/decouvrir/histoire/grands-personnages/louis-philippe-ier), and [Portuguese Parliament's Maria II portrait](https://www.parlamento.pt/VisitaParlamento/Paginas/BiogDMariaII.aspx).

## Data boundaries

Historical identities and these office dates do not certify the broader scenario as a historical reconstruction. Economy starting values, city populations, demographic shares, production balances and procedural building layouts are gameplay abstractions unless their own content record identifies a source. A leader's presence does not imply that national institutions, colonial administration, ministers or succession politics are fully simulated.

### Territory and city administration

`GeographyCatalog` is a curated 96-city scenario, not a reconstruction of every administrative district, border or occupation. A city's country assignment currently selects its gameplay treasury and market. The engine does not yet separate a sovereign claim from the authority administering a city, local autonomy, or temporary military occupation.

**Damascus is a known simplification:** its assignment to the Ottoman scenario reflects imperial sovereignty, while Egyptian forces and government controlled it during the opening years. Egyptian troops took the city in 1832, and Ottoman administration returned in 1840. Treating its production and taxes as directly controlled from Constantinople in 1836 is consequently a gameplay abstraction. [Research on Damascus's administration and political society](https://www.sciencedirect.com/science/article/abs/pii/S1081602X11000182) documents the distinction; the [text of the 1840 London Convention in the US diplomatic archive](https://history.state.gov/historicaldocuments/frus1879/d518) supplies contemporary evidence of the wider sovereignty and control dispute. Egypt, its controller relationship and this transfer are not separately simulated.

The British scenario includes Dublin and therefore represents the **United Kingdom of Great Britain and Ireland**, established by the union effective in 1801; “Great Britain” is a shortened scenario label, not the full territorial name. [Parliamentary Archives, Act of Union](https://www.parliament.uk/about/living-heritage/evolutionofparliament/legislativescrutiny/parliamentandireland/collections/ireland/act-of-union-1800/).

Japan's capital marker denotes the shogunal governing seat at Edo; Kyoto remains the imperial seat described in the leader records. Domain governments and their autonomy are not modelled as separate controllers. Region groupings such as “Alpine and Bohemian lands” and “Northeastern states” are interface groupings, not historical legal subdivisions; Washington belongs to a federal district rather than a state. Rounded coordinates identify a city's general location, not surveyed 1836 boundaries or its historical built-up extent.
