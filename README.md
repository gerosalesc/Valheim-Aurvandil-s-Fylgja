# Valheim Fylgja

A BepInEx mod for Valheim adding summonable **Fylgja** astral guardians as Forsaken Powers. Currently featuring: **Aurvandil's Fylgja**.

For step-by-step Windows setup instructions, see the [Installation & Usage Guide](INSTALL.md).

## Features
- **Sacrificial Stone Progression:** Spawns a custom rune-carved monolith at the **Start Temple** circle. Offer a **Fallen Valkyrie Trophy** to awaken **Aurvandil's Fylgja**.
- **Forsaken Power Integration:** Summoned via the **`F`** power key (**5 minutes** active duration, **10 minutes** cooldown), respecting native HUD slots and power cooldowns.
- **Fylgja Visuals:** Spectral purple humanoid guardian wearing dark forged-gold shoulder pads, waist belt, knee pads, and circlet; crimson neck scarf; matte light-grey loincloth; black combat boots and gloves; windswept black mane; and a glowing purple aura.
- **Hands-Free Requirement:** Fylgja combat actions (Single Punch, Astral Barrage, and Fylgja Shield Defense) only trigger when the player's hands are free (unarmed or weapons sheathed via **`R`**). When weapons or shields are equipped, normal combat actions take precedence.
- **Primary Attack (Fylgja Single Punch):** With hands free, press **Left Mouse Button** for a fast punch (`5` stamina, `35` blunt damage, knockback, and high stagger).
- **Secondary Attack (Astral Barrage):** With hands free, press **Middle Mouse Button** to unleash a rapid **12-hit punch barrage** (`16` stamina).
  - Features rapid combo animations, spectral speed-flurry afterimages, and shockwave impact rings.
  - Deals `20` blunt damage per strike with a `45` blunt damage heavy knockback finisher (`2.5x`–`3.0x` stagger multiplier).
  - Includes custom barrage audio.
- **Fylgja Shield Defense & Parry:** With hands free, holding **Right Mouse Button** (Block) moves Aurvandil's Fylgja directly in front of you in a guard stance with a flared protective aura.
  - **Base Block Armor:** `2x` the player's base armor, scaled with the **Blocking** skill (`+50%` at skill `100`).
  - **Parry Block Armor:** `6x` the player's base armor when timed within `0.25s` of raising guard, triggering parry sparks and staggering the attacker.

## Quick Usage
1. Offer a **Fallen Valkyrie Trophy** at the **Aurvandil's Fylgja** sacrificial stone near the Start Temple (or open the `F5` console with `-console`, run `devcommands`, and type `fylgja`).
2. Press **`F`** to summon **Aurvandil's Fylgja**.
3. Sheathe your weapons (`R`) so your hands are free:
   - **Left Mouse Button:** Fast single punch.
   - **Middle Mouse Button:** 12-hit astral barrage.
   - **Hold Right Mouse Button:** Guard or time a **6x Parry** with Aurvandil's Fylgja.

## Installation & Build
Download the prebuilt plugin directly from [`Releases/StandsOfValheim.dll`](Releases/StandsOfValheim.dll) and place it in `Valheim/BepInEx/plugins/`. See [INSTALL.md](INSTALL.md) for full Windows installation steps.

To compile from source (requires .NET SDK and Valheim with BepInEx installed):
```bash
dotnet build -p:ValheimDir="/path/to/Valheim"
```
Copy `bin/Debug/net48/StandsOfValheim.dll` to `Valheim/BepInEx/plugins/`. All icons, stone textures, and audio clips are embedded inside the DLL.

## Roadmap
- Additional Fylgjur / boss-linked astral guardians
- Custom 3D models loaded via Unity AssetBundle
- Multiplayer network sync
- Expanded survival crafting & rune rituals
