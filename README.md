# SealHunter

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that automates
the three **Grand Company hunting logs** to farm company seals.

After you press **Start**, it works through every incomplete *open-world* GC hunting-log entry on
its own: reads which marks are still needed, teleports to the nearest aetheryte, walks to each
target with vnavmesh, and clears the required kills using BossMod Reborn's autorotation, then stops
when the open-world portion is done.

> ⚠️ **This automates gameplay, which violates the FFXIV User Agreement and can get your account
> penalized or banned. Use entirely at your own risk.**

## Important limitation: dungeon targets

About a third of GC hunting-log targets (25 of 90) live **inside dungeons** — Halatali, the
Tam-Tara Deepcroft, Wanderer's Palace, etc. (`Doctore`, `Firemane`, `Tonberry`, `Giant Bavarois`…).
SealHunter does **not** run dungeons. It detects those targets, **skips them**, and lists them in
its window (and via `/sealhunter dump`) so you can clear them manually. So the bot completes the
**65 open-world targets**, not the entire log.

## Required plugins

All three are hard dependencies; the **Start** button is disabled until they're present.

| Plugin | Used for |
| --- | --- |
| **vnavmesh** (`vnavmesh`) | pathfinding & movement |
| **Teleporter** (`Teleport` IPC) | teleport to the nearest aetheryte |
| **BossMod Reborn** (`BossMod`) | combat (autorotation via an "Overworld" preset) |

Optional: **RotationSolver Reborn** is supported as an alternative combat backend in code
(`RotationSolverIPC`), but BossMod Reborn is the default.

## Install

Add the plugin repository to Dalamud:

```
/xlsettings → Experimental → Custom Plugin Repositories
https://edgl.dev/share/sealhunter/pluginmaster.json
```

Then install **SealHunter** from the plugin installer.

## Usage

- `/sealhunter` — open the main window (status, start/stop, progress, activity log)
- `/sealhunter start` — begin the autonomous loop
- `/sealhunter stop` — stop immediately (aborts movement and combat)
- `/sealhunter dump` — print current GC log progress to chat (open-world + duty-bound)
- `/sealhunter killone` — debug: run the first open-world target once

The main window shows the live state, the current step ("what it's doing"), per-target progress
bars, the duty-bound targets it can't reach, and a rolling activity log.

### How the loop works

1. Read incomplete entries for your current GC from `MonsterNoteManager`.
2. Pick the next open-world target (grouped by zone to minimise teleports).
3. Teleport to the nearest aetheryte, mount, and navigate to the camp.
4. Find the live mob by `NameId`, target it, dismount, and enable BossMod autorotation.
5. Confirm the kill against live progress (re-tries on stray-aggro or empty camps), loop until the
   entry is done, then move to the next. Stop when all open-world entries are complete.

It pauses for cutscenes / duty pops / manual control, recovers from death, and stops on low gear
durability. All of these are configurable in the settings window.

## Build

Requires the .NET SDK and an extracted Dalamud dev bundle.

```bash
DALAMUD_HOME=~/.cache/dalamud-dev dotnet build SealHunter/SealHunter.csproj -c Release -p:Platform=x64
```

Output: `SealHunter/bin/x64/Release/SealHunter.dll` and a packaged `…/SealHunter/latest.zip`.

## Publish / deploy

`./publish.sh` builds Release and copies `latest.zip`, `SealHunter.dll`, and a regenerated
single-entry `pluginmaster.json` to the share directory (default `~/share/sealhunter`, served at
`https://edgl.dev/share/sealhunter`). **Bump `<Version>` in the csproj before publishing** so
Dalamud detects the update. `publish.sh` is gitignored (its paths are environment-specific).

## Documentation

- `PLAN.md` — the full design/implementation plan.
- `CLAUDE.md` / `AGENTS.md` — notes for AI assistants working on this repo.

## Credits

- Hunting-log target data and game-read patterns adapted from
  [Hunty](https://github.com/Infiziert90/Hunty) (AGPL-3.0).
- BossMod overworld-combat preset recipe adapted from
  [Questionable](https://github.com/afan0431/Questionable) (mirror; upstream by Liza Carvelli).
- Navigation / scheduler idioms from [ICE](https://github.com/Taira-Yo/Ices-Cosmic-Exploration)
  and the [ECommons](https://github.com/NightmareXIV/ECommons) library.

## License

AGPL-3.0-or-later. See `LICENSE` and `NOTICE`.
