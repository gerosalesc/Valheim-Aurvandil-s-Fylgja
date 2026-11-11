# Stands of Valheim

BepInEx mod adding JoJo Stands. Currently: **Star Platinum** (primitive-shape placeholder model).

## Usage
In-game: F5 console → `devcommands` → `stand` equips Star Platinum as your forsaken power; activate it with the power key (same as boss powers). Only one power can be equipped at a time.
The stand floats behind the local player and follows them.

## Build
Requires .NET SDK and Valheim with BepInEx installed.
`dotnet build -p:ValheimDir=/path/to/Valheim`
Copy `bin/Debug/net48/StandsOfValheim.dll` to `Valheim/BepInEx/plugins/`.

## Roadmap
Attacks (ORA barrage), proper model/asset bundle, Za Warudo time stop, multiplayer sync, non-dev activation.
