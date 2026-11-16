# TendenciasPeru · el clima de Perú año por año (datos y red de TrendDetective)

`tendencias_peru.json` trae las series climáticas de **11 regiones de Perú**, su tendencia y lo que sugiere una red neuronal. SONORA lo convierte en música año por año. **No descarga nada**: los datos ya fueron descargados y procesados por el proyecto **TrendDetective** (Earth System Trend Detective).

## Qué contiene

| Campo | Qué es |
|---|---|
| `fuentes` | **NASA POWER** (MERRA-2 y CERES, 1981–2025, 14 variables) y **NASA Giovanni** (GPM, GLDAS, AIRS, OMI, 2000–2025, 12 variables). 2026 se agrega como año **provisional**, con solo los meses ya publicados. |
| `variables` | Código, nombre simple, qué mide y cómo leer la unidad. |
| `regiones[].series` | Por región y variable: **media anual sin estacionalidad** de cada año, tendencia (Mann-Kendall estacional, α = 0,05), pendiente de Sen por década con su IC 95 % y `red`: la probabilidad que da la red neuronal de que suba o baje. |

**Regiones:** Lima, Piura y Sechura, Ica y Nazca, Mar de Grau (corriente de Humboldt), Cajamarca, Cordillera Blanca, Cusco, Puno y lago Titicaca, Loreto, Ucayali y Madre de Dios.

Cada región es una caja de 2° × 2° (unos 220 km de lado), y el valor de cada año es el promedio de toda la caja.

## La red neuronal

- **Arquitectura:** MLP de **Hba.NeuralNet**.
- **Hiperparámetros:** los buscó **NSGA-II** de HBA.Evolutionary, con los objetivos de máximo F1 y mínimo tamaño.
- **Datos de entrenamiento:** **199 regiones de todo el mundo**. Con solo Perú habría unos 120 ejemplos, demasiado pocos para aprender.
- **Entradas:** latitud, longitud, altura, clima medio, bioma y variable.
- **Salida:** probabilidad de que la serie suba, baje o no tenga tendencia.
- **Redes:** hay una por fuente, `sonora-giovanni` y `sonora-power`. Sus métricas en regiones que nunca vio están en `fuentes[].red`.
- **Uso en SONORA:** las variables de cada región vienen **ordenadas por lo que sugiere la red**. Las 4 primeras se proponen como melodía, ritmo, bajo y armonía.
- **Regiones nunca vistas:** algunas regiones de Perú quedaron en el conjunto de prueba (`regionNoVista: true`), así que la red nunca vio sus datos.

## Cómo suena en SONORA

`/api/tendencias/serie` devuelve el mismo formato que `/api/biomas/serie`, es decir, un "mapa" por año:

1. **Cuadro de cada año** (`/api/tendencias/cuadro`): 4 barras horizontales con colores exactos, una por capa musical. El largo de cada barra es el valor de ese año entre el mínimo y el máximo de la serie.
2. **Notas:** si la variable sube con los años, la barra crece y la nota sube.
3. **Pasos:** cada paso musical es un año. Los eventos de `PERU_EVENTS` (El Niño de 2017 y 2023, sequías amazónicas) se anuncian solos.

## Regenerar el JSON

Se genera desde la solución **TrendDetective**, que tiene los datos y el código estadístico:

```bash
# 1. (opcional) reentrenar las redes con los datos ya descargados
dotnet run --project TrendDetective/TrendDetective.ML.Trainer -c Release -- --run --data giovanni --name sonora-giovanni
dotnet run --project TrendDetective/TrendDetective.ML.Trainer -c Release -- --run --data power --name sonora-power

# 2. exportar a esta carpeta
dotnet run --project TrendDetective/TrendDetective.Sonora -- <ruta a SONORA>/entrenamiento/TendenciasPeru
```

Después, vuelve a compilar SONORA para que copie el JSON a `bin/.../Tendencias/`. También se puede indicar otra ruta con `Tendencias:DataPath`.

## Limitaciones

- **Promedios regionales, no mapas:** sirven para escuchar la evolución en el tiempo, no para mejorar la red de biomas píxel a píxel.
- **2026 es provisional:** se excluye AIRS 2026, porque la validación de TrendDetective mostró un enfriamiento irreal en casi todas las regiones.
- **Una sugerencia de la red no es una prueba:** la columna «real» (Mann-Kendall con los datos) es la que confirma la tendencia.
