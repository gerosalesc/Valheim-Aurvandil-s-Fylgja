# Installation & Usage Guide (Windows)

This guide explains how to install **Aurvandil's Fylgja** (`StandsOfValheim.dll`) for Valheim on Windows.

> **Note:** The icon, sacrificial stone textures, and barrage audio are embedded directly inside `StandsOfValheim.dll`, so you only need that single `.dll` file to play.

---

## Step 1: Locate Your Valheim Folder

1. Open **Steam** and go to your **Library**.
2. Right-click **Valheim** → **Manage** → **Browse local files**.
3. Keep the opened folder ready (usually `C:\Program Files (x86)\Steam\steamapps\common\Valheim`).

---

## Step 2: Install BepInEx (If Not Already Installed)

Valheim requires **BepInEx** to load plugins. Choose one of the two options below:

### Option A: Using a Mod Manager (Easiest — r2modman or Gale)

1. Download and install **r2modman** or **Gale Mod Manager** from Thunderstore.
2. Select **Valheim** and create a profile.
3. Search for and install **`BepInExPack_Valheim`** (by *denikson*).
4. Open your profile's plugins directory via **Settings** → **Browse profile folder**, then open `BepInEx\plugins\` and continue to **Step 3**.
   - *When using a mod manager, always launch the game using the manager's **Start modded** button.*

### Option B: Manual Installation with Steam

1. Download **`BepInExPack_Valheim`** from Thunderstore:  
   https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/ (click **Manual Download**).
2. Extract the downloaded `.zip` archive.
3. Open the extracted `BepInExPack_Valheim` folder and copy **all files and folders inside it** (`BepInEx`, `doorstop_config.ini`, `winhttp.dll`, etc.) directly into your **Valheim** root directory from Step 1.
4. Launch Valheim once to the main menu, then close it so BepInEx initializes `BepInEx\plugins\`.

---

## Step 3: Install `StandsOfValheim.dll`

1. Obtain **`StandsOfValheim.dll`** from the repository's **Releases** page (or build it from source—see [Building from Source](#building-from-source) below).
2. Copy `StandsOfValheim.dll` into your plugins folder:
   - **Manual Steam install:** `...\Valheim\BepInEx\plugins\StandsOfValheim.dll`
   - **Mod manager install:** `<Your Profile Folder>\BepInEx\plugins\StandsOfValheim.dll`

> **Multiplayer Note:** If you are playing co-op on the same world, **both the host/server and all players** must have `StandsOfValheim.dll` installed in their `BepInEx\plugins\` folder.

---

## Step 4: Unlocking & Using Aurvandil's Fylgja

### Unlocking in Survival
1. Travel to the boss ring at the **Start Temple**. A new custom sacrificial monolith with purple-and-gold runes will spawn in the circle.
2. Hang a **Fallen Valkyrie Trophy** (`TrophyFallenValkyrie`) on the stone's hook and interact with it to claim the **Aurvandil's Fylgja** Forsaken Power.

### Quick Testing via Console Command
1. Ensure the Valheim console is enabled in Steam (*Valheim Properties → General → Launch Options → `-console`*).
2. In-game, press **`F5`** to open the console and enter:
   ```text
   devcommands
   fylgja
   ```
   This equips **Aurvandil's Fylgja** as your active Forsaken Power right away.

### Controls
1. Press **`F`** (your Forsaken Power key) to summon **Aurvandil's Fylgja** (**5 minutes** active duration, **10 minutes** cooldown).
2. Keep your **hands free** (no weapon, shield, or tool equipped; press **`R`** to sheathe). While holding weapons or a shield, normal player combat actions take priority.
3. With hands free:
   - **Left Mouse Button (Primary Attack):** Fast single Fylgja punch (`5` stamina).
   - **Middle Mouse Button (Secondary Attack):** Rapid **12-hit barrage** (`16` stamina) with impact shockwaves and flurry visuals.
   - **Hold Right Mouse Button (Block):** Summons your Fylgja in front of you as an astral shield. Block right before impact (`< 0.25s`) for a **6x Parry** that staggers the attacker!

---

## Building from Source (Optional)

If you want to compile `StandsOfValheim.dll` yourself on Windows (requires **.NET SDK** and **Valheim + BepInEx** installed):

```powershell
dotnet build -c Release -p:ValheimDir="C:\Program Files (x86)\Steam\steamapps\common\Valheim"
```

Then copy `bin\Release\net48\StandsOfValheim.dll` into `Valheim\BepInEx\plugins\`.
