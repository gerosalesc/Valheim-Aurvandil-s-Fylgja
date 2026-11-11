# Stands of Valheim

BepInEx mod for Valheim adding JoJo Stands. Currently featuring: **Star Platinum**.

## Features
- **Forsaken Power Integration:** Equipped via `devcommands` and triggered with the `F` key, respecting the native boss power cooldown, duration, and HUD slot (only one power active at a time).
- **Stand Visuals:** Purple humanoid stand with gold trim, dark hair, red eyes, and a dynamic glowing purple Stand aura.
- **Stand Shield Defense & Parry:** Holding **Right Mouse Button** (Block) causes Star Platinum to move directly in front of you, crossing its arms in a shield stance with a flared energy aura.
  - **Base Block Armor:** `2x` the player's base armor, scaled with the player's **Blocking** skill (`+50%` at skill 100).
  - **Parry Block Armor:** `6x` the player's base armor when timed within 0.25s of raising block.
  - **Parry Bonus:** `2.5x` with spark effects, parry sound, and attacker stagger.
- **Secondary Attack (ORA Barrage):** Press **Middle Mouse Button** to trigger an **ORA ORA ORA** punch barrage. Punches deal 35 blunt damage, heavy knockback, and high stagger with semi-transparent grey "ORA!" combat text.

## Usage
1. Open the in-game console (`F5`).
2. Type:
   ```
   devcommands
   stand
   ```
3. Press **`F`** to summon Star Platinum.
4. Hold **Right Mouse Button** to block incoming attacks with Star Platinum. Time it right before an attack lands for a **6x Parry**!
5. Press **Middle Mouse Button** (Secondary Attack) to unleash the ORA punch barrage.

## Build
Requires .NET SDK and Valheim with BepInEx installed.
```bash
dotnet build
```
Copy `bin/Debug/net48/StandsOfValheim.dll` to `Valheim/BepInEx/plugins/`.

## Roadmap
- Custom 3D model loaded via Unity AssetBundle
- Audio sound effects for stand cries and punches
- Star Platinum: The World (time-stop mechanic)
- Additional JoJo stands (The World, Silver Chariot, Crazy Diamond)
- Multiplayer network sync
- Craftable stand arrows for survival progression
