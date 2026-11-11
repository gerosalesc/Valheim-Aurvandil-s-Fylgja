# Stands of Valheim

BepInEx mod adding JoJo Stands. Currently: **Star Platinum** (primitive-shape placeholder model).

## Usage
In-game: F5 console → `devcommands` → `stand` (or `stand summon` / `stand dismiss`).
The stand floats behind the local player and follows them.

## Build
Requires .NET SDK and Valheim with BepInEx installed.
`dotnet build -p:ValheimDir=/path/to/Valheim`
Copy `bin/Debug/net48/StandsOfValheim.dll` to `Valheim/BepInEx/plugins/`.

## Roadmap
Attacks (ORA barrage), proper model/asset bundle, Za Warudo time stop, multiplayer sync, non-dev activation.
