# SealHunter

Clears your hunting log for you. Choose the Grand Company log for company seals, or your current
class or job log for XP.

> **Warning:** this automates gameplay, which breaks the FFXIV User Agreement and can get your
> account penalized or banned. Use it at your own risk.

## What you need

- vnavmesh for movement
- Lifestream for teleporting
- BossMod Reborn or RotationSolver Reborn for combat (pick one in settings)
- AutoDuty (optional) to run the dungeons that hold some marks

## How it works

Press Start. SealHunter teleports to each mark's zone, travels to the camp and fights until the log
entry is done. It stops when every open-world mark is cleared.

Some marks live inside dungeons. The main window lists them by dungeon. With AutoDuty installed,
each dungeon gets a Run button; otherwise, clear them yourself.

It pauses for cutscenes and duty pops, recovers if you die, and gets itself unstuck if it stops
moving. The main window shows kills left and an estimated time to finish.

## Install

In `/xlsettings`, under Experimental, add this to Custom Plugin Repositories:

```
https://plugins.xivhub.net/pluginmaster.json
```

Then install SealHunter from the plugin installer.

## Commands

| Command | What it does |
|---|---|
| `/sealhunter` | Open the main window |
| `/sealhunter start` | Start |
| `/sealhunter stop` | Stop right away |
| `/sealhunter dump` | Print your log progress to chat |

## Credits

Hunting log data from [Hunty](https://github.com/Infiziert90/Hunty). Combat preset from
[Questionable](https://github.com/afan0431/Questionable). Navigation ideas from
[ICE](https://github.com/Taira-Yo/Ices-Cosmic-Exploration) and
[ECommons](https://github.com/NightmareXIV/ECommons).

Building from source: see [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## License

AGPL-3.0-or-later. See `LICENSE` and `NOTICE`.
