# SONORA · LISTEN TO OUR PLANET

Proyecto para el **NASA Space Apps Challenge 2026 · The Earth Information Jukebox**.
SONORA convierte visualizaciones de la Tierra de la NASA, o cualquier imagen o video, en música
que se puede escuchar, explorar y entender sin verla.

*(English version: [README.en.md](README.en.md))*

## 🎵 Jukebox del Earth Information Center (EIC)
La pestaña **Jukebox NASA (EIC)** reúne 24 visualizaciones de la Tierra de la NASA, con filtro por tema:
- **NASA SVS (9):** anomalías de temperatura 1880–2025; El Niño 2026 (temperatura y color del océano); aerosoles GEOS (imagen fija y globo girando); CO₂ y metano por origen; pronóstico de humo; ICESat-2.
- **NASA Earth Observatory · Global Maps (15 mapas mensuales):** lluvia, vapor de agua, nubosidad, nieve, temperatura y anomalía del mar, clorofila, temperatura y anomalía terrestre, radiación neta, vegetación, incendios, monóxido de carbono, aerosoles y su tamaño.

**Cómo funciona:** cada visualización está incluida en `SONORA/SONORA/wwwroot/eic/` como una hoja de cuadros WebP (48 cuadros de 480 px, ~1 MB). Abre al instante, no depende de servidores externos y todo cabe en un hosting pequeño. El navegador recorta los cuadros y los convierte en música en tiempo real. Cómo generar hojas nuevas: [docs/eic-hojas.md](docs/eic-hojas.md).

**API:**
- `GET /api/eic/catalogo`
- `POST /api/eic/preparar/{id}`
- `GET /api/eic/video/{id}`

**Descargar audio:** el botón **⏺ Descargar audio de la pieza** graba una vuelta completa y la guarda en `.webm`, para armar una *playlist* de audios, que es el entregable alternativo del reto.

## Interfaz
- **1. Elige qué escuchar**, en pestañas:
  - Jukebox NASA (EIC).
  - Tu archivo.
  - Biomas de Perú.
  - Clima de Perú.
  - Todo el planeta.
- **2. Escucha:** la imagen con su leyenda lateral (año o periodo, colores y valores) y los controles. Debajo hay pestañas:
  - Capas.
  - Mapa: contexto de OpenStreetMap; lo que no suena queda en gris.
  - Qué estás escuchando.
- **3. Accesibilidad:** siempre visible.
- **Bienvenida accesible:** al entrar, «¿Necesitas ayuda para navegar?» con los botones **Activar guía hablada** y **Ahora no** (Enter y Escape).
- **Asistente:** botón **Asistente** con texto, abajo a la derecha, en todas las secciones. Incluye la **guía hablada** (secciones y datos en grupos cortos, por teclado, tacto o voz), explicaciones sencilla y detallada, «Aprender a escuchar», «¿dónde estoy?» y «¿qué puedo hacer aquí?». El micrófono solo se usa si se pulsa «Usar micrófono» (tecla `A`); también hay manos libres (`M`) y lectura de la página (`L`). Detalles: [docs/asistente-de-voz.md](docs/asistente-de-voz.md).
- **Idioma automático:** español o inglés según el navegador; la voz y el asistente lo siguen.
- **Descargas:** audio de la pieza (.m4a o .webm) y video con sonido (.mp4 o .webm).

## Ejecutar
```bash
dotnet run --project SONORA/SONORA/SONORA.csproj --launch-profile http
```
Abre http://localhost:5241. También puedes abrir `SONORA.slnx` en Visual Studio.

## Estructura
| Ruta | Qué es |
|---|---|
| `SONORA/SONORA/` | Servidor ASP.NET Core. Expone `POST /api/optimize` (NSGA-II con HBA.Evolutionary) |
| `SONORA/SONORA/Sonification/MappingOptimizer.cs` | Motor musical en C#, cromosoma y optimizador |
| `SONORA/SONORA.Client/Pages/Home.razor` | Interfaz (Blazor WebAssembly) |
| `SONORA/SONORA.Client/Services/Describer.cs` | Textos descriptivos en español (lector de pantalla, voz, transcripción) |
| `SONORA/SONORA.Client/Services/GuideFlow.cs`, `Explainer.cs`, `SoundRules.cs`, `StatusAnnouncer.cs` | Guía hablada (estados y órdenes contextuales), explicaciones con las reglas reales del sonido y estados anunciados |
| `SONORA/SONORA.Client/Components/GuidePanel.razor` | Panel de la guía hablada dentro del asistente |
| `SONORA/SONORA.Tests/` | Pruebas unitarias (xUnit) de la guía, los estados y las explicaciones |
| `tests/e2e/` | Prueba de extremo a extremo del asistente accesible (Playwright) |
| `SONORA/SONORA.Client/wwwroot/js/sonora.js` | Análisis de imagen y video (canvas), k-means, audio (Tone.js), teclado, vibración y voz |
| `SONORA/SONORA.Client/wwwroot/data/` | Imagen y barra de colores de NASA SVS 5669 y frente de Pareto precalculado |
| `SONORA/SONORA/Peru/` | Atlas de biomas de Perú en el servidor y API `/api/biomas/*` |
| `entrenamiento/BiomasPeru.Core/` | Librería compartida: datos NASA GIBS, red neuronal, tendencias y dibujo de mapas (`Atlas`) |
| `entrenamiento/BiomasPeru/` | Consola que entrena el modelo de biomas (Hba.NeuralNet + NSGA-II) y genera los mapas de referencia |
| `SONORA/SONORA/Eic/` | Jukebox del Earth Information Center: catálogo de NASA SVS, caché de videos y API `/api/eic/*` |
| `SONORA/SONORA.Client/Components/EicJukebox.razor`, `TabBar.razor` | Galería del EIC y pestañas accesibles de la interfaz |
| `SONORA/SONORA/Tendencias/` | Tendencias del clima en Perú (datos y red de TrendDetective) y API `/api/tendencias/*` |
| `SONORA/SONORA.Client/Components/TendenciasPeru.razor` | Bloque «📈 Tendencias del clima en Perú» de la interfaz |
| `entrenamiento/TendenciasPeru/` | `tendencias_peru.json` exportado por TrendDetective: series 1981–2026, tendencias y sugerencias de la red |
| `prototipo/` | Primer prototipo HTML y optimizador de consola (referencia) |

## Cómo funciona
1. **Fuente:** ejemplo NASA (aerosoles GEOS, paleta oficial) o un archivo subido.
   - **Imagen:** se recorre en 32 columnas.
   - **Video:** de 16 a 64 fotogramas.
2. **Capas:** 4 familias de color. En el ejemplo NASA son sulfatos, polvo, humo y sal marina; en los archivos subidos se detectan con k-means.
   - Se convierten en **melodía, ritmo, bajo y armonía**.
3. **NSGA-II** (HBA.Evolutionary, en el servidor) busca reglas musicales que equilibren **fidelidad** y **musicalidad**.
   - El usuario elige su punto del frente de Pareto.
4. **Recorrido:**
   - **Lineal:** de izquierda a derecha (de oeste a este en el mapa).
   - **Radar:** como la sonificación [Caldwell 73](https://svs.gsfc.nasa.gov/13786/) de la NASA, una línea gira desde las 12 alrededor de un centro. Se puede mover el centro con doble clic; la distancia al centro decide el tono.
5. **Destellos:** los puntos brillantes aislados suenan uno a uno: marimba si son cálidos, glockenspiel si son fríos o blancos.
6. **Escalas:** pentatónica menor (por defecto), pentatónica mayor, mayor, menor natural, dórica, blues, hirajoshi y tonos enteros. Todas tienen 15 grados, así que el frente de Pareto vale para cualquiera.
7. **Verificación:** el motor existe en C# y en JS, y deben ser idénticos. Al cargar cada frente se comprueba que generen la misma música.

## 🇵🇪 Biomas de Perú (IA + NASA)
SONORA genera **al instante** mapas de biomas y de cambio ambiental de Perú, y los convierte en música. Es la parte de SONORA pensada para el reto *Earth System Trend Detective*: escuchar dónde y cuándo cambia el territorio.

### Cómo funciona
1. **Modelo:** una red neuronal (**Hba.NeuralNet**) clasifica 8 biomas peruanos.
   - **Entradas:** NDVI y temperatura de superficie MODIS mensuales, relieve SRTM y cobertura IGBP, de NASA GIBS, 2001–2024, en una rejilla de unos 5,5 km.
   - **Hiperparámetros:** los buscó un algoritmo evolutivo NSGA-II (**HBA.Evolutionary**).
   - Ver [`entrenamiento/BiomasPeru/README.md`](entrenamiento/BiomasPeru/README.md).
2. **Al arrancar:** el servidor carga los datos y el modelo y precalcula los 24 años en unos 5 s, en segundo plano. La primera vez descarga unos 38 MB de NASA GIBS.
3. **Bajo demanda:** cada mapa se dibuja en unos 30 ms, o 2 ms si ya está en caché.
4. **Paleta conocida:** los mapas usan **4 colores exactos**, uno por capa musical. SONORA no tiene que adivinar colores con k-means y cada capa suena con su nombre real.
5. **Recorrido geográfico:** el mapa se recorre de oeste a este. Cada paso dice su longitud y las ciudades que cruza: Lima, Pucallpa, Puerto Maldonado…

| Mapa | Año | Melodía | Ritmo | Bajo | Armonía |
|---|---|---|---|---|---|
| Biomas | sí | Selva | Agricultura y urbano | Andes | Costa |
| Cambio de bosque 2001–2024 | no | Pérdida de bosque | Pérdida fuerte | Ganancia de bosque | Bosque estable |
| Tendencia del verdor (NDVI) | no | Verdor bajando | Bajando fuerte | Verdor subiendo | Subiendo fuerte |
| Anomalía del verdor (NDVI) | sí | Más verde que lo normal | Extremo seco | Más seco que lo normal | Extremo húmedo |

**Regiones:** Perú completo, Madre de Dios, Ucayali, San Martín, Cordillera Blanca, costa norte (Piura y Tumbes), altiplano (Puno) y Loreto.

### Cómo usarlo
- **Interfaz:** en "1. Elige qué escuchar", bloque **🇵🇪 Biomas de Perú**. Se elige mapa, región y año, y luego:
  - **Escuchar mapa** carga un solo mapa.
  - **Escuchar 2001–2024 año por año** carga una serie en la que cada paso musical es un año.
  - **Ver versión detallada** abre el mapa con paletas completas.
- **Voz o texto** (tecla `A`), con frases como:
  - «muéstrame la deforestación en Madre de Dios»
  - «biomas de Perú en 2010»
  - «evolución de los biomas en Ucayali»
  - «anomalía del verdor en Piura en 2017»
  - «¿cómo fue El Niño en la costa norte año por año?»
  - Después, «llévame a Pucallpa» salta a esa longitud.
  - Las frases habituales se entienden con reglas al instante, y las libres, con el asistente de OpenAI (acción `load_map`).

### API
| Endpoint | Devuelve |
|---|---|
| `GET /api/biomas/catalogo` | Estado del atlas (`calentando`, `descargando datos NASA`, `calculando atlas`, `listo` o `error`), tipos con su paleta, años y regiones |
| `GET /api/biomas/mapa?tipo=&anio=&region=&estilo=` | PNG. `estilo=sonora` (4 colores, por defecto) o `vista` (paletas detalladas) |
| `GET /api/biomas/info?tipo=&anio=&region=` | Título, texto alternativo con porcentajes, contexto científico y estadísticas de la región, caja geográfica, ciudades, paleta y URL de la imagen |
| `GET /api/biomas/serie?tipo=&region=` | Lo mismo con 24 fotogramas, de 2001 a 2024 (solo `biomas` y `anomalia_ndvi`) |

Mientras el atlas se prepara, `mapa`, `info` y `serie` responden `503` con `Retry-After`.

Ejemplo: `http://localhost:5241/api/biomas/mapa?tipo=cambio_bosque&region=madre-de-dios&estilo=vista`

### Configuración
| Clave | Por defecto | Uso |
|---|---|---|
| `Biomas:DataDir` | `App_Data/biomas`; en desarrollo, `../../entrenamiento/BiomasPeru/datos` | Datos NASA GIBS. Si faltan, se descargan al arrancar |
| `Biomas:ModelPath` | `bin/.../Biomas/modelo_biomas.json` | Modelo entrenado. Se copia desde `entrenamiento/BiomasPeru/salida/` al compilar |

Para **reentrenar** el modelo, ejecuta `cd entrenamiento/BiomasPeru && dotnet run -c Release -- --minutos 12`, que tarda unos 7 minutos. Luego vuelve a compilar SONORA para que copie el modelo nuevo.

## 📈 Tendencias del clima en Perú (datos y red de TrendDetective)
Esta sección escucha cómo cambió el clima de 11 regiones de Perú **año por año**, de 1981 a 2026. Usa datos que ya descargó el proyecto TrendDetective y **no descarga nada**.

- **Datos:**
  - NASA POWER: temperatura, lluvia, humedad, viento, radiación y nubes, de 1981 a 2025.
  - NASA Giovanni: lluvia GPM, suelo y nieve GLDAS, ozono y aerosoles OMI, de 2000 a 2025.
  - 2026 se incluye como año provisional.
- **Red neuronal:** está entrenada con 199 regiones del mundo y estima qué variables de cada región están cambiando. Esas variables se proponen primero (★).
- **Sonido:** las 4 variables elegidas suenan como melodía, ritmo, bajo y armonía. Cuando la variable sube, la nota sube.
- **Validación de la sugerencia:** la columna «real» muestra lo que dice la prueba de Mann-Kendall con los datos.
- **API:**
  - `GET /api/tendencias/catalogo`: regiones, fuentes y variables, ordenadas por la red.
  - `GET /api/tendencias/serie?region=&fuente=&vars=`: serie año por año, en el mismo formato que `/api/biomas/serie`.
  - `GET /api/tendencias/cuadro?...&anio=`: PNG de un año.
- **Detalles y cómo regenerar el JSON:** ver [`entrenamiento/TendenciasPeru/README.md`](entrenamiento/TendenciasPeru/README.md).

## Accesibilidad
- **Lector de pantalla:**
  - Región `aria-live` que describe el paso al navegar o pausar y anuncia los lugares destacados durante la reproducción.
  - Explorador con `role="application"` para que las flechas lleguen a la app.
  - Texto alternativo rico.
- **Exploración espacial:**
  - `←` `→` recorren pasos.
  - `↑` `↓` recorren 8 franjas horizontales (latitudes en el mapa de la NASA). Cada celda suena y se describe.
- **Narración por voz** (Web Speech API, español) con velocidad ajustable.
- **Vibración** (Vibration API, Android): pulso largo = bajo, pulsos cortos = golpes de ritmo.
- **Texto descriptivo:**
  - Resumen automático de dónde y cuándo aparece cada capa.
  - Contexto científico del ejemplo NASA.
  - Transcripción completa descargable en .txt.
  - Campo para que quien sube un archivo lo describa.
- **Visual:** alto contraste, texto grande, foco visible, `prefers-reduced-motion` y enlace para saltar al reproductor.
- **Teclado completo:** Espacio, flechas, Inicio/Fin, Esc o Ctrl (detener la voz), A (micrófono del asistente), M (manos libres), L (leer la página), D (describir), V (narración), E (escala), 1–5 (capas y destellos), H (ayuda).
- **Guía hablada y voz propia:** ver [docs/asistente-de-voz.md](docs/asistente-de-voz.md). La **voz de SONORA** se puede desactivar para usar el lector de pantalla; la velocidad, el volumen, la guía y la voz se recuerdan en el navegador (el micrófono no).
- **Uso solo con la voz (personas ciegas)** — `SONORA.Client/wwwroot/js/lector.js`:
  - **Lector de página:** lee la página en orden diciendo qué es cada cosa («Sección…», «Pestaña…, seleccionada», «Botón…», «Casilla…, marcada», «Deslizador…, 88 BPM») y resalta lo que lee. `L` empieza, detiene o sigue; `Ctrl` o `Esc` lo callan.
  - **Navegación por voz:** «lee la página», «para de leer», «sigue leyendo», «siguiente sección», «lee esta sección», «qué secciones hay», «qué hay aquí», «dónde estoy en la página», «ve a accesibilidad», «abre la pestaña clima», «pulsa descargar video», «activa alto contraste», «qué visualizaciones hay», «escucha la número 3» o «escucha lluvia total». Busca en los textos de la página, así que funciona con cualquier botón o sección nueva.
  - **Manos libres:** `M` o «activa manos libres». Escucha, responde y vuelve a escuchar sin pulsar nada; mientras lee la página no escucha (oiría su propia voz). Se apaga con «desactiva manos libres», «deja de escuchar» o `M`, y se pausa solo tras cuatro silencios.
  - Orden de las órdenes: orientación y explicaciones, la guía hablada (si está activa), la navegación de la página, las reglas de la música y el mapa (`VoiceAssistant.Parse`) y por último el modelo de OpenAI.
  - Funciona en Chrome y Edge (reconocimiento de voz del navegador); en inglés acepta también órdenes como «read the page», «go to accessibility» o «press download video».

## Créditos
- **Datos:**
  - NASA Scientific Visualization Studio y GMAO (GEOS), visualización 5669.
  - NASA GIBS: MODIS NDVI y temperatura de superficie, MODIS IGBP y SRTM, para los biomas de Perú.
- **Optimización:** HBA.Evolutionary.
- **Red neuronal:** Hba.NeuralNet.
- **Audio:** Tone.js.
