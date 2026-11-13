# Stands of Valheim

BepInEx mod for Valheim adding JoJo Stands. Currently featuring: **Star Platinum**.

## Features
- **Forsaken Power Integration:** Equipped via `devcommands` and triggered with the `F` key (60s active duration, 60s cooldown), respecting native HUD slots and power cooldowns.
- **Stand Visuals:** Translucent purple muscular humanoid Stand wearing golden shoulder pads, a wrapped red scarf, black combat boots, black gloves, a golden circlet, Painter Curls black hair, and a glowing purple Stand aura.
- **Stand Actions (Hands-Free Requirement):** Stand combat actions (Single Punch, ORA Barrage, and Stand Shield Block) only trigger when the player's hands are free (unarmed or with weapons sheathed via `R`). When weapons or shields are equipped in hand, normal weapon/shield actions take precedence.
- **Primary Attack (Stand Single Punch):** With hands free, press **Left Mouse Button** for a fast single punch from Star Platinum (1 hit, no ORA voice cry, 5 stamina).
- **Secondary Attack (ORA Barrage):** With hands free, press **Middle Mouse Button** to trigger a **5-hit ORA ORA ORA** punch barrage (16 stamina).
  - Features authentic Jotaro / Star Platinum **ORA ORA ORA** voice audio cry.
  - Rapid punching animation using accelerated player unarmed combos.
  - Punches deal 35 blunt damage, heavy knockback, and high stagger.
- **Stand Shield Defense & Parry:** With hands free, holding **Right Mouse Button** (Block) causes Star Platinum to move directly in front of you, crossing its arms in a shield stance with a flared energy aura.
  - **Base Block Armor:** `2x` the player's base armor, scaled with the player's **Blocking** skill (`+50%` at skill 100).
  - **Parry Block Armor:** `6x` the player's base armor when timed within 0.25s of raising block.
  - **Parry Bonus:** `2.5x` with spark effects, parry sound, and attacker stagger.

## Usage
1. Open the in-game console (`F5`).
2. Type:
   ```
   devcommands
   stand
   ```
3. Press **`F`** to summon Star Platinum.
4. Press **Left Mouse Button** (Primary Attack) for a fast Stand single punch without an ORA shout.
5. Press **Middle Mouse Button** (Secondary Attack) to unleash the 5-hit ORA punch barrage with voice audio cry!
6. Hold **Right Mouse Button** to block incoming attacks with Star Platinum. Time it right before an attack lands for a **6x Parry**!

## Build
Requires .NET SDK and Valheim with BepInEx installed.
```bash
dotnet build
```
Copy `bin/Debug/net48/StandsOfValheim.dll` to `Valheim/BepInEx/plugins/`.

## Roadmap
- Custom 3D model loaded via Unity AssetBundle
- Star Platinum: The World (time-stop mechanic)
- Additional JoJo stands (The World, Silver Chariot, Crazy Diamond)
- Multiplayer network sync
- Craftable stand arrows for survival progression
