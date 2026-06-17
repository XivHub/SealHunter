# SealHunter

A Dalamud plugin that automates the three Grand Company hunting logs to earn company seals.

After you press Start, it works through every incomplete **open-world** GC hunting-log entry
on its own: teleports to the nearest aetheryte, walks to each target with vnavmesh, and clears
the required marks using BossMod Reborn's autorotation, then stops.

## Important limitation: dungeon targets

About a third of GC hunting-log targets (25 of 90) live **inside dungeons** (Halatali, the
Tam-Tara Deepcroft, Wanderer's Palace, etc.), not in the open world. SealHunter cannot run
dungeons; it detects those targets, **skips them, and lists them** in its window so you can
clear them manually. So the bot drives the open-world portion to completion, not the entire log.

## Required plugins

- **vnavmesh** — pathfinding / movement
- **Lifestream** — aetheryte teleport (Teleporter plugin supported as a fallback)
- **BossMod Reborn** — combat (autorotation via an "Overworld" preset)

## Status

Early development. See the implementation plan for scope and progress.

## Disclaimer

This plugin automates gameplay, which violates the FFXIV User Agreement and can result in
account penalties. Use at your own risk.

## Credits

- Hunting-log target data and game-read patterns adapted from
  [Hunty](https://github.com/Infiziert90/Hunty) (AGPL-3.0).
- BossMod overworld-combat preset recipe adapted from
  [Questionable](https://github.com/afan0431/Questionable).

## License

AGPL-3.0-or-later.
