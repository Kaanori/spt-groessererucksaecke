# GroessereRucksaecke (bigger backpacks)

Enlarges the compartments of all backpacks by a factor (default 1.5).

## How this mod was made (AI disclosure)

This mod was **programmed by Claude**, an AI assistant made by Anthropic.
**Kaan was the tester**: Kaan has no programming background, played with the mod
on SPT 4.1.6, reported what went wrong, and Claude fixed it. No human programmer
has reviewed the code. It is open source so anyone can check it — if you find a
bug, please open an [issue](https://github.com/Kaanori/spt-groessererucksaecke/issues).

## Requirements

- SPT 4.1.6 (server).

## Installation

1. Download `GroessereRucksaecke-1.1.0.zip` from the [Releases](https://github.com/Kaanori/spt-groessererucksaecke/releases) page.
2. Extract it **into your SPT game folder** (the folder that contains `EscapeFromTarkov.exe`).
   The ZIP already has the right layout; the mod ends up in `SPT_Runtime/user/mods/GroessereRucksaecke/`.
   On Windows, make sure "Extract All" does not add an extra subfolder.
3. Restart the SPT server.

## Uninstall

Delete the mod's files from `SPT_Runtime/user/mods/GroessereRucksaecke/` (settings: `BepInEx/config/` for client mods) and restart the SPT server.

## Settings: `rucksaecke.json`

Takes effect on the next server start.

| Key | Default | Meaning |
|---|---|---|
| `faktor` | `1.5` | How much bigger every compartment gets (1.5 = +50 % cells). |
| `breiteBehalten` | `false` | `true` = compartments keep their original **width** and only grow **downwards**. Use this if wide backpacks push items off the right edge of the inventory screen. |
| `ausnahmen` | `[]` | Backpack template IDs that stay untouched. |

> **Before switching `breiteBehalten` on:** move everything out of the extra columns on the right of your enlarged backpacks (stash included). Items that sit beyond the original width would end up outside the narrower compartment.

For backpacks with several compartments, RucksackAnordnung (backpack layout) makes sure the enlarged compartments do not overlap.

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
