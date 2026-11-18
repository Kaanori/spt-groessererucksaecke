# GroessereRucksaecke (bigger backpacks)

Enlarges the compartments of all backpacks by a factor (default 1.5) and – since 1.2.0 – keeps the compartments of multi-compartment backpacks from overlapping (client part).

## How this mod was made (AI disclosure)

This mod was **programmed by Claude**, an AI assistant made by Anthropic.
**Kaan was the tester**: Kaan has no programming background, played with the mod
on SPT 4.1.6, reported what went wrong, and Claude fixed it. No human programmer
has reviewed the code. It is open source so anyone can check it — if you find a
bug, please open an [issue](https://github.com/Kaanori/spt-groessererucksaecke/issues).

## Requirements

- SPT 4.1.6 (EFT 0.16.9). This mod has a client part and a server part; the ZIP contains both.

## Installation

1. Download `GroessereRucksaecke-1.2.0.zip` from the [Releases](https://github.com/Kaanori/spt-groessererucksaecke/releases) page.
2. Extract it **into your SPT game folder** (the folder that contains `EscapeFromTarkov.exe`).
   The ZIP already has the right layout; the mod ends up in `BepInEx/plugins/` (client part) and `SPT_Runtime/user/mods/GroessereRucksaecke/` (server part).
   On Windows, make sure "Extract All" does not add an extra subfolder.
3. Restart the SPT server and the game.

## Uninstall

Delete the mod's files from `BepInEx/plugins/` (client part) and `SPT_Runtime/user/mods/GroessereRucksaecke/` (server part) (settings: `BepInEx/config/` for client mods) and restart the SPT server and the game.

## Settings: `rucksaecke.json`

Takes effect on the next server start.

| Key | Default | Meaning |
|---|---|---|
| `faktor` | `1.5` | How much bigger every compartment gets (1.5 = +50 % cells). |
| `breiteBehalten` | `false` | `true` = compartments keep their original **width** and only grow **downwards**. Use this if wide backpacks push items off the right edge of the inventory screen. |
| `ausnahmen` | `[]` | Backpack template IDs that stay untouched. |

> **Before switching `breiteBehalten` on:** move everything out of the extra columns on the right of your enlarged backpacks (stash included). Items that sit beyond the original width would end up outside the narrower compartment.

## Multi-compartment backpacks (client part)

Backpacks with several compartments (e.g. Mystery Ranch SATL, Dragon Egg MKII) ship their window layout as a fixed template: each compartment takes its size from the game data, but its *position* stays where the template put it. With enlarged compartments the upper ones would grow into the lower ones and overlap.

The client part (`BepInEx/plugins/GroessereRucksaecke/`) fixes this: on first display it remembers the original layout and moves each compartment right/down by exactly as much as the compartments to its left or above have grown. Gaps are kept, overlap is ruled out, and the frame grows along. It only acts when a compartment really is larger than in the original template, so backpacks at vanilla size and single-compartment backpacks are untouched. Nothing runs per frame – it calculates once when you open a backpack.

- F12 setting: **Fix overlapping compartments** (section "Backpacks"), on by default. Takes effect the next time you open a backpack.
- Needs the server part: without enlarged compartments there is nothing to fix.
- **Upgrading from the stand-alone RucksackAnordnung plugin:** delete `BepInEx/plugins/RucksackAnordnung.dll`. If it is still there, this client part detects it, stays off and logs a hint, so nothing is patched twice.

## Building from source

Needs the .NET 10 SDK and an SPT 4.1.6 installation to build against.

```bash
./build.sh             # Linux: build only
./build.sh --install   # build and copy into the game folder
```
```powershell
.\build.ps1            # Windows
.\build.ps1 -Install
```

The game folder is set in `Directory.Build.props` (Windows default `C:/SPT`)
or via the environment variable `SPT_DIR`.

## Support

Bugs and questions: [GitHub issues](https://github.com/Kaanori/spt-groessererucksaecke/issues).

## License

MIT — see [LICENSE](https://github.com/Kaanori/spt-groessererucksaecke/blob/main/LICENSE).
