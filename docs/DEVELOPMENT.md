# Development

## Build

Requires the .NET SDK and an extracted Dalamud dev bundle. Shared helpers come from
[XivHubPluginKit](https://github.com/XivHub/XivHubPluginKit), linked by source: clone it next to this
repo, so `../XivHubPluginKit` exists.

```bash
DALAMUD_HOME=~/.cache/dalamud-dev DOTNET_ROOT=~/.dotnet \
  dotnet build SealHunter/SealHunter.csproj -c Release -p:Platform=x64
```

The build writes `SealHunter.dll` and a packaged `latest.zip` under `SealHunter/bin/x64/Release/`.

## Publish

Bump `<Version>` in `SealHunter/SealHunter.csproj` so Dalamud sees an update, then run `publish-plugin` from the repo
root. It builds Release and replaces SealHunter's entry in the XIV Hub `pluginmaster.json`, leaving the
other plugins alone.
