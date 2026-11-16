// ======================================================================================
// SONORA en inglés: botón ES/EN, idioma inicial según el navegador (la cultura que .NET
// también lee en Blazor WebAssembly) y voz en el idioma elegido.
//
// - Textos fijos de la interfaz: diccionario propio (funciona en cualquier navegador).
// - Textos dinámicos (descripciones, contexto, asistente): traductor del propio navegador
//   (Chrome 138+, Translator API, en el dispositivo: no envía datos a ningún servidor).
//   Sin esa API se quedan en español.
// - Voz: se traduce antes de hablar y se usa una voz en inglés. El micrófono escucha en
//   inglés y la frase se pasa al español para el asistente (que entiende reglas en español).
// ======================================================================================

export let LANG = "es";
const KEY = "sonora-lang";

export function preferredLang() {
  try { const s = localStorage.getItem(KEY); if (s === "es" || s === "en") return s; } catch { /* sin almacenamiento */ }
  const l = (navigator.languages && navigator.languages[0]) || navigator.language || "es";
  return l.toLowerCase().startsWith("es") ? "es" : "en";
}

// ---------- Diccionario de la interfaz (texto exacto, sin espacios en los extremos) ----------
const D = {
  "LISTEN TO OUR PLANET": "LISTEN TO OUR PLANET",
  "Saltar al reproductor": "Skip to the player",
  "Bienvenido a SONORA": "Welcome to SONORA",
  "🎧 Empezar con audio": "🎧 Start with audio",
  "Entrar sin audio": "Enter without audio",
  "Convierte las visualizaciones de la Tierra de la NASA —o tu propia imagen o video— en música que se puede escuchar, explorar y entender sin necesidad de verla.":
    "Turns NASA's visualizations of Earth —or your own image or video— into music you can hear, explore and understand without needing to see it.",
  "1. Elige qué escuchar": "1. Choose what to listen to",
  "2. Escucha": "2. Listen",
  "3. Accesibilidad": "3. Accessibility",
  "🎵 Jukebox NASA (EIC)": "🎵 NASA Jukebox (EIC)",
  "⬆ Tu archivo": "⬆ Your file",
  "🇵🇪 Biomas de Perú": "🇵🇪 Biomes of Peru",
  "📈 Clima de Perú": "📈 Climate of Peru",
  "🌎 Todo el planeta": "🌎 Whole planet",
  "🎚 Capas": "🎚 Layers",
  "🗺 Mapa": "🗺 Map",
  "📖 Qué estás escuchando": "📖 What you are hearing",
  "🤖 Asistente": "🤖 Assistant",
  "🎧 Escuchar": "🎧 Listen",
  "🎧 Escuchar mapa": "🎧 Listen to map",
  "▶ Escuchar": "▶ Listen to",
  "año por año": "year by year",
  "🎧 Escuchar esta zona": "🎧 Listen to this area",
  "Ver versión detallada ↗": "See detailed version ↗",
  "⬆ Sube una imagen o video": "⬆ Upload an image or video",
  "Descripción de tu archivo (opcional, recomendado para accesibilidad)": "Description of your file (optional, recommended for accessibility)",
  "PNG, JPG, WebP, MP4 o WebM (máx. 300 MB). Se procesa en tu navegador: el archivo no se sube a ningún servidor.":
    "PNG, JPG, WebP, MP4 or WebM (max. 300 MB). It is processed in your browser: the file is not uploaded to any server.",
  "Mapa": "Map", "Región": "Region", "Año": "Year", "Lugar": "Place", "Datos": "Data",
  "▶ Reproducir": "▶ Play", "⏸ Pausar": "⏸ Pause",
  "Tempo": "Tempo", "Volumen": "Volume", "Escala": "Scale", "Segundos por año": "Seconds per year",
  "Ver capas detectadas": "Show detected layers",
  "🗣 Describir este paso": "🗣 Describe this step",
  "⏺ Descargar audio de la pieza": "⏺ Download audio of the piece",
  "⏹ Grabando… (pulsa para terminar)": "⏹ Recording… (press to stop)",
  "Recorrido": "Scan",
  "Radar (gira desde un centro, como Caldwell 73 de la NASA)": "Radar (sweeps around a center, like NASA's Caldwell 73)",
  "Centrar en la imagen": "Center on the image",
  "Destellos": "Sparkles",
  "melodía": "melody", "ritmo": "rhythm", "bajo": "bass", "armonía": "harmony",
  "Melodía": "Melody", "Ritmo": "Rhythm", "Bajo": "Bass", "Armonía": "Harmony",
  "Asistente de voz": "Voice assistant",
  "🎙 Hablar con SONORA": "🎙 Talk to SONORA",
  "⏳ Pensando…": "⏳ Thinking…",
  "⏹ Escuchando… (pulsa para terminar)": "⏹ Listening… (press to stop)",
  "Preguntar": "Ask",
  "O escribe: ¿dónde hay más polvo?": "Or type: where is there more dust?",
  "Narración por voz": "Voice narration",
  "Leer en voz alta cada paso y los lugares destacados": "Read each step and highlighted places aloud",
  "Velocidad": "Speed",
  "Retroalimentación táctil": "Haptic feedback",
  "Vibrar con el ritmo y el bajo": "Vibrate with the rhythm and bass",
  "Probar vibración": "Test vibration",
  "Visualización": "Display",
  "Alto contraste": "High contrast",
  "Texto grande": "Large text",
  "Texto descriptivo": "Descriptive text",
  "⬇ Descargar descripción completa (.txt)": "⬇ Download full description (.txt)",
  "⌨ Atajos de teclado": "⌨ Keyboard shortcuts",
  "Atajos de teclado": "Keyboard shortcuts",
  "Cerrar": "Close",
  "Descripción de la imagen": "Image description",
  "Contexto científico": "Scientific context",
  "Cómo suena cada capa": "How each layer sounds",
  "Resumen del recorrido": "Scan summary",
  "Este año:": "This year:",
  "listo al instante": "ready instantly",
  "Todos": "All", "Filtrar por tema": "Filter by theme",
  "Aire": "Air", "Agua": "Water", "Océano": "Ocean", "Temperatura": "Temperature", "Tierra": "Land", "Incendios": "Fires",
  "Hielo y nivel del mar": "Ice and sea level", "Gases de efecto invernadero": "Greenhouse gases",
  "la primera vez": "the first time",
  "incluida": "included",
  "Aire · imagen fija": "Air · still image",
  "Aerosoles GEOS · 19 ago 2025": "GEOS Aerosols · Aug 19, 2025",
  "Cargando el catálogo del EIC…": "Loading the EIC catalog…",
  "— Elige un lugar o mueve el mapa —": "— Choose a place or move the map —",
  "Serie": "Series", "Periodo": "Period", "provisional": "provisional",
  "Un solo año": "A single year",
  "Un solo mapa que resume todo el periodo": "One map summarizing the whole period",
  "Usar las 4 que sugiere la red": "Use the 4 suggested by the network",
  "★ Usar las 4 que sugiere la red": "★ Use the 4 suggested by the network",
  "📈 Tendencias del clima en Perú": "📈 Climate trends in Peru",
  "Preparando…": "Preparing…",
  "Listo": "Ready",
  "Mapas mensuales del planeta": "Monthly maps of the planet", "Actualizado a diario": "Updated daily",
  "Polvo del Sahara hacia el huracán Erin, humo de África y sal marina. Usa la paleta oficial de la NASA (sin k-means).":
    "Saharan dust heading toward Hurricane Erin, smoke from Africa and sea salt. It uses NASA's official palette (no k-means).",
  // ---- Textos fijos de la página ----
  "SONORA necesita JavaScript para generar el audio.": "SONORA needs JavaScript to generate the audio.",
  "Elige una visualización de la Tierra de la NASA, como las del": "Choose a NASA visualization of Earth, like those of the",
  ". SONORA recorre sus cuadros en el tiempo y convierte sus 4 familias de color en melodía, ritmo, bajo y armonía.":
    ". SONORA steps through its frames over time and turns its 4 color families into melody, rhythm, bass and harmony.",
  "Bioma más probable de cada celda según la red neuronal, agrupado en 4 familias.": "Most likely biome of each cell according to the neural network, grouped into 4 families.",
  "Selva (melodía), Agricultura y urbano (ritmo), Andes (bajo), Costa (armonía)": "Rainforest (melody), Farmland and urban (rhythm), Andes (bass), Coast (harmony)",
  "(datos NASA + red neuronal de TrendDetective)": "(NASA data + TrendDetective neural network)",
  "Escucha cómo cambió el clima de una región año por año. La red neuronal sugiere qué variables están cambiando (★); las 4 elegidas suenan como melodía, ritmo, bajo y armonía, en ese orden. Si una variable sube con los años, su nota sube.":
    "Hear how the climate of a region changed year by year. The neural network suggests which variables are changing (★); the 4 chosen ones play as melody, rhythm, bass and harmony, in that order. If a variable rises over the years, its note rises.",
  "★ = sugerencia de la red (entrenada con 199 regiones del mundo; acierta el": "★ = network suggestion (trained on 199 regions of the world; it is right",
  "% en regiones que no vio). «Real» es lo que dicen los datos (Mann-Kendall).": "% of the time in regions it never saw). «Real» is what the data say (Mann-Kendall).",
  "Punto de rocío": "Dew point", "Vapor de agua en el aire": "Water vapor in the air",
  "Humedad del suelo (raíces)": "Soil moisture (root zone)", "Temperatura mínima": "Minimum temperature",
  "Temperatura del aire": "Air temperature", "Humedad del suelo (superficie)": "Soil moisture (surface)",
  "Temperatura máxima": "Maximum temperature", "Nubosidad": "Cloud cover", "Viento": "Wind", "Lluvia": "Rain",
  "Ver las 14 variables": "Show all 14 variables",
  "(cobertura terrestre de la NASA, 2001–2024)": "(NASA land cover, 2001–2024)",
  "Mueve el mapa o elige un lugar y pulsa «Escuchar esta zona». Se usa la clasificación oficial MODIS IGBP de la NASA (no la red neuronal, que está entrenada solo con Perú), agrupada en 4 capas: Bosque (melodía), Agricultura y urbano (ritmo), Sabana, matorral y pastizal (bajo) y Desierto y hielo (armonía). Solo suena el recuadro claro del centro (lo gris queda fuera; máximo 40° de lado). Para elegir tu propia zona, mantén Mayús y arrastra sobre el mapa; clic derecho vuelve al recuadro automático.":
    "Move the map or choose a place and press «Listen to this area». It uses NASA's official MODIS IGBP classification (not the neural network, which is trained only on Peru), grouped into 4 layers: Forest (melody), Farmland and urban (rhythm), Savanna, shrubland and grassland (bass) and Desert and ice (harmony). Only the bright box in the center plays (the gray area is left out; at most 40° per side). To choose your own area, hold Shift and drag over the map; right-click returns to the automatic box.",
  "▶ Escuchar 2001–2024 año por año": "▶ Listen to 2001–2024 year by year",
  "asistente de voz ·": "voice assistant ·",
  "Pentatónica menor": "Minor pentatonic", "Pentatónica mayor": "Major pentatonic", "Mayor (jónica)": "Major (Ionian)",
  "Menor natural (eólica)": "Natural minor (Aeolian)", "Dórica": "Dorian", "Blues": "Blues",
  "Hirajoshi (japonesa)": "Hirajoshi (Japanese)", "Tonos enteros": "Whole tone",
  "misteriosa y abierta (la propuesta original)": "mysterious and open (the original proposal)",
  "Reproduciendo. Pausa con Espacio para escuchar la descripción del paso.": "Playing. Pause with Space to hear the description of the step.",
  "de oeste a este": "from west to east",
  "En modo radar, haz doble clic en la imagen para mover el centro (por ejemplo, sobre el ojo de un huracán). Las flechas ↑ ↓ recorren anillos desde el centro.":
    "In radar mode, double-click the image to move the center (for example, onto the eye of a hurricane). The ↑ ↓ arrows move through rings from the center.",
  "sin percusión": "no percussion", "en total": "in total",
  "🗺 Mapa con contexto (OpenStreetMap): lo que está en gris no suena; el recuadro amarillo marca el paso que está sonando. Arrastra, usa la rueda o los botones + y − para acercarte.":
    "🗺 Map with context (OpenStreetMap): what is gray does not play; the yellow box marks the step that is playing. Drag, use the wheel or the + and − buttons to zoom in.",
  "Todas las notas salen de la escala": "All notes come from the scale",
  ". Progresión de acordes:": ". Chord progression:",
  "percusión granular": "granular percussion",
  "más arriba, más agudo": "higher up, higher pitch",
  "Escribe una pregunta o una orden para el asistente": "Type a question or a command for the assistant",
  "o el botón y pide lo que quieras: «haz sonar el Sahara», «busca donde hay más humo», «más lento», «sube el volumen», «más científico», «escala blues», «solo la melodía», «centra el radar en el huracán» o una pregunta como «¿por qué hay polvo sobre el Atlántico?». Di «ayuda» para oír todas las órdenes.":
    "or the button and ask for anything: «play the Sahara», «find where there is more smoke», «slower», «turn up the volume», «more scientific», «blues scale», «only the melody», «center the radar on the hurricane» or a question like «why is there dust over the Atlantic?». Say «help» to hear all the commands.",
  "El reconocimiento de voz lo hace tu navegador (en Chrome, con servidores de Google); las preguntas libres las responde un modelo de OpenAI.":
    "Speech recognition is done by your browser (in Chrome, with Google's servers); free-form questions are answered by an OpenAI model.",
  "Si ya usas un lector de pantalla (NVDA, JAWS, VoiceOver, TalkBack) puedes dejarla apagada: las descripciones se anuncian automáticamente.":
    "If you already use a screen reader (NVDA, JAWS, VoiceOver, TalkBack) you can leave it off: descriptions are announced automatically.",
  "Pulso largo = bajo (humo / capa grave); pulsos cortos = golpes de ritmo.": "Long pulse = bass (smoke / low layer); short pulses = rhythm hits.",
  "La transcripción describe la imagen, el contexto científico y cada paso musical.": "The transcript describes the image, the scientific context and each musical step.",
  "Datos: NASA Earth Information Center y Scientific Visualization Studio (SVS 5669, 5603, 5688, 5687, 5572, 5436, 5554, 5624, 5616), GMAO (GEOS), NASA GIBS, POWER y Giovanni. Proyecto para el NASA Space Apps Challenge 2026 · The Earth Information Jukebox.":
    "Data: NASA Earth Information Center and Scientific Visualization Studio (SVS 5669, 5603, 5688, 5687, 5572, 5436, 5554, 5624, 5616), NASA Earth Observatory Global Maps, GMAO (GEOS), NASA GIBS, POWER and Giovanni. Project for the NASA Space Apps Challenge 2026 · The Earth Information Jukebox.",
  "Optimización: HBA.Evolutionary · Audio: Tone.js": "Optimization: HBA.Evolutionary · Audio: Tone.js",
  "Reproducir o pausar (al pausar se describe el paso actual)": "Play or pause (pausing describes the current step)",
  "Paso anterior o siguiente (suena y se describe)": "Previous or next step (it plays and is described)",
  "Primer o último paso": "First or last step",
  "Modo exploración: recorre las 8 franjas horizontales (latitudes en el mapa de la NASA) o, en modo radar, los 8 anillos desde el centro":
    "Explore mode: move through the 8 horizontal bands (latitudes on the NASA map) or, in radar mode, the 8 rings from the center",
  "Salir de la exploración y volver al paso completo": "Leave explore mode and return to the whole step",
  "Hablar con el asistente de voz (vuelve a pulsar para terminar). Di «ayuda» para oír las órdenes": "Talk to the voice assistant (press again to finish). Say «help» to hear the commands",
  "Describir con detalle el paso actual": "Describe the current step in detail",
  "Activar o desactivar la narración por voz": "Turn voice narration on or off",
  "Silenciar o activar cada capa (melodía, ritmo, bajo, armonía, destellos)": "Mute or unmute each layer (melody, rhythm, bass, harmony, sparkles)",
  "En modo radar, mueve el centro del giro a ese punto de la imagen": "In radar mode, move the center of the sweep to that point of the image",
  "Cambiar a la siguiente escala musical (pentatónica, mayor, menor, dórica, blues, hirajoshi, tonos enteros)": "Switch to the next musical scale (pentatonic, major, minor, Dorian, blues, hirajoshi, whole tone)",
  "Con lector de pantalla, entra en el explorador sonoro (zona de la imagen) para que las flechas lleguen a SONORA.": "With a screen reader, enter the sound explorer (the image area) so the arrow keys reach SONORA.",
  "Ocurrió un error inesperado.": "An unexpected error occurred.",
  "Qué escuchar": "What to listen to",
  "Ej.: Temperatura del mar en el Pacífico durante El Niño 2023": "E.g.: Sea temperature in the Pacific during El Niño 2023",
  "Detalles de lo que suena": "Details of what is playing",
  "Más intensidad = nota más aguda y más fuerte (escala pentatónica menor, en do).": "More intensity = higher and louder note (minor pentatonic scale, in C).",
  "Más intensidad = más golpes por paso (1, 2 o 4) y bombo cuando es muy densa.": "More intensity = more hits per step (1, 2 or 4) and a kick drum when it is very dense.",
  "Suena cuando la capa aparece; su intensidad controla el volumen.": "It plays when the layer appears; its intensity controls the volume.",
  "Cada píxel se compara con los colores de la barra oficial de la NASA. Las intensidades son relativas dentro de esta imagen, no concentraciones absolutas.":
    "Each pixel is compared with the colors of NASA's official color bar. Intensities are relative within this image, not absolute concentrations.",
  "Pulsa": "Press", "Tú:": "You:",
  "🔁 Manos libres": "🔁 Hands-free", "🔁 Manos libres activado": "🔁 Hands-free on", "📖 Leer la página": "📖 Read the page",
  "Navegar solo con la voz:": "Browse with your voice only:",
  "«lee la página», «para de leer», «sigue leyendo», «siguiente sección», «qué secciones hay», «qué hay aquí», «ve a accesibilidad», «abre la pestaña clima», «pulsa descargar video», «activa alto contraste», «qué visualizaciones hay», «escucha la número 3». Con":
    "«read the page», «stop reading», «keep reading», «next section», «what sections are there», «what is here», «go to accessibility», «open the climate tab», «press download video», «turn on high contrast», «what visualizations are there», «listen to number 3». With",
  "(manos libres) no hace falta pulsar nada: SONORA te escucha todo el tiempo.": "(hands-free) you don't need to press anything: SONORA listens all the time.",
  "manos libres ·": "hands-free ·", "leer la página ·": "read the page ·",
  "Modo manos libres: el asistente te escucha todo el tiempo, sin pulsar nada (vuelve a pulsar para apagarlo)":
    "Hands-free mode: the assistant listens all the time, without pressing anything (press again to turn it off)",
  "Leer toda la página en voz alta; vuelve a pulsar para detener o seguir donde iba": "Read the whole page aloud; press again to stop or continue where it left off",
  "Callar la lectura de la página": "Silence the page reading",
  "Modo manos libres desactivado. Pulsa M para volver a activarlo.": "Hands-free mode off. Press M to turn it on again.",
  "Modo manos libres activado. Te escucho todo el tiempo; para terminar, di «desactiva manos libres».": "Hands-free mode on. I'm listening all the time; to finish, say «turn off hands-free».",
  "Modo manos libres activado. Te escucho todo el tiempo: di lo que quieras, por ejemplo «lee la página». Para terminar, di «desactiva manos libres».":
    "Hands-free mode on. I'm listening all the time: say anything, for example «read the page». To finish, say «turn off hands-free».",
  "Pongo en pausa el modo manos libres porque no te escuché. Pulsa M o A para seguir hablando.": "I'm pausing hands-free mode because I didn't hear you. Press M or A to keep talking.",
  "Manos libres: te escucho…": "Hands-free: I'm listening…",
  "Tu navegador no admite reconocimiento de voz. Usa Chrome o Edge para el modo manos libres.": "Your browser doesn't support speech recognition. Use Chrome or Edge for hands-free mode.",
  "Asistente": "Assistant", "Abrir el asistente de voz": "Open the voice assistant", "Cerrar el asistente": "Close the assistant",
  "🎬 Descargar video con sonido": "🎬 Download video with sound",
  "⏹ Grabando video… (pulsa para terminar)": "⏹ Recording video… (press to stop)",
  // ---------- Bienvenida accesible y guía hablada ----------
  "Bienvenido a Sonora": "Welcome to Sonora",
  "Escucha nuestro planeta. ¿Necesitas ayuda para navegar? Puedes activar la guía hablada o continuar sin ella.":
    "Listen to our planet. Do you need help to navigate? You can turn on the spoken guide or continue without it.",
  "Activar guía hablada": "Turn on spoken guide", "Ahora no": "Not now", "🧭 Activar guía hablada": "🧭 Turn on spoken guide",
  "Enter activa la guía y Escape continúa sin ella. La guía no enciende el micrófono: podrás usar el teclado, tocar los botones o, si quieres, elegir «Usar micrófono».":
    "Enter turns on the guide and Escape continues without it. The guide does not turn on the microphone: you can use the keyboard, tap the buttons or, if you want, choose «Use microphone».",
  "Cerrar asistente": "Close assistant", "✕ Cerrar": "✕ Close", "Guía hablada": "Spoken guide", "Estás en:": "You are in:",
  "⏹ Detener voz": "⏹ Stop voice", "🔈 Voz de SONORA: activada": "🔈 SONORA voice: on", "🔇 Voz de SONORA: desactivada": "🔇 SONORA voice: off",
  "🎙 Usar micrófono": "🎙 Use microphone", "⏳ Procesando…": "⏳ Processing…", "Enviar": "Send", "✎ Corregir": "✎ Correct", "Entendí: «": "I understood: «",
  "Escuchando": "Listening", "Procesando": "Processing", "Cargando": "Loading", "Reproduciendo": "Playing", "En pausa": "Paused",
  "◀ Opciones anteriores": "◀ Previous options", "Más opciones ▶": "More options ▶", "↩ Volver": "↩ Back", "☰ Menú": "☰ Menu",
  "⟳ Reintentar": "⟳ Retry", "✎ Corregir la última elección": "✎ Correct the last choice", "☰ Volver al menú": "☰ Back to menu",
  "🗣 Describir el momento": "🗣 Describe this moment", "🔉 Bajar volumen": "🔉 Volume down", "🔊 Subir volumen": "🔊 Volume up",
  "Explicación sencilla": "Simple explanation", "Explicación detallada": "Detailed explanation", "🎧 Aprender a escuchar": "🎧 Learn to listen",
  "↩ Cambiar contenido": "↩ Change content", "O escribe el año": "Or type the year", "Elegir año": "Choose year",
  "¿Qué quieres escuchar?": "What do you want to listen to?", "Tu selección": "Your selection",
  "Voz de SONORA (asistente, guía y lectura)": "SONORA voice (assistant, guide and reading)",
};
const DICT = new Map(Object.entries(D).map(([k, v]) => [k.replace(/\s+/g, " "), v]));

// ---------- Traductor del navegador (Translator API) ----------
const cache = new Map();
let toEn = null, toEs = null, toEnPending = null;
// La API puede quedarse esperando sin fin (descarga del modelo, navegadores embebidos): nunca bloquear la página.
const within = (p, ms, fallback) => Promise.race([p, new Promise(r => setTimeout(() => r(fallback), ms))]);
async function makeTranslator(source, target, ms = 4000) {
  try {
    if (!("Translator" in self)) return null;
    const a = await within(Translator.availability({ sourceLanguage: source, targetLanguage: target }), ms, "unavailable");
    if (a === "unavailable") return null;
    return await within(Translator.create({ sourceLanguage: source, targetLanguage: target }), a === "available" ? ms : 120000, null);
  } catch { return null; }
}
export async function translatorStatus() {
  if (!("Translator" in self)) return "none";
  if (toEn) return "available";
  try { return await within(Translator.availability({ sourceLanguage: "es", targetLanguage: "en" }), 2000, "none"); } catch { return "none"; }
}

/** Español → inglés (si LANG es "en"): diccionario, caché o traductor del navegador; si no, el mismo texto. */
export async function tr(text) {
  if (LANG !== "en" || !text || !/[a-záéíóúñ]/i.test(text)) return text;
  const k = text.trim().replace(/\s+/g, " ");               // el HTML de Razor trae saltos de línea y sangrías
  const lead = text.match(/^\s*/)[0] ? " " : "", tail = text.match(/\s*$/)[0] ? " " : "";
  const wrap = s => lead + s + tail;
  if (DICT.has(k)) return wrap(DICT.get(k));
  if (cache.has(k)) return wrap(cache.get(k));
  if (!toEn) return text;
  try { const out = await toEn.translate(k); cache.set(k, out); return wrap(out); } catch { return text; }
}
/** Inglés → español (lo que dice la persona al asistente). */
export async function toSpanish(text) {
  if (LANG !== "en" || !text) return text;
  toEs = toEs || await makeTranslator("en", "es", 2000);
  if (!toEs) return text;
  try { return await toEs.translate(text); } catch { return text; }
}

// ---------- Traducción de la página (textos y atributos accesibles) ----------
const ATTRS = ["aria-label", "placeholder", "title", "alt", "aria-valuetext", "aria-roledescription"];
const orig = new WeakMap();   // nodo de texto → { es, en }
const origAttr = new WeakMap(); // elemento → { attr: { es, en } }
let observer = null;
const SKIP = new Set(["SCRIPT", "STYLE", "TEXTAREA", "CODE", "KBD"]);

async function trText(node) {
  const v = node.nodeValue;
  if (!v || !v.trim() || !/[a-záéíóúñ]/i.test(v)) return;
  const o = orig.get(node);
  if (o && v === o.en && o.en !== o.es) return; // ya traducido por nosotros
  const es = o && v === o.en ? o.es : v;
  const en = await tr(es);
  if (node.nodeValue !== v) return; // Blazor lo cambió mientras traducíamos
  orig.set(node, { es, en });
  if (en !== es) node.nodeValue = en;
}
async function trAttrs(el) {
  for (const a of ATTRS) {
    const v = el.getAttribute && el.getAttribute(a);
    if (!v || !/[a-záéíóúñ]/i.test(v)) continue;
    const m = origAttr.get(el) || {}; const o = m[a];
    if (o && v === o.en && o.en !== o.es) continue;
    const en = await tr(v);
    if (el.getAttribute(a) !== v) continue;
    m[a] = { es: v, en }; origAttr.set(el, m);
    if (en !== v) el.setAttribute(a, en);
  }
}
function walk(root) {
  if (!root) return;
  if (root.nodeType === 3) { trText(root); return; }
  if (root.nodeType !== 1 || SKIP.has(root.tagName) || root.closest?.("[data-no-tr]")) return;
  trAttrs(root);
  const it = document.createTreeWalker(root, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT, {
    acceptNode: n => n.nodeType === 1
      ? (SKIP.has(n.tagName) || n.hasAttribute("data-no-tr") ? NodeFilter.FILTER_REJECT : NodeFilter.FILTER_SKIP)
      : NodeFilter.FILTER_ACCEPT,
  });
  root.querySelectorAll?.("[aria-label],[placeholder],[title],[alt],[aria-valuetext],[aria-roledescription]").forEach(e => { if (!e.closest("[data-no-tr]")) trAttrs(e); });
  for (let n = it.nextNode(); n; n = it.nextNode()) trText(n);
}
function restore(root) {
  const it = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  for (let n = it.nextNode(); n; n = it.nextNode()) { const o = orig.get(n); if (o && n.nodeValue === o.en) n.nodeValue = o.es; orig.delete(n); }
  root.querySelectorAll("*").forEach(e => {
    const m = origAttr.get(e); if (!m) return;
    for (const [a, o] of Object.entries(m)) if (e.getAttribute(a) === o.en) e.setAttribute(a, o.es);
    origAttr.delete(e);
  });
}

/** Cambia el idioma. Devuelve el estado del traductor del navegador ("available", "downloadable", "none"…). */
export async function setLang(lang) {
  LANG = lang === "en" ? "en" : "es";
  try { localStorage.removeItem(KEY); } catch { /* sin almacenamiento */ } // el idioma ya no se guarda: siempre el del navegador
  document.documentElement.lang = LANG;
  if (observer) { observer.disconnect(); observer = null; }
  if (LANG === "es") { restore(document.body); return "es"; }
  // 1) Al instante con el diccionario. 2) Cuando el traductor del navegador esté listo, se repasa la página
  //    para los textos dinámicos (descripciones, contexto…).
  walk(document.body);
  if (!toEn && !toEnPending) {
    toEnPending = makeTranslator("es", "en").then(t => {
      toEnPending = null;
      if (t) { toEn = t; if (LANG === "en") walk(document.body); }
      return t;
    });
  }
  observer = new MutationObserver(muts => {
    for (const m of muts) {
      if (m.type === "characterData") trText(m.target);
      else if (m.type === "attributes") trAttrs(m.target);
      else m.addedNodes.forEach(walk);
    }
  });
  observer.observe(document.body, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ATTRS });
  return toEn ? "available" : await translatorStatus();
}

/** Texto original en español de un elemento (aunque la página esté en inglés): para entender órdenes de voz. */
export function esText(el) {
  if (!el) return "";
  if (el.nodeType === 3) { const o = orig.get(el); return o && el.nodeValue === o.en ? o.es : el.nodeValue; }
  let s = "";
  const it = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
  for (let n = it.nextNode(); n; n = it.nextNode()) { const o = orig.get(n); s += o && n.nodeValue === o.en ? o.es : n.nodeValue; }
  return s;
}
/** Valor original en español de un atributo (aria-label, title…). */
export function esAttr(el, a) {
  const v = el?.getAttribute?.(a); if (!v) return "";
  const o = (origAttr.get(el) || {})[a];
  return o && v === o.en ? o.es : v;
}

/** Textos con versión en inglés propia (p. ej. el catálogo del EIC): se registran en el diccionario. */
export function addTranslations(pairs) {
  for (const [es, en] of Object.entries(pairs || {})) if (es && en) DICT.set(es.trim().replace(/\s+/g, " "), en);
  if (LANG === "en") walk(document.body);
}
