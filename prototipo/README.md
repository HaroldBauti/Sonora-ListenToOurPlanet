# Earth Jukebox · Aerosoles GEOS (NASA Space Apps 2026)

Sonificación musical de la visualización **GEOS Aerosols** de NASA SVS (ID 5669, 19-ago-2025 20:00 UTC).
Cada tipo de aerosol es una capa musical; los parámetros del mapeo se optimizan con **NSGA-II (HBA.Evolutionary)**.

## Estructura
| Ruta | Qué es |
|---|---|
| `index.html` | App web: analiza la imagen, genera la música con Tone.js y muestra el frente de Pareto |
| `server.js` | Servidor local (el navegador no deja leer píxeles de archivos abiertos con `file://`) |
| `data/Aerosols_*.png` | Imagen y barra de colores originales de NASA SVS |
| `data/features.json` | Intensidad de cada aerosol en 32 columnas (exportada por la web) |
| `data/pareto.json` | Frente de Pareto generado por el optimizador |
| `optimizer/` | Consola .NET 10 con HBA.Evolutionary |

## Uso
```bash
node server.js            # http://localhost:5173  (PORT=5174 node server.js para otro puerto)
```

Para reoptimizar (por ejemplo, si cambias la imagen o la clasificación):
1. En la web, pulsa **Exportar características** y guarda `features.json` en `data/`.
2. `cd optimizer && dotnet run -c Release [generaciones] [semilla]` → escribe `data/pareto.json`.
3. Recarga la web y elige una solución con el deslizador **más musical ↔ más científico**.

## Mapeo
- **Sulfatos → melodía** (pentatónica de do menor), **Polvo → percusión**, **Humo → bajo**, **Sal marina → pad armónico**.
- **Genes optimizados (10):** suavizado de melodía y ritmo, umbral de sulfatos, curva de tono γ, nota más grave, rango, 3 umbrales de polvo, umbral de humo.
- **Objetivo 1, fidelidad:** correlación de Pearson entre cada capa de datos y su parámetro sonoro (promedio de melodía, ritmo y bajo).
- **Objetivo 2, musicalidad:** saltos melódicos pequeños, variedad de notas, ~70 % de pasos con nota, ritmo y bajo estables.

El motor musical existe dos veces: en C# (`Engine` en `optimizer/Program.cs`) y en JS (`ema`/`compile` en `index.html`).
**Deben ser idénticos.** La web comprueba al cargar que ambos producen la misma secuencia para cada solución.
