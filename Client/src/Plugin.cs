// Groessere Rucksaecke - Client-Teil (Fachanordnung)
//
// Rucksaecke mit MEHREREN Faechern (z. B. Dragon Egg MKII, Mystery Ranch SATL)
// bringen ihr Fensterlayout als fertige Vorlage im Item-Bundle mit:
// TemplatedGridsView mit fest platzierten GridViews (_presetGridViews). Beim
// Anzeigen setzt jedes GridView seine Groesse aus der Fachgroesse
// (GridView.OnGridResized -> sizeDelta = Zellen x 63 px), die POSITIONEN
// bleiben aber die aus der Vorlage. Vergroessert der Server-Teil die Faecher,
// wachsen sie deshalb uebereinander.
//
// Dieses Plugin merkt sich beim ersten Anzeigen die Original-Anordnung der
// Vorlage und schiebt nach dem Anzeigen jedes Fach um genau so viel nach
// rechts/unten, wie die Faecher links davon bzw. darueber gewachsen sind.
// Abstaende bleiben erhalten. Danach waechst der Rahmen der Vorlage mit.
//
// Es greift NUR, wenn mindestens ein Fach tatsaechlich groesser ist als in der
// Vorlage. Rucksaecke in Originalgroesse und Rucksaecke mit nur einem Fach
// (GeneratedGridsView) bleiben unberuehrt. Es gibt keinen Update()-Aufruf:
// gerechnet wird nur beim Oeffnen eines Rucksacks.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace GroessereRucksaecke.Client
{
	// Weicher Verweis auf das alte Einzel-Plugin, damit es vor uns geladen wird
	// und wir es sicher erkennen (Doppelpatch vermeiden).
	[BepInPlugin(Guid, "GroessereRucksaecke Client", "1.2.0")]
	[BepInDependency(AltGuid, BepInDependency.DependencyFlags.SoftDependency)]
	public class Plugin : BaseUnityPlugin
	{
		internal const string Guid = "com.kaan.groessererucksaecke";
		internal const string AltGuid = "com.kaan.rucksackanordnung";

		internal static ManualLogSource Log;
		internal static ConfigEntry<bool> Aktiv;

		private void Awake()
		{
			Log = Logger;
			Aktiv = Config.Bind("Backpacks", "Fix overlapping compartments", true,
				"Moves the compartments of multi-compartment backpacks apart when GroessereRucksaecke has enlarged them, so they no longer overlap. Applies the next time a backpack is opened.");

			try
			{
				if (AltesPluginAktiv(out string grund))
				{
					Log.LogInfo("Old standalone plugin RucksackAnordnung is installed (" + grund + ") - staying off to avoid patching twice. Delete RucksackAnordnung.dll to use this one.");
					return;
				}
				new Harmony(Guid).PatchAll(typeof(ShowPatch));
				Log.LogInfo("Compartment layout fix active");
			}
			catch (Exception e)
			{
				Log.LogWarning("Could not start: " + e.Message);
			}
		}

		private static bool AltesPluginAktiv(out string grund)
		{
			grund = null;
			if (Chainloader.PluginInfos.ContainsKey(AltGuid))
			{
				grund = "plugin " + AltGuid;
				return true;
			}
			MethodBase ziel = ShowPatch.Ziel();
			Patches info = ziel != null ? Harmony.GetPatchInfo(ziel) : null;
			if (info != null && info.Owners.Contains(AltGuid))
			{
				grund = "Harmony patch " + AltGuid;
				return true;
			}
			return false;
		}
	}

	[HarmonyPatch]
	internal static class ShowPatch
	{
		private static MethodBase TargetMethod() { return Ziel(); }

		internal static MethodBase Ziel()
		{
			return AccessTools.Method(typeof(TemplatedGridsView), nameof(TemplatedGridsView.Show), new[]
			{
				typeof(CompoundItem), typeof(ItemContext), typeof(ItemController),
				typeof(FilterPanel), typeof(ItemUiContext), typeof(bool)
			});
		}

		private static void Prefix(TemplatedGridsView __instance)
		{
			if (!Plugin.Aktiv.Value || __instance == null) return;
			try { Anordnung.Merken(__instance); }
			catch (Exception e) { Anordnung.Warnen("Merken", e); }
		}

		private static void Postfix(TemplatedGridsView __instance)
		{
			if (!Plugin.Aktiv.Value || __instance == null) return;
			try { Anordnung.Ordnen(__instance); }
			catch (Exception e) { Anordnung.Warnen("Ordnen", e); }
		}
	}

	// Haengt an der Vorlage und haelt deren Original-Anordnung fest.
	// Koordinaten: relativ zur linken oberen Ecke des Vorlagen-Rahmens,
	// x nach rechts, y nach UNTEN (in lokalen Einheiten des Rahmens).
	internal sealed class Anordnung : MonoBehaviour
	{
		private const float Toleranz = 2f;

		private static readonly HashSet<string> Gemeldet = new HashSet<string>();

		private RectTransform[] _faecher;
		private Rect[] _original;
		private Vector2 _rahmenOriginal;
		// Beschriftungs-/Filterknoepfe je Fach (_gridButtons/_slotButtons der
		// Vorlage). Sie sitzen nicht im Fach selbst, sondern daneben im Rahmen,
		// und muessen deshalb mit ihrem Fach mitruecken.
		private RectTransform[] _knopf;
		private Rect[] _knopfOriginal;
		private bool _ueberspringen;
		private bool _bereit;      // Merken ist vollstaendig durchgelaufen
		private bool _veraendert;  // wir haben an dieser Vorlage schon etwas verschoben
		private static bool _warnungGezeigt;

		// Einmal je Spielstart melden, nicht bei jedem Oeffnen eines Rucksacks.
		internal static void Warnen(string wo, Exception e)
		{
			if (_warnungGezeigt) return;
			_warnungGezeigt = true;
			Plugin.Log.LogWarning(wo + ": " + e.Message + " (further messages suppressed)");
		}

		internal static void Merken(TemplatedGridsView view)
		{
			if (view.GetComponent<Anordnung>() != null)
			{
				return;
			}
			Anordnung a = view.gameObject.AddComponent<Anordnung>();
			RectTransform rahmen = (RectTransform)view.transform;

			IEnumerable<Component> grids = (view._presetGridViews ?? Array.Empty<GridView>()).Where(g => g != null).Cast<Component>();
			IEnumerable<Component> slots = (view._presetSlotViews ?? Array.Empty<SlotView>()).Where(s => s != null).Cast<Component>();
			a._faecher = grids.Concat(slots).Select(c => (RectTransform)c.transform).ToArray();

			// Mit weniger als zwei Faechern gibt es nichts anzuordnen. Liegen die
			// Faecher in einer LayoutGroup, ordnet Unity sie schon selbst.
			a._ueberspringen = a._faecher.Length < 2 || a._faecher.Any(f =>
			{
				LayoutGroup lg = f.parent != null ? f.parent.GetComponent<LayoutGroup>() : null;
				return lg != null && lg.enabled;
			});
			if (a._ueberspringen)
			{
				a._bereit = true;
				return;
			}
			a._original = a._faecher.Select(f => Messen(rahmen, f)).ToArray();
			var knoepfe = new Dictionary<Transform, RectTransform>();
			if (view._gridButtons != null)
			{
				foreach (KeyValuePair<GridView, UI.LocalizedFilterButton> kv in view._gridButtons)
				{
					if (kv.Key != null && kv.Value != null) knoepfe[kv.Key.transform] = (RectTransform)kv.Value.transform;
				}
			}
			if (view._slotButtons != null)
			{
				foreach (KeyValuePair<SlotView, UI.LocalizedFilterButton> kv in view._slotButtons)
				{
					if (kv.Key != null && kv.Value != null) knoepfe[kv.Key.transform] = (RectTransform)kv.Value.transform;
				}
			}
			a._knopf = new RectTransform[a._faecher.Length];
			a._knopfOriginal = new Rect[a._faecher.Length];
			for (int i = 0; i < a._faecher.Length; i++)
			{
				// Ein Knopf, der im Fach selbst haengt, wandert ohnehin mit.
				if (knoepfe.TryGetValue(a._faecher[i], out RectTransform k) && !k.IsChildOf(a._faecher[i]))
				{
					a._knopf[i] = k;
					a._knopfOriginal[i] = Messen(rahmen, k);
				}
			}
			a._rahmenOriginal = rahmen.rect.size;
			a._bereit = true;
		}

		internal static void Ordnen(TemplatedGridsView view)
		{
			Anordnung a = view.GetComponent<Anordnung>();
			if (a == null || !a._bereit || a._ueberspringen)
			{
				return;
			}
			a.Anwenden((RectTransform)view.transform, view.gameObject.name);
		}

		private void Anwenden(RectTransform rahmen, string name)
		{
			int n = _faecher.Length;
			Vector2[] groesse = new Vector2[n];
			for (int i = 0; i < n; i++)
			{
				groesse[i] = _faecher[i] != null ? Messen(rahmen, _faecher[i]).size : _original[i].size;
			}

			// Nur eingreifen, wenn ein Fach wirklich groesser ist als in der Vorlage
			// (Originalgroesse = nichts tun). Wurde diese Vorlage schon veraendert,
			// laeuft die Rechnung weiter, damit alles wieder an seinen Platz kommt.
			bool gewachsen = false;
			for (int i = 0; i < n && !gewachsen; i++)
			{
				gewachsen = groesse[i].x - _original[i].width > Toleranz || groesse[i].y - _original[i].height > Toleranz;
			}
			if (!gewachsen && !_veraendert)
			{
				return;
			}

			float[] x = Verteilen(n, i => _original[i].xMin, i => _original[i].xMax, i => groesse[i].x - _original[i].width);
			float[] y = Verteilen(n, i => _original[i].yMin, i => _original[i].yMax, i => groesse[i].y - _original[i].height);

			// Rahmen um so viel wachsen lassen, wie die Faecher ueber ihren
			// alten Umriss hinausragen.
			float altRechts = _original.Max(r => r.xMax);
			float altUnten = _original.Max(r => r.yMax);
			float neuRechts = Enumerable.Range(0, n).Max(i => x[i] + groesse[i].x);
			float neuUnten = Enumerable.Range(0, n).Max(i => y[i] + groesse[i].y);
			Vector2 ziel = _rahmenOriginal + new Vector2(Mathf.Max(0f, neuRechts - altRechts), Mathf.Max(0f, neuUnten - altUnten));
			RahmenSetzen(rahmen, ziel);

			bool verschoben = false;
			for (int i = 0; i < n; i++)
			{
				RectTransform f = _faecher[i];
				if (f == null)
				{
					continue;
				}
				Rect jetzt = Messen(rahmen, f);
				float dx = x[i] - jetzt.xMin;
				float dy = y[i] - jetzt.yMin;
				if (Mathf.Abs(dx) > 0.01f || Mathf.Abs(dy) > 0.01f)
				{
					f.position += rahmen.TransformVector(new Vector3(dx, -dy, 0f));
					verschoben = true;
				}
				// Knopf um dieselbe Verschiebung wie das Fach, gerechnet von seiner
				// Original-Lage aus (wiederholbar, kein Aufsummieren).
				RectTransform k = _knopf[i];
				if (k != null)
				{
					Rect kJetzt = Messen(rahmen, k);
					float kx = _knopfOriginal[i].xMin + (x[i] - _original[i].xMin) - kJetzt.xMin;
					float ky = _knopfOriginal[i].yMin + (y[i] - _original[i].yMin) - kJetzt.yMin;
					if (Mathf.Abs(kx) > 0.01f || Mathf.Abs(ky) > 0.01f)
					{
						k.position += rahmen.TransformVector(new Vector3(kx, -ky, 0f));
					}
				}
			}

			_veraendert = gewachsen;
			LayoutRebuilder.MarkLayoutForRebuild(rahmen);

			if (verschoben && Gemeldet.Add(name))
			{
				Plugin.Log.LogInfo($"{name}: {n} Faecher neu angeordnet, Rahmen {_rahmenOriginal.x:0}x{_rahmenOriginal.y:0} -> {ziel.x:0}x{ziel.y:0}");
			}
		}

		// Ordnet entlang einer Achse: Jedes Fach rueckt um (Verschiebung +
		// Wachstum) des am weitesten geschobenen Fachs nach, das vollstaendig
		// davor liegt. Was vorher getrennt war, bleibt dadurch getrennt.
		private static float[] Verteilen(int n, Func<int, float> anfang, Func<int, float> ende, Func<int, float> wachstum)
		{
			int[] reihenfolge = Enumerable.Range(0, n).OrderBy(anfang).ToArray();
			float[] schub = new float[n];
			foreach (int i in reihenfolge)
			{
				float s = 0f;
				foreach (int j in reihenfolge)
				{
					if (j != i && ende(j) <= anfang(i) + Toleranz)
					{
						s = Mathf.Max(s, schub[j] + Mathf.Max(0f, wachstum(j)));
					}
				}
				schub[i] = s;
			}
			return Enumerable.Range(0, n).Select(i => anfang(i) + schub[i]).ToArray();
		}

		private static void RahmenSetzen(RectTransform rahmen, Vector2 ziel)
		{
			Vector2 d = ziel - rahmen.rect.size;
			if (Mathf.Abs(d.x) > 0.01f || Mathf.Abs(d.y) > 0.01f)
			{
				// Linke obere Ecke bleibt stehen.
				rahmen.sizeDelta += d;
				rahmen.anchoredPosition += new Vector2(d.x * rahmen.pivot.x, -d.y * (1f - rahmen.pivot.y));
			}
			// Falls ein Layout darueber die Groesse bestimmt, braucht es diese
			// Mindestgroesse, sonst schrumpft es den Rahmen wieder zurueck.
			LayoutElement le = rahmen.GetComponent<LayoutElement>() ?? rahmen.gameObject.AddComponent<LayoutElement>();
			le.minWidth = ziel.x;
			le.minHeight = ziel.y;
			if (le.preferredWidth >= 0f) le.preferredWidth = Mathf.Max(le.preferredWidth, ziel.x);
			if (le.preferredHeight >= 0f) le.preferredHeight = Mathf.Max(le.preferredHeight, ziel.y);
		}

		private static readonly Vector3[] Ecken = new Vector3[4];

		// Rechteck eines Fachs relativ zur linken oberen Ecke des Rahmens.
		private static Rect Messen(RectTransform rahmen, RectTransform f)
		{
			f.GetWorldCorners(Ecken);
			Vector3 lu = rahmen.InverseTransformPoint(Ecken[0]); // links unten
			Vector3 ro = rahmen.InverseTransformPoint(Ecken[2]); // rechts oben
			Rect r = rahmen.rect;
			return Rect.MinMaxRect(lu.x - r.xMin, r.yMax - ro.y, ro.x - r.xMin, r.yMax - lu.y);
		}
	}
}
