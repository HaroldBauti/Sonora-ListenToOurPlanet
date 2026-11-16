# BiomasPeru · mapas de calor de biomas y tendencias para SONORA

App de consola que clasifica los biomas de Perú con una red neuronal (**Hba.NeuralNet**), ajusta sus hiperparámetros con un algoritmo evolutivo (**NSGA-II de HBA.Evolutionary**) y genera mapas de calor y animaciones por año que SONORA puede convertir en sonido.

## Estructura
| Proyecto | Qué contiene |
|---|---|
| `entrenamiento/BiomasPeru.Core/` | Librería compartida: `Datos` (descarga y decodificación de NASA GIBS), `Biomas` (características y etiquetas), `Hpo` (cromosoma y NSGA-II), `Mapas` (predicción, tendencias y paletas), `Atlas` (atlas precalculado 2001–2024 y dibujo de cualquier mapa por tipo, año y región) y `Png` |
| `entrenamiento/BiomasPeru/` | Esta consola: entrena, evalúa y escribe `salida/` |
| `SONORA/SONORA/Peru/` | Usa el Core y `salida/modelo_biomas.json` para servir los mapas al instante en `/api/biomas/*` (ver el README principal) |

## Ejecutar
```bash
cd entrenamiento/BiomasPeru
dotnet run -c Release -- --minutos 12     # presupuesto de la búsqueda evolutiva
dotnet run -c Release -- --benchmark      # solo mide el coste de una iteración
```
- La primera ejecución descarga unos 600 PNG pequeños de NASA GIBS en `datos/` (cerca de 1 minuto). No hace falta cuenta de Earthdata.
- Los resultados se guardan en `salida/`.

## Datos (NASA GIBS, 2001–2024, rejilla de 0,05° ≈ 5,5 km)
| Capa GIBS | Uso |
|---|---|
| `MODIS_Terra_L3_NDVI_Monthly` | 12 valores mensuales de NDVI por año |
| `MODIS_Terra_L3_Land_Surface_Temp_Monthly_Day` | 12 temperaturas de superficie mensuales (°C) por año |
| `MODIS_Combined_L3_IGBP_Land_Cover_Type_Annual` | Etiquetas (clase IGBP modal) |
| `SRTM_Color_Index` | Altitud y relieve |

Los colores de cada píxel se convierten a valores físicos con las paletas oficiales de GIBS. Cada bin tiene un color único, así que la conversión es exacta.

## Biomas (etiquetas = IGBP + altitud + vertiente)
| # | Bioma | Regla |
|---|---|---|
| 0 | Desierto costero | Vertiente del Pacífico, < 2000 m, suelo desnudo, matorral o pastizal |
| 1 | Bosque seco ecuatorial | Vertiente del Pacífico, al norte de 8° S, < 1500 m, bosque, matorral o sabana |
| 2 | Valles y matorral andino | 1500–3500 m, vegetación abierta |
| 3 | Puna y páramo | ≥ 3500 m, vegetación abierta |
| 4 | Nival y glaciar | Nieve o hielo, o suelo desnudo por encima de 4800 m |
| 5 | Yungas (selva alta) | Vertiente amazónica, 800–3500 m, bosque |
| 6 | Selva baja amazónica | Vertiente amazónica, < 800 m, bosque o humedal |
| 7 | Agricultura y urbano | Cultivos o zona urbana |

## Cómo funciona
1. **Características:** 29 por celda y año: 12 de NDVI, 12 de temperatura, altitud, relieve, latitud, vertiente y NDVI de las celdas vecinas.
2. **Partición espacial:** los datos se dividen por bloques de 0,5° (70/15/15). Así la autocorrelación espacial no infla la puntuación de validación.
3. **NSGA-II:**
   - **Cromosoma (7 genes):** tasa de aprendizaje, número de capas, neuronas, activación, tamaño de lote, L2 y optimizador (SGD, Momentum o Adam).
   - **Objetivos:** maximizar la exactitud equilibrada en validación y minimizar el número de parámetros.
4. **Aceleración:**
   - Evaluaciones baratas: 30 % de los datos, como máximo 30 iteraciones y parada temprana.
   - Evaluación en paralelo.
   - Caché de configuraciones ya evaluadas.
   - Reutilización de élites.
   - Límite de tiempo.
5. **Entrenamiento final:** la mejor configuración se reentrena con todos los datos y se compara con una red base elegida a mano (`[32] ReLU Adam lr=0.01`).
6. **Tendencias:** Theil-Sen y Mann-Kendall por celda, sobre el NDVI anual y sobre la probabilidad de bosque que predice la red.

## Salidas (`salida/`)
| Archivo | Qué es |
|---|---|
| `biomas_2001.png`, `biomas_2024.png` | Bioma más probable según la red |
| `biomas_etiquetas_igbp.png` | Etiquetas de entrenamiento (gris = ambiguo) |
| `confianza_2024.png` | Confianza de la red (los valores bajos marcan zonas de transición) |
| `tendencia_bosque.png` | Tendencia de la probabilidad de bosque (marrón = pérdida, verde = ganancia) |
| `tendencia_ndvi.png` | Solo la tendencia de NDVI significativa (blanco = sin tendencia significativa) |
| `frames/bosque_AAAA.png` | Probabilidad de bosque por año, para el modo video de SONORA |
| `frames/ndvi_anomalia_AAAA.png` | Anomalía de NDVI por año (El Niño, sequías, deforestación) |
| `cbar_tendencia.png`, `cbar_probabilidad.png` | Barras de colores |
| `modelo_biomas.json` | Red entrenada (`Perceptron.Load`) |
| `resumen.json` | Métricas, frente de Pareto, tiempos y casos de estudio |

**Paletas de los mapas:** las tendencias usan una escala divergente marrón–blanco–verde (BrBG) y las probabilidades una escala tipo viridis.

**Colores fijos:**
- El agua es azul (`#2B6CB0`).
- Las celdas de Perú sin datos son grises (`#3A3F4A`).
- Todo lo que está fuera de Perú es casi negro (`#0B0E14`).

## Uso desde SONORA
- El servidor de SONORA **no entrena**. Al arrancar carga `datos/` (configurable con `Biomas:DataDir`) y el modelo `salida/modelo_biomas.json`, que se copia a `bin/Biomas/` al compilar. Con eso precalcula el atlas en unos 5 s.
- Si los datos no existen, el servidor los descarga de NASA GIBS (unos 38 MB).
- Después de reentrenar aquí, vuelve a compilar SONORA para que use el modelo nuevo.
- `Atlas.Render` tiene dos estilos:
  - `vista`: las paletas de esta consola.
  - `sonora`: 4 colores exactos, uno por capa musical, con el fondo, el agua y las celdas sin datos en tonos de luminancia < 18, para que SONORA los trate como silencio.

## Limitaciones
- **Etiquetas débiles:** MODIS IGBP es un producto de 500 m con sus propios errores, y las reglas de altitud y vertiente simplifican las ecorregiones reales.
- **Resolución:** a 5,5 km se suavizan los cambios pequeños, como minas puntuales o un glaciar concreto.
- **Sin precipitación:** el modelo de "NDVI esperado según el clima" del plan aún no está implementado. Necesitaría GPM IMERG.
