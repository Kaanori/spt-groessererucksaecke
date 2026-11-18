# Changelog

## 1.2.0 — 2026-11-18

- New client part: multi-compartment backpacks (Mystery Ranch SATL, Dragon Egg MKII, …) no longer show overlapping compartments. These backpacks ship their window layout with fixed compartment positions, so an enlarged upper compartment used to grow into the ones below it. The client plugin now pushes every compartment right/down by exactly as much as the compartments before it have grown, and the frame grows along. It only acts when a compartment really is larger than in the original layout; vanilla-sized and single-compartment backpacks are left alone.
- New setting in F12: "Fix overlapping compartments" (Backpacks), on by default.
- Replaces the stand-alone RucksackAnordnung plugin. If `RucksackAnordnung.dll` is still installed, the new client part stays off and says so in the log (so nothing is patched twice) – delete the old DLL.
- The server DLL now reports its real version (it said 1.0.0 in its file properties before).
- The ZIP now contains both parts: `BepInEx/plugins/GroessereRucksaecke/` (client) and `SPT_Runtime/user/mods/GroessereRucksaecke/` (server). The ZIP carries the default `rucksaecke.json` – back up your own before extracting over an old install.

## 1.1.0 — 2026-11-08

- New setting `breiteBehalten` (keep width): compartments keep their original width and only grow downwards, so wide backpacks no longer push items off the right edge of the inventory. Off by default – read the note in the README before switching it on.

## 1.0.0 — 2026-11-03

- First release as a standalone repository (SPT 4.1.6).
