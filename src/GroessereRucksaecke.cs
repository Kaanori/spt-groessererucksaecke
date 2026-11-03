// ---------------------------------------------------------------
//  Groessere Rucksaecke
//
//  Vergroessert beim Serverstart die Fachaufteilung aller Rucksaecke
//  um einen einstellbaren Faktor (Standard: 1,5 = +50 %).
//
//  Warum ein eigener Mod:
//  SVM kann Taschen, Sicherheitsbehaelter und die Behaelterkisten -
//  Rucksaecke aber NICHT. Im ganzen SVM steht zu Rucksaecken nur
//  BackpackStacking und RemoveBackpacksRestrictions, nichts zur
//  Groesse. Und die Datenbank direkt anfassen geht seit SPT 4.1
//  nicht mehr: sie ist pruefsummengesichert, eine geaenderte
//  items.json bricht den Serverstart ab. Mods duerfen dieselbe
//  Tabelle zur Laufzeit aendern - dafuer sind sie da.
//
//  Kein Streit mit SVM: SVM fasst Rucksaecke nirgends an, die
//  Ladereihenfolge ist deshalb egal.
//
//  Der Faktor steht in rucksaecke.json neben dieser DLL, damit man
//  ihn ohne Neubau aendern kann.
// ---------------------------------------------------------------
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Common.Models.Logging;

// „Path“ gibt es auch in den EFT-Modellen (Wegpunkte) - Alias setzen.
using Pfad = System.IO.Path;

namespace GroessereRucksaecke;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.kaan.groessererucksaecke";
    public string Name { get; init; } = "Groessere Rucksaecke";
    public string Author { get; init; } = "Kaan";
    public List<string>? Contributors { get; init; } = new();
    public SemanticVersioning.Version Version { get; init; } = new(1, 0, 0);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; } = new();
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new();
    public string? Url { get; init; } = "";
    public string License { get; init; } = "MIT";
}

[Injectable(InjectionType.Singleton)]
public class RucksackVergroesserer(
    ISptLogger<RucksackVergroesserer> logger,
    TemplateTable vorlagen) : IOnLoad
{
    // Basisklasse "Backpack" - daran haengen alle Rucksackvorlagen.
    private const string RucksackBasis = "5448e53e4bdc2d60728b4567";

    public Task OnLoadAsync(CancellationToken abbruch)
    {
        try
        {
            var (faktor, ausnahmen) = EinstellungenLesen();
            if (faktor <= 1.0)
            {
                logger.Info($"[Groessere Rucksaecke] Faktor {faktor} - nichts zu tun.");
                return Task.CompletedTask;
            }

            int anzahl = 0, felderAlt = 0, felderNeu = 0;

            foreach (var (id, vorlage) in vorlagen.Items)
            {
                if (vorlage.Parent != RucksackBasis) continue;
                if (ausnahmen.Contains(id.ToString())) continue;

                var gitter = vorlage.Properties?.Grids?.ToList();
                if (gitter is null || gitter.Count == 0) continue;

                var vorher = 0;
                var nachher = 0;
                foreach (var fach in gitter)
                {
                    var eig = fach.Properties;
                    // CellsH/CellsV sind int? - ohne Werte ist das Fach
                    // nichts, was wir vergroessern koennten.
                    if (eig?.CellsH is not int breit || eig.CellsV is not int hoch) continue;
                    if (breit <= 0 || hoch <= 0) continue;

                    vorher += breit * hoch;
                    var (neuBreit, neuHoch) = Skaliere(breit, hoch, faktor);
                    eig.CellsH = neuBreit;
                    eig.CellsV = neuHoch;
                    nachher += neuBreit * neuHoch;
                }

                if (vorher == 0) continue;

                // Grids ist ein IEnumerable - die Liste zurueckschreiben,
                // so wie es auch SVM bei den Behaeltern macht.
                vorlage.Properties!.Grids = gitter;
                anzahl++;
                felderAlt += vorher;
                felderNeu += nachher;
            }

            if (anzahl == 0)
            {
                logger.Warning("[Groessere Rucksaecke] Keine Rucksackvorlagen gefunden - "
                               + "hat sich die Basisklasse geaendert?");
                return Task.CompletedTask;
            }

            var zuwachs = (felderNeu / (double)felderAlt - 1) * 100;
            logger.Success($"[Groessere Rucksaecke] {anzahl} Rucksaecke: "
                           + $"{felderAlt} -> {felderNeu} Faecher ({zuwachs:+0.0;-0.0} %), "
                           + $"Faktor {faktor}.");
        }
        catch (Exception ex)
        {
            logger.Error("[Groessere Rucksaecke] " + ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sucht die Fachgroesse, deren Feldzahl dem Faktor am naechsten kommt.
    /// Erstes Kriterium ist die Feldzahl, zweites das Seitenverhaeltnis -
    /// damit bleibt die Form des Fachs erkennbar und aus einem breiten
    /// Rucksack wird kein schmaler Turm. Gesucht wird nur zwischen der
    /// Ausgangsgroesse und dem Doppelten; groesser wird bei Faktor 1,5
    /// ohnehin nie gebraucht.
    /// </summary>
    internal static (int Breit, int Hoch) Skaliere(int breit, int hoch, double faktor)
    {
        var ziel = (int)Math.Round(breit * hoch * faktor, MidpointRounding.AwayFromZero);
        var seite = (double)breit / hoch;

        var bestBreit = breit;
        var bestHoch = hoch;
        var bestAbstand = int.MaxValue;
        var bestSeite = double.MaxValue;

        var grenzeB = (int)Math.Ceiling(breit * Math.Max(faktor, 1.0));
        var grenzeH = (int)Math.Ceiling(hoch * Math.Max(faktor, 1.0));

        for (var nb = breit; nb <= grenzeB; nb++)
        {
            for (var nh = hoch; nh <= grenzeH; nh++)
            {
                if (nb == breit && nh == hoch) continue;
                var abstand = Math.Abs(nb * nh - ziel);
                var seitenAbstand = Math.Abs((double)nb / nh - seite);
                if (abstand < bestAbstand
                    || (abstand == bestAbstand && seitenAbstand < bestSeite))
                {
                    bestAbstand = abstand;
                    bestSeite = seitenAbstand;
                    bestBreit = nb;
                    bestHoch = nh;
                }
            }
        }

        return (bestBreit, bestHoch);
    }

    private (double Faktor, HashSet<string> Ausnahmen) EinstellungenLesen()
    {
        var standard = (1.5, new HashSet<string>());

        var ordner = Pfad.GetDirectoryName(typeof(RucksackVergroesserer).Assembly.Location);
        var datei = Pfad.Combine(ordner ?? ".", "rucksaecke.json");
        if (!File.Exists(datei))
        {
            logger.Info("[Groessere Rucksaecke] Keine rucksaecke.json - Faktor 1,5.");
            return standard;
        }

        try
        {
            using var papier = JsonDocument.Parse(File.ReadAllText(datei));
            var wurzel = papier.RootElement;

            var faktor = wurzel.TryGetProperty("faktor", out var f) && f.TryGetDouble(out var wert)
                ? wert
                : 1.5;

            var ausnahmen = new HashSet<string>();
            if (wurzel.TryGetProperty("ausnahmen", out var a)
                && a.ValueKind == JsonValueKind.Array)
            {
                foreach (var eintrag in a.EnumerateArray())
                {
                    var text = eintrag.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) ausnahmen.Add(text);
                }
            }

            return (faktor, ausnahmen);
        }
        catch (Exception ex)
        {
            logger.Error("[Groessere Rucksaecke] rucksaecke.json ließ sich nicht lesen ("
                         + ex.Message + ") - Faktor 1,5.");
            return standard;
        }
    }
}
