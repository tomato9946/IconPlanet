# Icon Planet

A code mod for [Spaceflight Simulator](https://store.steampowered.com/app/1718870/) (SFS) that replaces the plain map dots with planet icons built from real planet data (textures, water, rings, terrain, clouds).

**Author:** Luna Tomato  

## Screenshots

![Smooth mode](assets/screenshot_smooth.png)

![High mode](assets/screenshot_high.png)

![Custom solar system](assets/screenshot_custom.png)

---

## 1. About the mod

The base game shows planets on the map as simple dots. Icon Planet draws each planet using:

- Planet surface texture  
- Oceans (water mask + colors)  
- Rings (when the planet has them)  
- Terrain outline  
- Front clouds (depending on mode)  

Works with the **default solar system** and **custom solar systems**.

### Modes (press **P**)

| Mode | Description |
|------|-------------|
| **Smooth** | Clean look across the whole planet; front clouds only on planets without terrain. Default when you start the game. |
| **High** | More detail, including haze and clouds. |
| **Default** | Stock game dots (mod drawing off). |

The last mode is saved to `config.txt`.

---

## 2. How it works & how to use

1. Install the mod and open a world  
2. Open the **map**  
3. Press **P** to cycle: Smooth → High → Default  

Change the key in **Settings → Keybindings**.

If something fails while generating an icon, the mod falls back to the stock dot so the game does not break. Details go to `Player.log` (search for `IconPlanet`).

---

## 3. Config (`config.txt`)

Place next to `IconPlanet.dll` in the mod folder. No rebuild needed after edits.

| Key | Meaning |
|-----|---------|
| **TextureSize** | Icon resolution / sharpness |
| **MinVisiblePx** | Below this on-screen size (px), show the stock dot; above it, fade the icon in |
| **TerrainFadePx** | When zoomed in past this size, fade the icon so the real planet mesh shows (avoids broken close-up). `0` = never fade |
| **Mode** | `smooth` / `high` / `default` |
| **ShowClouds** | Front clouds on/off (Smooth still limits clouds to planets without terrain) |
| **FullDiscTexture** | Try flipping if a planet’s colors look wrong |
| **Enabled** | `false` disables the mod drawing |

---

## 4. Installation

1. Download **IconPlanet.dll** (and `config.txt` if provided) from [Releases](https://github.com/tomato9946/Icon-Planet/releases)  
2. Put them in:  
   `Spaceflight Simulator Game/Mods/IconPlanet/`  
3. Start the game  

Guide: [Code Mod Installation | SFS Modding Guide](https://kojamori.github.io/SFS-Modding-Guide/getting-started/installation/code-mods)

### Build from source

1. Copy DLLs from `Spaceflight Simulator_Data/Managed` into `Dependencies/`  
2. Copy `IconPlanet/env.props.example` → `IconPlanet/env.props`  
3. Open `IconPlanet.sln` → Build **Release**  

---

## Links

- [Releases (download)](https://github.com/tomato9946/Icon-Planet/releases)  
- [YouTube](https://www.youtube.com/channel/UCv2v5wErevqbt4HJkFUhe2A)  
- [SFS Thailand Discord](https://discord.gg/7g6v8Z9k)  

## License

Non-commercial use only — see the [LICENSE](LICENSE) file.

You may use, modify, and share this mod for free community purposes.  
Selling or using it for profit is not allowed without permission.
