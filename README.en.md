# SONORA · LISTEN TO OUR PLANET

**NASA Space Apps Challenge 2026 · The Earth Information Jukebox**
*(Versión en español: [README.md](README.md))*

SONORA is a web-based **Earth Jukebox**. You pick a visualization from NASA's **Earth Information Center (EIC)** and SONORA turns its frames into music **in real time**, so the data can be heard, explored and understood without seeing it.

## Run it
```bash
dotnet run --project SONORA/SONORA/SONORA.csproj --launch-profile http
```
Then open http://localhost:5241. Requires the .NET 10 SDK and a modern browser (Chrome, Edge or Firefox).

## How the challenge is addressed
| Challenge asks for | SONORA |
|---|---|
| Pair **EIC visual frames** with **real-time** sonification | **Jukebox NASA (EIC)** tab: 9 EIC / NASA SVS visualizations. They include global temperature anomalies 1880–2025, El Niño 2026 (sea temperature and ocean color), GEOS aerosols, CO₂, methane by source, the wildfire smoke forecast and ICESat-2. The server caches each SVS video the first time, and the browser extracts 16–64 frames and plays them as music in real time (Tone.js). |
| Let people **select** a visualization and hear its soundscape | One click per card. The player can also be explored step by step with the keyboard. |
| Computer vision: color, brightness, shape, density → pitch, rhythm, instruments | Each frame is split into **4 color families**: the official NASA color bar when it is known, k-means otherwise. They become **melody, rhythm, bass and harmony**. Isolated bright spots ring as **sparkles** (marimba / glockenspiel). The image can be scanned west to east or as a **radar** sweep, like NASA's Caldwell 73 sonification. |
| **Balance data fidelity and musicality** | The mapping rules are tuned by a multi-objective genetic algorithm (**NSGA-II**, HBA.Evolutionary) with two goals: *fidelity* and *musicality*. The user picks their own point on the Pareto front. There are 8 musical scales, all with 15 degrees. |
| **Accessibility** for blind and low-vision users | Screen-reader live descriptions, full keyboard control (role="application"), spoken narration, **haptic feedback** (vibration), rich alt text and scientific context, a downloadable transcript, high contrast, large text and reduced motion. |
| A **playlist of generated audio** as an alternative deliverable | **⏺ Download audio** records one full pass of the current piece and saves it as a `.webm` file. |
| Tell the story / climate urgency | **Climate of Peru**: 1981–2026 yearly trends with the Mann-Kendall test. **Biomes of Peru**: a neural network over NASA GIBS data. **Whole planet**: MODIS land cover 2001–2024. A per-year legend gives values and trends. |

## Interface
- **1. Choose what to listen to** (tabs):
  - 🎵 **Jukebox NASA (EIC)**
  - ⬆ **Your file** (image or video, processed in your browser)
  - 🇵🇪 **Biomes of Peru**
  - 📈 **Climate of Peru**
  - 🌎 **Whole planet**: only the clear box sounds. Shift + drag picks your own area.
- **2. Listen**:
  - The image, with a side legend showing the year or period, each color and its value.
  - Controls: play, tempo, volume, scale, describe, download audio.
  - Tabs: 🎚 **Layers** · 🗺 **Map** (OpenStreetMap context, everything outside the sounding area is greyed out) · 📖 **What you are hearing** · 🤖 **Voice assistant**.
- **3. Accessibility**: always visible.
- **Accessible welcome and spoken guide**: on entry SONORA asks whether you need help to navigate (**Turn on spoken guide** / **Not now**, Enter / Escape). The **Assistant** button is always visible and contains the spoken guide (real sections and only the data each one needs, in short groups, by keyboard, touch or optional voice), simple and detailed explanations, «Learn to listen», «where am I?» and «what can I do here?». The microphone is only used after pressing «Use microphone». SONORA's own voice can be turned off to use a screen reader. Details (in Spanish): [docs/asistente-de-voz.md](docs/asistente-de-voz.md).

## Architecture
| Path | What it is |
|---|---|
| `SONORA/SONORA/` | ASP.NET Core server. Hosts the NSGA-II optimizer (`POST /api/optimize`) and the APIs listed below |
| `SONORA/SONORA/Eic/` | EIC Jukebox: curated SVS catalog, on-demand video cache and same-origin streaming (`/api/eic/*`) |
| `SONORA/SONORA.Client/` | Blazor WebAssembly UI |
| `SONORA/SONORA.Client/wwwroot/js/sonora.js` | Frame analysis (canvas, k-means), music engine (Tone.js), keyboard, haptics, speech, audio recording |
| `SONORA/SONORA/Peru/`, `Mundo/`, `Tendencias/` | Biomes of Peru, whole-planet land cover and Peru climate trends |

### EIC API
| Endpoint | Returns |
|---|---|
| `GET /api/eic/catalogo` | Curated visualizations: title, theme, period, description, alt text, scientific context, SVS link, size, whether it is cached |
| `POST /api/eic/preparar/{id}` | Downloads the SVS video into the cache (`Eic:CacheDir`, default `App_Data/eic`) if it is missing |
| `GET /api/eic/video/{id}` | The cached MP4, with range requests |

## Credits
- **Visualizations:** NASA Earth Information Center and Scientific Visualization Studio (SVS 5669, 5603, 5688, 5687, 5572, 5436, 5554, 5624, 5616), GMAO GEOS, GISS GISTEMP.
- **Data:** NASA GIBS (MODIS, SRTM), NASA POWER and NASA Giovanni.
- **Base map:** © OpenStreetMap contributors.
- **Optimization:** HBA.Evolutionary.
- **Neural networks:** Hba.NeuralNet.
- **Audio:** Tone.js.
