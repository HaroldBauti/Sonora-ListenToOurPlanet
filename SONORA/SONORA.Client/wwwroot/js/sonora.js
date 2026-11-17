import { LANG, tr, toSpanish } from "./i18n.js";
export { setLang, preferredLang, addTranslations } from "./i18n.js";
import * as lector from "./lector.js";
export { waitReaderIdle, isReading, stopReading, navigationHelp } from "./lector.js";
// Lector de página y navegación por voz (lector.js): misma velocidad de voz que el asistente.
let readerRate = 1;
lector.setReaderHooks({ duck: on => duck(on), rate: () => readerRate });
export function pageCommand(text, rate) { readerRate = rate || readerRate; return lector.pageCommand(text); }
export function toggleReading(rate) { readerRate = rate || readerRate; lector.toggleReading(); }
// SONORA · motor de análisis visual y síntesis musical (Tone.js global).
// Blazor llama a las funciones exportadas; este módulo avisa a Blazor con dotnet.invokeMethodAsync("OnStep", info).
//
// IMPORTANTE: ema() y compile() deben ser idénticos a MusicEngine en SONORA/Sonification/MappingOptimizer.cs.

const ROWS = 8;
const ROLES = ["melody", "rhythm", "bass", "pad"];
// ---------- Escalas ----------
// Todas tienen 15 grados (índices 0–14): el optimizador NSGA-II y la paridad C#/JS no cambian;
// solo cambia qué nota suena en cada grado y la progresión de acordes.
const NAMES = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];
const NAMES_ES = ["Do", "Re♭", "Re", "Mi♭", "Mi", "Fa", "Sol♭", "Sol", "La♭", "La", "Si♭", "Si"];
const midiName = m => NAMES[m % 12] + (Math.floor(m / 12) - 1);
const QUAL = { M: [[0, 4, 7], "mayor"], m: [[0, 3, 7], "menor"], d7: [[0, 4, 10], "7"], aug: [[0, 4, 8], "aumentado"] };
function chord(semi, q) {
  const [iv, suf] = QUAL[q];
  let pr = 48 + semi; if (pr > 52) pr -= 12; // acordes alrededor de Do3
  return {
    name: suf === "7" ? `${NAMES_ES[semi]}7` : `${NAMES_ES[semi]} ${suf}`,
    notes: iv.map(i => midiName(pr + i)), root: midiName(pr - 12), fifth: midiName(pr - 5),
  };
}
const SCALES = {
  "pent-minor": { name: "Pentatónica menor", mood: "misteriosa y abierta (la propuesta original)", iv: [0, 3, 5, 7, 10], prog: [[0, "m"], [8, "M"], [3, "M"], [10, "M"]] },
  "pent-major": { name: "Pentatónica mayor", mood: "luminosa y tranquila", iv: [0, 2, 4, 7, 9], prog: [[0, "M"], [9, "m"], [5, "M"], [7, "M"]] },
  "major": { name: "Mayor (jónica)", mood: "alegre y estable", iv: [0, 2, 4, 5, 7, 9, 11], prog: [[0, "M"], [9, "m"], [5, "M"], [7, "M"]] },
  "minor": { name: "Menor natural (eólica)", mood: "melancólica", iv: [0, 2, 3, 5, 7, 8, 10], prog: [[0, "m"], [8, "M"], [3, "M"], [10, "M"]] },
  "dorian": { name: "Dórica", mood: "menor pero esperanzada", iv: [0, 2, 3, 5, 7, 9, 10], prog: [[0, "m"], [5, "M"], [3, "M"], [10, "M"]] },
  "blues": { name: "Blues", mood: "con tensión y swing", iv: [0, 3, 5, 6, 7, 10], prog: [[0, "d7"], [5, "d7"], [0, "d7"], [7, "d7"]] },
  "hirajoshi": { name: "Hirajoshi (japonesa)", mood: "oriental y contemplativa", iv: [0, 2, 3, 7, 8], prog: [[0, "m"], [8, "M"], [0, "m"], [8, "M"]] },
  "whole-tone": { name: "Tonos enteros", mood: "flotante y onírica, sin centro claro", iv: [0, 2, 4, 6, 8, 10], prog: [[0, "aug"], [2, "aug"], [0, "aug"], [10, "aug"]] },
};
function buildScale(iv) { const out = []; for (let o = 0; out.length < 15; o++) for (const i of iv) if (out.length < 15) out.push(midiName(48 + 12 * o + i)); return out; }
let SCALE = buildScale(SCALES["pent-minor"].iv);
let CHORDS = SCALES["pent-minor"].prog.map(([s, q]) => chord(s, q));
const MANUAL = { smoothMel: 0, smoothRhy: 0, melThr: 0.10, gamma: 1, pitchLow: 0, pitchSpan: 15, rhy1: 0.12, rhy2: 0.45, rhy4: 0.75, bassThr: 0.12 };
const esNote = n => { const m = n.match(/^([A-G]b?)(-?\d)$/); return NAMES_ES[NAMES.indexOf(m[1])] + m[2]; };

// Ejemplo NASA: GEOS Aerosols (SVS 5669). Paleta oficial leída de la barra de colores.
const SAMPLE = {
  title: "Aerosoles GEOS · NASA SVS 5669 (19 ago 2025, 20:00 UTC)",
  image: "data/Aerosols_2025-08-19T20_00_00_1920x1080.png",
  cbar: "data/Aerosols_cbar.png",
  palette: [
    { role: "melody", name: "Sulfatos", cbarX: 1192 },
    { role: "rhythm", name: "Polvo", cbarX: 597 },
    { role: "bass", name: "Humo", cbarX: 302 },
    { role: "pad", name: "Sal marina", cbarX: 898 },
  ],
  alt: "Mapa mundial de aerosoles del modelo GEOS de la NASA. Una ancha franja lila de polvo del Sahara cruza el Atlántico hacia el huracán Erin, que gira cerca del Caribe. Sobre el sur de África hay una mancha naranja y roja de humo de quemas agrícolas. Filamentos verdes de sulfatos recorren las latitudes medias y salen de los volcanes Kīlauea y Etna. Remolinos gris azulado de sal marina cubren los océanos del sur.",
  context: "Los aerosoles son partículas diminutas, sólidas o líquidas, suspendidas en el aire. Pueden viajar miles de kilómetros y afectar la calidad del aire y la visibilidad lejos de su origen. Esta visualización del modelo GEOS de la NASA muestra el 19 de agosto de 2025: el huracán Erin arrastra polvo de África y sus vientos levantan sal marina; el polvo del Sahara cruza el Atlántico hasta el Caribe; el Kīlauea en Hawái y el Etna en Italia emiten corrientes de sulfatos; y hay humo de incendios en España y de quemas agrícolas en el sur de África.",
  landmarks: [
    { lon: [-160, -150], text: "Volcán Kīlauea, Hawái: una corriente estrecha de sulfatos se desplaza hacia el oeste." },
    { lon: [-75, -60], text: "Huracán Erin en el Atlántico: arrastra polvo africano y sus vientos levantan sal marina en espiral." },
    { lon: [-50, -25], text: "Polvo del Sahara cruzando el Atlántico hacia el Caribe." },
    { lon: [-10, 0], text: "Incendios en España: humo de carbono negro sobre la península ibérica." },
    { lon: [0, 10], text: "Desierto del Sahara: aquí se levanta el polvo que cruza el Atlántico." },
    { lon: [10, 20], text: "Monte Etna, Italia: corriente de sulfatos sobre el sur de Europa; debajo, humo de quemas agrícolas en el sur de África." },
    { lon: [20, 35], text: "Sur de África: humo de quemas agrícolas que deriva sobre el Atlántico." },
  ],
};

let dn = null, els = null, A = null, params = { ...MANUAL }, seq = null;
let audio = null, playing = false, step = 0, row = -1, lastLandmark = null;
let IMG = null;                                   // píxeles de la imagen actual (para re-analizar en otro recorrido)
const scan = { mode: "linear", cx: 0.5, cy: 0.5 }; // recorrido lineal o radar (centro en fracción del ancho/alto)
const opts = { haptics: false, showClass: false };
const muted = { melody: false, rhythm: false, bass: false, pad: false, points: false };
const MAX_POINTS_PER_STEP = 6;

// ======================================================================================
// Inicialización y teclado
// ======================================================================================
export function init(dotnetRef, stage, canvas, img, video) {
  dn = dotnetRef; els = { stage, canvas, img, video, g: canvas.getContext("2d") };
  // Bienvenida por voz: el saludo ya lo dice bienvenida.js con el primer clic o tecla (aunque .NET aún esté
  // cargando). Aquí, cuando ese saludo termina, la app sigue con lo que está sonando y la pregunta de manos libres.
  document.getElementById("sonora-cargando")?.remove();
  const w = window.__sonoraWelcome;
  const go = async () => { const finished = await (w.done || Promise.resolve(false)); dn.invokeMethodAsync("OnFirstInteraction", w.acts, finished ? "done" : "cut"); };
  if (w) { if (w.fired) go(); else w.notify = go; }
  else {
    // Sin bienvenida.js (no debería pasar): la app escucha el primer clic o tecla por su cuenta.
    const first = e => {
      ["pointerdown", "keydown"].forEach(t => document.removeEventListener(t, first, true));
      const acts = !!e.target?.closest?.("button,a,input,select,textarea,label,summary,[role=tab],canvas")
        || (e.type === "keydown" && /^( |Enter|Arrow\w+|[aAdDeEhHlLmMvV1-5])$/.test(e.key));
      setTimeout(() => dn.invokeMethodAsync("OnFirstInteraction", acts, "none"), 150);
    };
    ["pointerdown", "keydown"].forEach(t => document.addEventListener(t, first, true));
  }
  canvas.addEventListener("click", e => {
    if (!A) return;
    const r = canvas.getBoundingClientRect();
    const c = A.mode === "image" ? Math.min(A.steps - 1, Math.floor((e.clientX - r.left) / r.width * A.steps)) : step;
    const rr = Math.min(ROWS - 1, Math.floor((e.clientY - r.top) / r.height * ROWS));
    audition(c, A.mode === "image" ? -1 : rr);
  });
  // Doble clic: mueve el centro del radar a ese punto
  canvas.addEventListener("dblclick", e => {
    if (!A || A.mode !== "image") return;
    const r = canvas.getBoundingClientRect();
    dn.invokeMethodAsync("OnCenterRequested", (e.clientX - r.left) / r.width, (e.clientY - r.top) / r.height);
  });
  // Captura: así Esc puede callar la voz antes de que el panel del asistente lo use para cerrarse.
  document.addEventListener("keydown", onKey, true);
}

function onKey(e) {
  const t = e.target, tag = t.tagName;
  if (t.closest && t.closest("dialog")) return; // la bienvenida y la ayuda gestionan sus propias teclas
  // Como en los lectores de pantalla: Ctrl o Esc callan la voz de SONORA (lectura, explicación o respuesta).
  if ((e.key === "Control" || e.key === "Escape") && (lector.isReading() || isSpeaking())) { stopVoice(); if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); } return; }
  if (e.ctrlKey || e.metaKey || e.altKey) return;
  // Dentro del panel del asistente y de la guía, las letras y los números no son atajos (se usan sus botones).
  if (t.closest && t.closest("[data-no-shortcuts]")) return;
  if (tag === "TEXTAREA" || tag === "SELECT" || (tag === "INPUT" && !["checkbox", "radio", "button"].includes(t.type))) return;
  const k = e.key;
  // L (leer la página) y M (manos libres) funcionan aunque todavía no haya nada cargado.
  if (/^[lLmM]$/.test(k)) { e.preventDefault(); dn.invokeMethodAsync("OnShortcut", k.toLowerCase()); return; }
  if (!A) return;
  if (k === " " || k === "Spacebar") { if (tag === "BUTTON" || tag === "INPUT" || tag === "A" || tag === "SUMMARY") return; e.preventDefault(); toggle(); }
  else if (k === "ArrowRight") { e.preventDefault(); audition((step + 1) % A.steps, row); }
  else if (k === "ArrowLeft") { e.preventDefault(); audition((step - 1 + A.steps) % A.steps, row); }
  else if (k === "ArrowDown") { e.preventDefault(); audition(step, Math.min(ROWS - 1, row + 1)); }
  else if (k === "ArrowUp") { e.preventDefault(); audition(step, row <= 0 ? -1 : row - 1); }
  else if (k === "Home") { e.preventDefault(); audition(0, row); }
  else if (k === "End") { e.preventDefault(); audition(A.steps - 1, row); }
  else if (k === "Escape") { if (row >= 0) { e.preventDefault(); audition(step, -1); } }
  else if (/^[aAdDeEhHvV1-5]$/.test(k)) { e.preventDefault(); dn.invokeMethodAsync("OnShortcut", k.toLowerCase()); }
}

export function dispose() { document.removeEventListener("keydown", onKey, true); pause(); }

// ======================================================================================
// Carga de medios
// ======================================================================================
const loadImage = src => new Promise((res, rej) => { const i = new Image(); i.onload = () => res(i); i.onerror = () => rej(new Error("No se pudo leer la imagen.")); i.src = src; });
const progress = (pct, msg) => dn && dn.invokeMethodAsync("OnProgress", Math.round(pct), msg);

export async function loadSample() {
  stopAll();
  const [img, cbar] = await Promise.all([loadImage(SAMPLE.image), loadImage(SAMPLE.cbar)]);
  progress(30, "Calibrando la paleta oficial de la NASA…");
  // Calibrar colores con la barra oficial
  const cc = document.createElement("canvas"); cc.width = cbar.width; cc.height = cbar.height;
  const cx = cc.getContext("2d", { willReadFrequently: true }); cx.drawImage(cbar, 0, 0);
  const palette = SAMPLE.palette.map(p => {
    const d = cx.getImageData(p.cbarX - 10, 177, 20, 20).data;
    let r = 0, g = 0, b = 0; const n = d.length / 4;
    for (let i = 0; i < d.length; i += 4) { r += d[i]; g += d[i + 1]; b += d[i + 2]; }
    r /= n; g /= n; b /= n;
    const m = (r + g + b) / 3, c = [r - m, g - m, b - m], norm = Math.hypot(...c);
    return { ...p, rgb: [r, g, b].map(Math.round), norm, u: c.map(v => v / norm) };
  });
  progress(60, "Clasificando aerosoles píxel por píxel…");
  const { px, W, H } = pixelsOf(img, 480);
  IMG = { px, W, H, palette, classify: chromaClassifier(palette), kind: "sample",
          meta: { title: SAMPLE.title, source: "sample", alt: SAMPLE.alt, context: SAMPLE.context,
                  box: { lonMin: -180, lonMax: 180, latMin: -90, latMax: 90 } } };
  scan.cx = scan.cy = 0.5;
  A = analyzeImage();
  showMedia("image", SAMPLE.image, img.width, img.height);
  return finish();
}

// Analiza la imagen actual según el recorrido elegido (lineal o radar) y detecta destellos.
function analyzeImage() {
  const { px, W, H, palette, classify, kind, meta } = IMG;
  const radar = scan.mode === "radar";
  const bin = radar ? radarBinner(W, H) : linearBinner(W, H);
  const a = buildImageAnalysis(px, W, H, palette, classify, bin);
  a.scan = scan.mode;
  if (radar) {
    const deg = c => c * 360 / a.steps;
    a.stepLabels = a.cols.map(c => `sector ${c + 1} de ${a.steps}, rumbo ${degStr(deg(c))} a ${degStr(deg(c + 1))}, ${clockStr(deg(c + 0.5))}`);
    a.rowLabels = Array.from({ length: ROWS }, (_, r) => `anillo ${r + 1} de ${ROWS} desde el centro (${Math.round(r / ROWS * 100)}–${Math.round((r + 1) / ROWS * 100)} % del radio)`);
    a.landmarks = a.cols.map(() => null);
    a.stepEdges = a.cols.map(c => [degStr(deg(c)), degStr(deg(c + 1))]);
  } else if (meta.box) {
    // Mapas (ejemplo NASA o biomas de Perú): cada columna es una franja de longitud y cada fila, de latitud.
    const { lonMin, lonMax } = meta.box, dx = (lonMax - lonMin) / a.steps;
    const lons = a.cols.map(c => [lonMin + c * dx, lonMin + (c + 1) * dx]);
    a.stepLabels = lons.map(([x, y]) => `longitud ${lonStr(x)} a ${lonStr(y)}`);
    a.rowLabels = latRows(meta.box);
    a.landmarks = lons.map(([x, y]) => kind === "sample" ? sampleLandmark((x + y) / 2) : placesIn(meta.places, x, y));
    a.stepEdges = lons.map(([x, y]) => [lonStr(x), lonStr(y)]);
  } else {
    a.stepLabels = a.cols.map(c => `columna ${c + 1} de ${a.steps} (${Math.round((c + 0.5) / a.steps * 100)} % desde la izquierda)`);
    a.rowLabels = Array.from({ length: ROWS }, (_, r) => `franja ${r + 1} de ${ROWS} desde arriba`);
    a.landmarks = a.cols.map(() => null);
    a.stepEdges = a.cols.map(c => [`${Math.round(c / a.steps * 100)} %`, `${Math.round((c + 1) / a.steps * 100)} %`]);
  }
  // En los mapas de cobertura (Perú o mundo) los píxeles sueltos son celdas de clasificación, no puntos brillantes.
  a.points = kind === "geo" ? [] : detectPoints(px, W, H, bin);
  a.pointsByStep = a.cols.map(c => pick(a.points.filter(p => p.step === c)));
  Object.assign(a, meta);
  return a;
}

const pick = list => list.slice().sort((p, q) => q.vel - p.vel).slice(0, MAX_POINTS_PER_STEP).sort((p, q) => p.frac - q.frac);

// Cambia el recorrido (lineal ↔ radar) o el centro del radar sin recargar la imagen.
export function setScan(mode) {
  if (!IMG || !A || A.mode !== "image") throw new Error("El recorrido radar solo está disponible para imágenes.");
  stopAll(); scan.mode = mode === "radar" ? "radar" : "linear";
  A = analyzeImage(); return finish();
}
export function setCenter(fx, fy) {
  if (!IMG) throw new Error("Carga una imagen primero.");
  stopAll(); scan.mode = "radar";
  scan.cx = Math.min(0.98, Math.max(0.02, fx)); scan.cy = Math.min(0.98, Math.max(0.02, fy));
  A = analyzeImage(); return finish();
}

const latRows = ({ latMin, latMax }) => Array.from({ length: ROWS }, (_, r) => {
  const dy = (latMax - latMin) / ROWS, p = latMax - r * dy; return `latitud ${latStr(p)} a ${latStr(p - dy)}`;
});
const sampleLandmark = lon => (SAMPLE.landmarks.find(m => m.lon[0] <= lon && lon < m.lon[1]) || {}).text || null;
// Ciudades o lugares dentro de la franja de longitud [x, y).
function placesIn(places, x, y) {
  const names = (places || []).filter(p => p.lon >= x && p.lon < y).map(p => p.name);
  return names.length ? names.slice(0, 3).join(", ") : null;
}
const PERU_EVENTS = { 2005: "Sequía amazónica de 2005", 2010: "Sequía amazónica de 2010", 2017: "El Niño costero de 2017", 2023: "El Niño costero de 2023" };
const knownPalette = info => info.palette.map(p => ({ role: p.role, name: p.name, rgb: p.rgb, reach: p.reach }));

// Mapa de Perú generado por el servidor (/api/biomas): paleta conocida, así que cada color es una capa con nombre.
export async function loadGeoImage(info) {
  stopAll();
  progress(15, "Generando el mapa en el servidor…");
  const img = await loadImage(info.imageUrl);
  progress(50, "Leyendo las capas del mapa…");
  const { px, W, H } = pixelsOf(img, 480);
  const palette = knownPalette(info);
  IMG = { px, W, H, palette, classify: nearestClassifier(palette), kind: "geo",
          meta: { title: info.title, source: "biomas", alt: info.alt, context: info.context, box: info.box, places: info.places,
                  event: info.anio && info.region !== "mundo" ? PERU_EVENTS[info.anio] || null : null } };
  scan.cx = scan.cy = 0.5;
  A = analyzeImage();
  setLegend(info, false);
  showMedia("image", info.imageUrl, img.width, img.height);
  return finish();
}

// Serie 2001–2024: un mapa por año; cada paso musical es un año.
export async function loadGeoSeries(info) {
  stopAll(); IMG = null;
  // Hasta 4 años a la vez (los mapas mundiales se piden a NASA GIBS y tardan unos segundos cada uno)
  const N = info.frames.length, frames = new Array(N); let done = 0, next = 0;
  progress(5, `Generando los mapas de ${N} años…`);
  await Promise.all(Array.from({ length: 4 }, async () => {
    while (next < N) {
      const i = next++;
      frames[i] = pixelsOf(await loadImage(info.frames[i].url), 240);
      done++; progress(5 + 75 * done / N, `Mapas listos: ${done} de ${N} (último: ${info.frames[i].anio})…`);
    }
  }));
  const a = analyzeFrames(frames, knownPalette(info));
  const years = info.frames.map(f => f.anio);
  Object.assign(a, {
    mode: "video", scan: "years", duration: N, frameUrls: info.frames.map(f => f.url), points: null,
    AW: 480, AH: Math.round(480 * frames[0].H / frames[0].W),
    stepLabels: years.map(y => `año ${y}`), rowLabels: latRows(info.box),
    landmarks: years.map(y => info.region === "mundo" ? null : PERU_EVENTS[y] || null), stepEdges: years.map(y => [String(y), String(y)]),
    title: info.title, source: "biomas", alt: info.alt, context: info.context, box: info.box,
  });
  A = a;
  setLegend(info, true);
  showMedia("image", info.frames[0].url, frames[0].W, frames[0].H);
  return finish();
}

export async function loadFile(inputEl, userDescription) {
  const file = inputEl.files && inputEl.files[0];
  if (!file) throw new Error("No se seleccionó ningún archivo.");
  if (file.size > 300 * 1024 * 1024) throw new Error("El archivo supera 300 MB.");
  stopAll();
  const url = URL.createObjectURL(file);
  if (file.type.startsWith("video/")) {
    IMG = null;
    A = await analyzeVideo(url);
    A.scan = "time"; A.points = null;
    A.title = file.name; A.source = "upload"; A.context = null;
    A.alt = autoAlt(file.name, userDescription);
  } else if (file.type.startsWith("image/")) {
    progress(10, "Leyendo la imagen…");
    const img = await loadImage(url);
    progress(30, "Buscando los 4 colores dominantes (k-means)…");
    const { px, W, H } = pixelsOf(img, 480);
    const palette = autoPalette([px]);
    progress(60, "Clasificando píxeles y buscando destellos…");
    IMG = { px, W, H, palette, classify: nearestClassifier(palette), kind: "upload",
            meta: { title: file.name, source: "upload", alt: "", context: null } };
    scan.cx = scan.cy = 0.5;
    A = analyzeImage();
    IMG.meta.alt = A.alt = autoAlt(file.name, userDescription);
    showMedia("image", url, img.width, img.height);
  } else throw new Error("Formato no admitido. Sube una imagen (PNG, JPG, WebP…) o un video (MP4, WebM…).");
  return finish();
}

// Jukebox del Earth Information Center: video de NASA SVS servido por SONORA desde su caché (mismo origen,
// así el canvas puede leer los píxeles). Se analiza como un video: cuadros → 4 familias de color (k-means).
export async function loadEicVideo(item) {
  stopAll(); IMG = null;
  progress(3, "Abriendo la visualización del EIC…");
  A = await analyzeVideo(`api/eic/video/${item.id}`);
  A.scan = "time"; A.points = null;
  A.title = `${item.titulo} · ${item.fuente}`; A.source = "eic";
  A.context = item.contexto; A.alt = item.alt;
  return finish();
}

// Jukebox con hoja de cuadros (wwwroot/eic/*.webp): los mismos cuadros que se sacarían del video, ya preparados.
// Pesa 1–2 MB, se sirve desde SONORA y no depende de NASA SVS. Se analiza igual que un video.
let spriteUrls = [];
export async function loadEicSprite(item) {
  stopAll(); IMG = null;
  const h = item.hoja;
  progress(5, "Abriendo la visualización del EIC…");
  const sheet = await loadImage(h.url);
  spriteUrls.forEach(u => URL.revokeObjectURL(u)); spriteUrls = [];
  const N = h.cuadros, dur = h.duracion, frames = [];
  const c = document.createElement("canvas"); c.width = h.ancho; c.height = h.alto;
  const g = c.getContext("2d", { willReadFrequently: true });
  for (let i = 0; i < N; i++) {
    g.drawImage(sheet, (i % h.cols) * h.ancho, Math.floor(i / h.cols) * h.alto, h.ancho, h.alto, 0, 0, h.ancho, h.alto);
    frames.push(pixelsOf(c, 240, h.ancho, h.alto));
    spriteUrls.push(URL.createObjectURL(await new Promise(r => c.toBlob(r, "image/webp", 0.85))));
    progress(5 + 70 * (i + 1) / N, `Preparando cuadro ${i + 1} de ${N}…`);
  }
  progress(80, "Buscando los 4 colores dominantes (k-means)…");
  const palette = autoPalette(frames.filter((_, i) => i % Math.ceil(N / 8) === 0).map(f => f.px));
  const a = analyzeFrames(frames, palette);
  const times = Array.from({ length: N }, (_, i) => (i + 0.5) * dur / N);
  const fmt = dur < 60
    ? t => (Math.round(t * 10) / 10).toFixed(1).replace(".", ",")
    : t => `${Math.floor(t / 60)}:${String(Math.floor(t % 60)).padStart(2, "0")}`;
  Object.assign(a, {
    mode: "video", scan: "time", duration: dur, frameUrls: spriteUrls, points: null,
    AW: 480, AH: Math.round(480 * h.alto / h.ancho),
    stepLabels: times.map(t => `segundo ${fmt(t)}`),
    rowLabels: Array.from({ length: ROWS }, (_, r) => `franja ${r + 1} de ${ROWS} desde arriba`),
    landmarks: times.map(() => null),
    stepEdges: times.map((_, i) => [fmt(i * dur / N), fmt((i + 1) * dur / N)]),
    title: `${LANG === "en" && item.en ? item.en.titulo : item.titulo} · ${item.fuente}`, source: "eic", alt: item.alt, context: item.contexto,
  });
  A = a;
  showMedia("image", spriteUrls[0], h.ancho, h.alto);
  return finish();
}

// Graba una vuelta completa de la pieza (del primer al último paso) y la descarga como audio .webm.
// Es la «playlist» que el reto acepta como entregable: cada visualización con su archivo de sonido.
let recording = null;
// Video: la imagen o el video que suena + el barrido (canvas) + una franja con el título y el paso, a 30 cuadros/s.
function videoCompositor() {
  const W = 1280, H = Math.round(W * (A.AH || 270) / (A.AW || 480) / 2) * 2, BAR = 56;
  const c = document.createElement("canvas"); c.width = W; c.height = H + BAR;
  const g = c.getContext("2d");
  let alive = true;
  const paint = () => {
    if (!alive) return;
    g.fillStyle = "#05070b"; g.fillRect(0, 0, c.width, c.height);
    const media = !els.video.hidden ? els.video : els.img;
    try { if (media.videoWidth || media.naturalWidth) g.drawImage(media, 0, 0, W, H); } catch { /* cuadro aún no listo */ }
    try { g.drawImage(els.canvas, 0, 0, W, H); } catch { /* sin barrido */ }
    g.fillStyle = "#0b1220"; g.fillRect(0, H, W, BAR);
    g.fillStyle = "#e8eef7"; g.font = "600 22px system-ui, sans-serif"; g.textBaseline = "middle";
    g.fillText(String(A.title || "SONORA").slice(0, 90), 18, H + BAR / 2);
    g.fillStyle = "#7fd4ff"; g.font = "18px system-ui, sans-serif"; g.textAlign = "right";
    g.fillText(`${A.stepLabels?.[step] || ""} · SONORA`, W - 18, H + BAR / 2); g.textAlign = "left";
  };
  const iv = setInterval(paint, 1000 / 30); paint();
  return { stream: c.captureStream(30), stop: () => { alive = false; clearInterval(iv); } };
}
const firstSupported = list => list.find(t => MediaRecorder.isTypeSupported(t)) || "";
export async function recordPiece(fileName, withVideo = false) {
  if (!A) throw new Error("Primero elige qué escuchar.");
  if (recording) { recording.stop(); return false; } // segundo clic: detener antes de tiempo
  await ensureAudio();
  if (!window.MediaRecorder) throw new Error("Tu navegador no permite grabar (usa Chrome, Edge o Firefox).");
  pause(); step = 0; row = -1; draw();
  const raw = Tone.getContext().rawContext, dest = raw.createMediaStreamDestination();
  Tone.getDestination().connect(dest);
  // MP4 / M4A primero (se abren en cualquier reproductor); si no, WebM.
  const comp = withVideo ? videoCompositor() : null;
  const stream = comp ? new MediaStream([...comp.stream.getVideoTracks(), ...dest.stream.getAudioTracks()]) : dest.stream;
  const type = withVideo
    ? firstSupported(["video/mp4;codecs=avc1.42E01E,mp4a.40.2", "video/mp4", "video/webm;codecs=vp9,opus", "video/webm;codecs=vp8,opus", "video/webm"])
    : firstSupported(["audio/mp4;codecs=mp4a.40.2", "audio/mp4", "audio/webm;codecs=opus", "audio/webm", "audio/ogg"]);
  const opts = type ? { mimeType: type } : {};
  if (withVideo) opts.videoBitsPerSecond = 2_500_000;
  const rec = new MediaRecorder(stream, opts), chunks = [];
  rec.addEventListener("stop", () => comp?.stop());
  rec.ondataavailable = e => { if (e.data.size) chunks.push(e.data); };
  const done = new Promise(res => rec.onstop = res);
  rec.start(500); recording = rec;
  await play();
  // Termina cuando la reproducción da la vuelta (último paso → primero) o a los 15 minutos.
  const t0 = Date.now(); let last = 0;
  await new Promise(res => {
    const iv = setInterval(() => {
      const wrapped = last === A.steps - 1 && step === 0;
      last = step;
      if (wrapped || !playing || rec.state !== "recording" || Date.now() - t0 > 15 * 60 * 1000) { clearInterval(iv); res(); }
    }, 100);
  });
  pause();
  if (rec.state === "recording") rec.stop();
  await done;
  recording = null;
  try { Tone.getDestination().disconnect(dest); } catch { /* ya desconectado */ }
  comp?.stop();
  const mt = rec.mimeType || type || (withVideo ? "video/webm" : "audio/webm");
  const blob = new Blob(chunks, { type: mt });
  const ext = mt.includes("mp4") ? (withVideo ? ".mp4" : ".m4a") : mt.includes("ogg") ? ".ogg" : ".webm";
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = (fileName || "sonora") + ext;
  document.body.appendChild(a); a.click(); a.remove();
  setTimeout(() => URL.revokeObjectURL(a.href), 10000);
  return ext;
}
export function isRecording() { return !!recording; }

export function focusById(id) { document.getElementById(id)?.focus(); }
export function clickById(id) { document.getElementById(id)?.click(); }

// Un mapa de Leaflet que estaba oculto (en otra pestaña) necesita recalcular su tamaño al mostrarse.
export function refreshMaps() {
  setTimeout(() => {
    if (world) { world.map.invalidateSize(); worldMask(); }
    if (ctx) { ctx.map.invalidateSize(); ctx.boxKey = null; try { updateContext(); } catch { /* opcional */ } }
  }, 30);
}

function finish() {
  params = { ...MANUAL }; seq = compile(params); step = 0; row = -1; lastLandmark = null;
  draw(); progress(100, "Listo");
  return {
    mode: A.mode, title: A.title, source: A.source, steps: A.steps, rows: ROWS,
    layers: A.layers.map(l => ({ role: l.role, name: l.name, rgb: l.rgb, share: l.share })),
    col: A.col, stepLabels: A.stepLabels, landmarks: A.landmarks, context: A.context, alt: A.alt, duration: A.duration || 0,
    stepStart: A.stepEdges.map(e => e[0]), stepEnd: A.stepEdges.map(e => e[1]),
    scan: A.scan, box: A.box || null,
    event: (A.mode === "image" && IMG && IMG.meta && IMG.meta.event) || null, // suceso del año del mapa (p. ej. El Niño costero de 2023)
    points: A.points ? {
      count: A.points.length, warm: A.points.filter(p => p.warm).length, cool: A.points.filter(p => !p.warm).length,
      perStep: A.cols.map(c => A.points.filter(p => p.step === c).length),
    } : null,
  };
}

function showMedia(kind, url, w, h) {
  const { img, video, stage, canvas } = els;
  img.hidden = kind !== "image"; video.hidden = kind !== "video";
  if (kind === "image") { img.src = url; img.alt = A.alt || ""; img.dataset.frame = url; }
  stage.style.aspectRatio = `${w} / ${h}`;
  canvas.width = A.AW; canvas.height = A.AH;
}

function pixelsOf(src, targetW, srcW, srcH) {
  const w0 = srcW || src.naturalWidth || src.width, h0 = srcH || src.naturalHeight || src.height;
  const W = Math.min(targetW, w0), H = Math.max(1, Math.round(W * h0 / w0));
  const c = document.createElement("canvas"); c.width = W; c.height = H;
  const x = c.getContext("2d", { willReadFrequently: true }); x.drawImage(src, 0, 0, W, H);
  return { px: x.getImageData(0, 0, W, H).data, W, H };
}

// ======================================================================================
// Clasificadores de color
// ======================================================================================
// Paleta conocida (NASA): proyección cromática sobre cada color oficial; los grises no suenan.
function chromaClassifier(palette) {
  return (r, g, b) => {
    const m = (r + g + b) / 3, c0 = r - m, c1 = g - m, c2 = b - m, cn = Math.hypot(c0, c1, c2);
    if (cn < 6) return -1;
    let best = -1, bs = 0, bp = 0;
    for (let j = 0; j < palette.length; j++) {
      const u = palette[j].u, p = c0 * u[0] + c1 * u[1] + c2 * u[2];
      if (p <= 0) continue;
      const cos = p / cn, s = p * cos * cos;
      if (s > bs) { bs = s; best = j; bp = p; }
    }
    if (best < 0 || bp / cn < 0.75) return -1;
    return best + Math.min(1.5, bp / palette[best].norm) / 2; // parte entera = clase, decimal codifica la cantidad
  };
}

// ---------- Color perceptual (CIELAB) ----------
// En RGB, dos colores que el ojo ve distintos pueden estar "cerca" y al revés; en Lab la distancia (ΔE)
// se parece a lo que percibimos. Se usa para la paleta automática de videos e imágenes subidas.
const LIN = Float64Array.from({ length: 256 }, (_, i) => { const c = i / 255; return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4; });
function toLab(r, g, b) {
  const R = LIN[r | 0], G = LIN[g | 0], B = LIN[b | 0];
  const f = t => t > 0.008856 ? Math.cbrt(t) : 7.787 * t + 16 / 116;
  const x = f((0.4124 * R + 0.3576 * G + 0.1805 * B) / 0.95047), y = f(0.2126 * R + 0.7152 * G + 0.0722 * B), z = f((0.0193 * R + 0.1192 * G + 0.9505 * B) / 1.08883);
  return [116 * y - 16, 500 * (x - y), 200 * (y - z)];
}
const dLab = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]);

// Paleta automática en Lab: color más cercano, pero solo si el píxel está claramente más cerca de un color que
// del siguiente. Los píxeles de transición (degradados, bordes suavizados, compresión del video) quedan fuera
// en lugar de repartirse entre dos capas, que es lo que hacía que los colores "se mezclaran".
function labClassifier(palette) {
  const cache = new Map();
  return (r, g, b) => {
    const key = (r << 16) | (g << 8) | b;
    const hit = cache.get(key); if (hit !== undefined) return hit;
    let out = -1;
    const lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
    if (lum >= 18) {
      const L = toLab(r, g, b);
      let best = -1, d1 = 1e9, d2 = 1e9;
      for (let j = 0; j < palette.length; j++) {
        const d = dLab(L, palette[j].lab);
        if (d < d1) { d2 = d1; d1 = d; best = j; } else if (d < d2) d2 = d;
      }
      const D = palette[best].reach;
      if (d1 <= D && d2 - d1 >= 0.25 * d1 + 4) {
        const amt = (1 - d1 / D) * (0.35 + 0.65 * lum / 255);
        out = best + Math.min(1.5, amt) / 2;
      }
    }
    if (cache.size < 200000) cache.set(key, out);
    return out;
  };
}

// Paleta conocida (mapas del servidor) en RGB; la automática (con .lab) usa el clasificador perceptual.
function nearestClassifier(palette) {
  if (palette[0] && palette[0].lab) return labClassifier(palette);
  return (r, g, b) => {
    const lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
    if (lum < 18) return -1;
    let best = -1, bd = 1e9;
    for (let j = 0; j < palette.length; j++) {
      const p = palette[j].rgb, d = Math.hypot(r - p[0], g - p[1], b - p[2]);
      if (d < bd) { bd = d; best = j; }
    }
    const D = palette[best].reach; if (bd > D) return -1;
    const amt = (1 - bd / D) * (0.35 + 0.65 * lum / 255);
    return best + Math.min(1.5, amt) / 2;
  };
}

function mulberry32(a) { return () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; }

function autoPalette(pixelArrays) {
  // Muestra de píxeles no oscuros, en Lab (perceptual) y con su RGB para mostrar el color
  const S = [], RGB = []; const rnd = mulberry32(7);
  const per = Math.ceil(24000 / pixelArrays.length);
  for (const px of pixelArrays) {
    const n = px.length / 4, stride = Math.max(1, Math.floor(n / per));
    for (let i = 0; i < n; i += stride) {
      const r = px[i * 4], g = px[i * 4 + 1], b = px[i * 4 + 2];
      if (0.2126 * r + 0.7152 * g + 0.0722 * b >= 18) { S.push(toLab(r, g, b)); RGB.push([r, g, b]); }
    }
  }
  if (S.length < 8) throw new Error("La imagen es casi completamente negra; no hay colores que sonificar.");
  const K = 4, MIN_DE = 22; // ΔE mínimo entre dos capas: por debajo, el oído y la vista las confunden
  const pickFar = C => { // k-means++: muestra lejos de los centros actuales (ponderada por distancia²)
    const w = S.map(s => Math.min(...C.map(c => dLab(s, c) ** 2)));
    let t = rnd() * w.reduce((a, b) => a + b, 0), i = 0;
    while (i < S.length - 1 && (t -= w[i]) > 0) i++;
    return S[i].slice();
  };
  const C = [S[Math.floor(rnd() * S.length)].slice()];
  while (C.length < K) C.push(pickFar(C));
  const assign = new Int32Array(S.length);
  const kmeans = iters => {
    for (let it = 0; it < iters; it++) {
      const sum = C.map(() => [0, 0, 0, 0]);
      S.forEach((s, i) => { let bj = 0, bd = 1e12; C.forEach((c, j) => { const d = dLab(s, c); if (d < bd) { bd = d; bj = j; } }); assign[i] = bj; const q = sum[bj]; q[0] += s[0]; q[1] += s[1]; q[2] += s[2]; q[3]++; });
      sum.forEach((q, j) => { if (q[3]) C[j] = [q[0] / q[3], q[1] / q[3], q[2] / q[3]]; else C[j] = pickFar(C.filter((_, k) => k !== j)); });
    }
  };
  kmeans(15);
  // Si dos colores quedaron casi iguales (típico en videos con degradados), se fusionan y el hueco se
  // vuelve a sembrar con el color más distinto que haya: así las 4 capas son colores claramente diferentes.
  for (let round = 0; round < 4; round++) {
    let pair = null;
    for (let a = 0; a < K && !pair; a++) for (let b = a + 1; b < K; b++) if (dLab(C[a], C[b]) < MIN_DE) { pair = [a, b]; break; }
    if (!pair) break;
    const others = C.filter((_, k) => k !== pair[1]);
    C[pair[1]] = pickFar(others);
    kmeans(8);
  }
  const stats = C.map((c, j) => {
    let n = 0, dist = 0; const m = [0, 0, 0];
    S.forEach((s, i) => { if (assign[i] === j) { n++; dist += dLab(s, c); m[0] += RGB[i][0]; m[1] += RGB[i][1]; m[2] += RGB[i][2]; } });
    return { rgb: m.map(v => Math.round(n ? v / n : 0)), lab: c, share: n / S.length, reach: Math.min(45, Math.max(12, 2.2 * (n ? dist / n : 12))) };
  });
  // Roles por presencia: la más extensa es la armonía de fondo; luego melodía, ritmo y bajo.
  const order = stats.map((s, j) => j).sort((a, b) => stats[b].share - stats[a].share);
  const roleOrder = ["pad", "melody", "rhythm", "bass"];
  const pal = order.map((j, k) => ({ role: roleOrder[k], ...stats[j] }));
  // Nombres de color únicos
  const used = {};
  pal.forEach(p => { let n = colorName(p.rgb); used[n] = (used[n] || 0) + 1; p.name = used[n] > 1 ? `${n} ${used[n]}` : n; });
  // Orden estable por rol
  return ROLES.map(r => pal.find(p => p.role === r));
}

function colorName([r, g, b]) {
  const max = Math.max(r, g, b) / 255, min = Math.min(r, g, b) / 255, l = (max + min) / 2, d = max - min;
  const s = d === 0 ? 0 : d / (1 - Math.abs(2 * l - 1));
  if (s < 0.15) return l > 0.8 ? "Blanco" : l > 0.55 ? "Gris claro" : l > 0.25 ? "Gris" : "Gris oscuro";
  let h; const R = r / 255, G = g / 255, B = b / 255;
  if (max === R) h = 60 * (((G - B) / d) % 6); else if (max === G) h = 60 * ((B - R) / d + 2); else h = 60 * ((R - G) / d + 4);
  if (h < 0) h += 360;
  const base = h < 15 ? "Rojo" : h < 40 ? "Naranja" : h < 65 ? "Amarillo" : h < 160 ? "Verde" : h < 195 ? "Cian" : h < 255 ? "Azul" : h < 290 ? "Violeta" : h < 335 ? "Magenta" : "Rojo";
  return base + (l < 0.3 ? " oscuro" : l > 0.72 ? " claro" : "");
}

// ======================================================================================
// Análisis: imagen (pasos = columnas) y video (pasos = fotogramas)
// ======================================================================================
// ---------- Recorridos: cada píxel → (paso, fila, fracción dentro del paso, posición para el tono) ----------
const STEPS = 32;
function makeBins(W, H, fn, extra) {
  const N = W * H, S = new Int16Array(N), R = new Int8Array(N), F = new Float32Array(N), P = new Float32Array(N);
  for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
    const p = y * W + x, [s, r, f, q] = fn(x, y);
    S[p] = s; R[p] = r; F[p] = f; P[p] = q;
  }
  return { steps: STEPS, S, R, F, P, ...extra };
}
// Lineal: columnas de izquierda a derecha, franjas de arriba abajo; más arriba = más agudo.
function linearBinner(W, H) {
  return makeBins(W, H, (x, y) => {
    const u = x * STEPS / W, s = Math.min(STEPS - 1, Math.floor(u));
    return [s, Math.min(ROWS - 1, Math.floor(y * ROWS / H)), u - s, 1 - (y + 0.5) / H];
  }, { kind: "linear" });
}
// Radar (como Caldwell 73): una línea gira en sentido horario desde las 12; anillos desde el centro; más lejos = más agudo.
function radarBinner(W, H) {
  const cx = scan.cx * W, cy = scan.cy * H;
  const rmax = Math.max(...[[0, 0], [W, 0], [0, H], [W, H]].map(([a, b]) => Math.hypot(a - cx, b - cy)));
  return makeBins(W, H, (x, y) => {
    const dx = x + 0.5 - cx, dy = y + 0.5 - cy;
    let ang = Math.atan2(dx, -dy); if (ang < 0) ang += 2 * Math.PI;
    const u = ang / (2 * Math.PI) * STEPS, s = Math.min(STEPS - 1, Math.floor(u)), r = Math.hypot(dx, dy) / rmax;
    return [s, Math.min(ROWS - 1, Math.floor(r * ROWS)), u - s, r];
  }, { kind: "radar", cx, cy, rmax });
}

function buildImageAnalysis(px, W, H, palette, classify, bin) {
  const COLS = bin.steps, K = palette.length;
  const classMap = new Int8Array(W * H).fill(-1), amtMap = new Float32Array(W * H);
  const sum = new Float32Array(COLS * ROWS * K), cnt = new Float32Array(COLS * ROWS);
  for (let p = 0; p < W * H; p++) {
    const i = p * 4, q = bin.S[p] * ROWS + bin.R[p];
    cnt[q]++;
    const v = classify(px[i], px[i + 1], px[i + 2]);
    if (v < 0) continue;
    const j = Math.floor(v), amt = (v - j) * 2;
    classMap[p] = j; amtMap[p] = amt;
    sum[q * K + j] += amt;
  }
  const a = finalize(COLS, sum, cnt, palette);
  Object.assign(a, { mode: "image", AW: W, AH: H, classMap, amtMap, bin, cols: [...Array(COLS).keys()] });
  a.classLayer = buildClassLayer(a, W, H);
  // Proporción de cada capa en la imagen (para la descripción)
  const tot = new Float64Array(K); let all = 0;
  classMap.forEach(j => { if (j >= 0) { tot[j]++; all++; } });
  a.layers.forEach((l, j) => { l.share = all ? Math.round(tot[j] / classMap.length * 1000) / 1000 : 0; });
  return a;
}

// ---------- Destellos: puntos brillantes y aislados (estrellas, rayos, incendios, luces de ciudades) ----------
function detectPoints(px, W, H, bin) {
  const N = W * H, L = new Float32Array(N);
  for (let i = 0; i < N; i++) L[i] = 0.2126 * px[i * 4] + 0.7152 * px[i * 4 + 1] + 0.0722 * px[i * 4 + 2];
  // Imagen integral para el brillo medio del entorno
  const IW = W + 1, I = new Float64Array(IW * (H + 1));
  for (let y = 0; y < H; y++) { let acc = 0; for (let x = 0; x < W; x++) { acc += L[y * W + x]; I[(y + 1) * IW + x + 1] = I[y * IW + x + 1] + acc; } }
  const R = 7, box = (x0, y0, x1, y1) => I[y1 * IW + x1] - I[y0 * IW + x1] - I[y1 * IW + x0] + I[y0 * IW + x0];
  const cand = [];
  for (let y = 2; y < H - 2; y++) for (let x = 2; x < W - 2; x++) {
    const i = y * W + x, l = L[i];
    if (l < 100) continue;
    let isMax = true;
    for (let dy = -2; dy <= 2 && isMax; dy++) for (let dx = -2; dx <= 2; dx++) if (L[i + dy * W + dx] > l) { isMax = false; break; }
    if (!isMax) continue;
    const x0 = Math.max(0, x - R), y0 = Math.max(0, y - R), x1 = Math.min(W, x + R + 1), y1 = Math.min(H, y + R + 1);
    const mean = box(x0, y0, x1, y1) / ((x1 - x0) * (y1 - y0)), prom = l - mean;
    if (prom < 40) continue;
    let big = 0;
    for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) if (L[i + dy * W + dx] > mean + prom * 0.5) big++;
    if (big > 12) continue; // es una mancha o un filamento, no un punto
    cand.push({ x, y, i, prom });
  }
  cand.sort((p, q) => q.prom - p.prom);
  const taken = new Uint8Array(N), pts = [];
  for (const c of cand) {
    if (taken[c.i]) continue;
    for (let dy = -3; dy <= 3; dy++) for (let dx = -3; dx <= 3; dx++) { const yy = c.y + dy, xx = c.x + dx; if (yy >= 0 && yy < H && xx >= 0 && xx < W) taken[yy * W + xx] = 1; }
    pts.push(c); if (pts.length >= 400) break;
  }
  const maxP = pts.length ? pts[0].prom : 1;
  return pts.map(p => {
    const r = px[p.i * 4], g = px[p.i * 4 + 1], b = px[p.i * 4 + 2];
    return { x: p.x, y: p.y, step: bin.S[p.i], row: bin.R[p.i], frac: bin.F[p.i], pos: bin.P[p.i], warm: isWarm(r, g, b), vel: Math.min(1, 0.3 + 0.7 * p.prom / maxP) };
  });
}

// Rojo, naranja, amarillo → cálido (marimba); azul, blanco y el resto → frío (glockenspiel), como en Caldwell 73.
function isWarm(r, g, b) {
  const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min;
  if (max === 0 || d / max < 0.25) return false;
  let h; if (max === r) h = 60 * (((g - b) / d) % 6); else if (max === g) h = 60 * ((b - r) / d + 2); else h = 60 * ((r - g) / d + 4);
  if (h < 0) h += 360;
  return h < 70 || h >= 320;
}

// sum[(step*ROWS+row)*K+j] y cnt[step*ROWS+row] → medias por paso y por celda, normalizadas por capa
function finalize(steps, sum, cnt, palette) {
  const K = palette.length, col = {}, cell = new Float32Array(steps * ROWS * K);
  palette.forEach((p, j) => {
    const v = new Float64Array(steps); let cmax = 0;
    for (let c = 0; c < steps; c++) {
      let s = 0, n = 0;
      for (let r = 0; r < ROWS; r++) {
        const q = c * ROWS + r, m = cnt[q] ? sum[q * K + j] / cnt[q] : 0;
        cell[q * K + j] = m; cmax = Math.max(cmax, m); s += sum[q * K + j]; n += cnt[q];
      }
      v[c] = n ? s / n : 0;
    }
    const max = Math.max(...v) || 1;
    col[p.role] = Array.from(v, x => Math.round(x / max * 1e6) / 1e6); // 6 decimales: los mismos que recibe el optimizador
    for (let q = 0; q < steps * ROWS; q++) cell[q * K + j] = cmax ? cell[q * K + j] / cmax : 0;
  });
  return { steps, col, cell, layers: palette.map(p => ({ role: p.role, name: p.name, rgb: p.rgb, share: p.share || 0 })) };
}

function buildClassLayer(a, W, H) {
  const c = document.createElement("canvas"); c.width = W; c.height = H;
  const x = c.getContext("2d"), id = x.createImageData(W, H);
  for (let p = 0; p < W * H; p++) {
    const j = a.classMap[p], i = p * 4;
    if (j < 0) { id.data[i + 3] = 235; continue; }
    const [r, g, b] = a.layers[j].rgb, k = Math.min(1, a.amtMap[p]);
    id.data[i] = r * k; id.data[i + 1] = g * k; id.data[i + 2] = b * k; id.data[i + 3] = 255;
  }
  x.putImageData(id, 0, 0); return c;
}

const once = (el, ev) => new Promise((res, rej) => {
  const ok = () => { cleanup(); res(); }, bad = () => { cleanup(); rej(new Error("El navegador no pudo leer este video (prueba MP4 H.264 o WebM).")); };
  const cleanup = () => { el.removeEventListener(ev, ok); el.removeEventListener("error", bad); };
  el.addEventListener(ev, ok); el.addEventListener("error", bad);
});

async function analyzeVideo(url) {
  const v = els.video;
  v.hidden = false; els.img.hidden = true; v.muted = true; v.playsInline = true; v.preload = "auto";
  const loaded = once(v, "loadeddata"); v.src = url; await loaded;
  // Algunos WebM (p. ej. grabados con MediaRecorder) no traen la duración: forzar al navegador a calcularla.
  if (!isFinite(v.duration) || v.duration < 0.5) {
    const known = new Promise(res => {
      const h = () => { if (isFinite(v.duration) && v.duration > 0) { v.removeEventListener("durationchange", h); v.removeEventListener("timeupdate", h); res(); } };
      v.addEventListener("durationchange", h); v.addEventListener("timeupdate", h);
      setTimeout(res, 4000);
    });
    v.currentTime = 1e7; await known;
    const back = once(v, "seeked"); v.currentTime = 0; await back;
  }
  const dur = v.duration;
  if (!isFinite(dur) || dur <= 0) throw new Error("No se pudo determinar la duración del video.");
  const N = Math.max(16, Math.min(64, Math.round(dur * 2)));
  const times = Array.from({ length: N }, (_, i) => (i + 0.5) * dur / N);
  const frames = [];
  for (let i = 0; i < N; i++) {
    const s = once(v, "seeked"); v.currentTime = times[i]; await s;
    frames.push(pixelsOf(v, 240, v.videoWidth, v.videoHeight));
    progress(5 + 70 * (i + 1) / N, `Extrayendo fotograma ${i + 1} de ${N}…`);
  }
  progress(80, "Buscando los 4 colores dominantes (k-means)…");
  const palette = autoPalette(frames.filter((_, i) => i % Math.ceil(N / 8) === 0).map(f => f.px));
  const a = analyzeFrames(frames, palette);
  const fmt = dur < 60
    ? t => (Math.round(t * 10) / 10).toFixed(1).replace(".", ",")
    : t => `${Math.floor(t / 60)}:${String(Math.floor(t % 60)).padStart(2, "0")}`;
  Object.assign(a, {
    mode: "video", duration: dur, times, AW: 480, AH: Math.round(480 * v.videoHeight / v.videoWidth),
    stepLabels: times.map(t => `segundo ${fmt(t)}`),
    rowLabels: Array.from({ length: ROWS }, (_, r) => `franja ${r + 1} de ${ROWS} desde arriba`),
    landmarks: times.map(() => null),
    stepEdges: times.map((_, i) => [fmt(i * dur / N), fmt((i + 1) * dur / N)]),
  });
  A = a; showMedia("video", url, v.videoWidth, v.videoHeight);
  v.currentTime = times[0];
  return a;
}

// Fotogramas (de un video o de una serie de mapas) → intensidad de cada capa por paso y franja.
function analyzeFrames(frames, palette) {
  const N = frames.length, classify = nearestClassifier(palette), K = palette.length;
  const sum = new Float32Array(N * ROWS * K), cnt = new Float32Array(N * ROWS), tot = new Float64Array(K); let allPx = 0;
  frames.forEach(({ px, W, H }, f) => {
    for (let y = 0; y < H; y++) {
      const rr = Math.min(ROWS - 1, Math.floor(y * ROWS / H));
      for (let x = 0; x < W; x++) {
        const i = (y * W + x) * 4; cnt[f * ROWS + rr]++; allPx++;
        const val = classify(px[i], px[i + 1], px[i + 2]);
        if (val < 0) continue;
        const j = Math.floor(val); sum[(f * ROWS + rr) * K + j] += (val - j) * 2; tot[j]++;
      }
    }
  });
  const a = finalize(N, sum, cnt, palette);
  a.layers.forEach((l, j) => { l.share = Math.round(tot[j] / allPx * 1000) / 1000; });
  return a;
}

function autoAlt(name, userDescription) {
  const parts = A.layers.slice().sort((a, b) => b.share - a.share).map(l => `${l.name.toLowerCase()} (${Math.round(l.share * 100)} %)`);
  const kind = A.mode === "video" ? `Video de ${Math.round(A.duration)} segundos subido` : "Imagen subida";
  const base = `${kind} por la persona usuaria: ${name}. Colores dominantes detectados: ${parts.join(", ")}.`;
  return userDescription && userDescription.trim() ? `${userDescription.trim()} (${base})` : base;
}

// ======================================================================================
// Motor musical (idéntico a MusicEngine en C#)
// ======================================================================================
function ema(x, a) {
  const n = x.length, out = new Array(n); let s = x[0];
  for (let pass = 0; pass < 2; pass++) for (let i = 0; i < n; i++) { s = a * s + (1 - a) * x[i]; if (pass === 1) out[i] = s; }
  return out;
}
function compile(p) {
  const sm = ema(A.col.melody, p.smoothMel), sr = ema(A.col.rhythm, p.smoothRhy);
  return {
    mi: sm.map(v => v > p.melThr ? Math.min(SCALE.length - 1, p.pitchLow + Math.round(Math.pow(v, p.gamma) * (p.pitchSpan - 1))) : -1),
    hits: sr.map(v => v > p.rhy4 ? 4 : v > p.rhy2 ? 2 : v > p.rhy1 ? 1 : 0),
    bass: A.col.bass.map(v => v > p.bassThr),
  };
}

export function features() { return { steps: A.steps, layers: { melody: A.col.melody, rhythm: A.col.rhythm, bass: A.col.bass } }; }
export function setParams(p) { params = { ...p }; if (A) { seq = compile(params); report(playing); } }
export function verify(pareto) {
  let bad = 0;
  for (const s of [pareto.manual, ...pareto.front]) {
    const q = compile(s.params);
    if (q.mi.some((v, c) => v !== s.melody[c]) || q.hits.some((v, c) => v !== s.hits[c])) bad++;
  }
  return bad;
}

function chordAt(c) { return CHORDS[Math.min(CHORDS.length - 1, Math.floor(c * CHORDS.length / A.steps))]; }

// Música de un paso completo (r = -1) o de una celda concreta en modo exploración (r ≥ 0).
function musicFor(c, r) {
  const chord = chordAt(c), v = {};
  if (r < 0) {
    ROLES.forEach(k => v[k] = A.col[k][c]);
    return { v, chord, mi: seq.mi[c], hits: seq.hits[c], bass: seq.bass[c] };
  }
  const K = A.layers.length;
  A.layers.forEach((l, j) => v[l.role] = A.cell[(c * ROWS + r) * K + j]);
  const p = params;
  return {
    v, chord,
    mi: v.melody > p.melThr ? Math.min(SCALE.length - 1, p.pitchLow + Math.round(Math.pow(v.melody, p.gamma) * (p.pitchSpan - 1))) : -1,
    hits: v.rhythm > p.rhy4 ? 4 : v.rhythm > p.rhy2 ? 2 : v.rhythm > p.rhy1 ? 1 : 0,
    bass: v.bass > p.bassThr,
  };
}

// ======================================================================================
// Audio (Tone.js)
// ======================================================================================
function buildAudio() {
  const reverb = new Tone.Reverb({ decay: 5, wet: 0.35 }).toDestination();
  const ch = {}; [...ROLES, "points"].forEach(k => { ch[k] = new Tone.Channel({ mute: muted[k] }).connect(reverb); });
  // Destellos: marimba (cálidos) y glockenspiel (fríos), sintetizados con FM
  const marimba = new Tone.PolySynth(Tone.FMSynth, { harmonicity: 3.01, modulationIndex: 2.2, oscillator: { type: "sine" },
    envelope: { attack: 0.002, decay: 0.5, sustain: 0, release: 0.3 }, modulation: { type: "sine" },
    modulationEnvelope: { attack: 0.002, decay: 0.12, sustain: 0, release: 0.1 } }).connect(ch.points);
  marimba.volume.value = -9;
  const glock = new Tone.PolySynth(Tone.FMSynth, { harmonicity: 7.5, modulationIndex: 5, oscillator: { type: "sine" },
    envelope: { attack: 0.001, decay: 1.3, sustain: 0, release: 1 }, modulation: { type: "sine" },
    modulationEnvelope: { attack: 0.001, decay: 0.35, sustain: 0, release: 0.2 } }).connect(ch.points);
  glock.volume.value = -16;
  const melody = new Tone.Synth({ oscillator: { type: "triangle" }, envelope: { attack: 0.01, decay: 0.25, sustain: 0.25, release: 0.8 } }).connect(ch.melody);
  melody.volume.value = -6;
  const nf = new Tone.Filter(2600, "bandpass").connect(ch.rhythm);
  const noise = new Tone.NoiseSynth({ noise: { type: "pink" }, envelope: { attack: 0.001, decay: 0.09, sustain: 0 } }).connect(nf);
  const kick = new Tone.MembraneSynth({ octaves: 4, envelope: { attack: 0.001, decay: 0.35, sustain: 0 } }).connect(ch.rhythm);
  kick.volume.value = -8;
  const bass = new Tone.MonoSynth({ oscillator: { type: "sawtooth" }, filter: { Q: 2, type: "lowpass" },
    envelope: { attack: 0.02, decay: 0.3, sustain: 0.5, release: 0.6 },
    filterEnvelope: { attack: 0.01, decay: 0.25, sustain: 0.3, baseFrequency: 70, octaves: 2.6 } }).connect(ch.bass);
  bass.volume.value = -10;
  const padVol = new Tone.Volume(-30).connect(ch.pad);
  const pad = new Tone.PolySynth(Tone.Synth, { oscillator: { type: "sine" }, envelope: { attack: 1.2, decay: 0.5, sustain: 0.8, release: 2.5 } }).connect(padVol);
  pad.volume.value = -8;
  // Campanita de cambio de año (serie 2001–2024): va directa a la reverb, no se silencia con las capas.
  const bell = new Tone.FMSynth({ harmonicity: 7.5, modulationIndex: 4, envelope: { attack: 0.001, decay: 0.6, sustain: 0, release: 0.4 },
    modulationEnvelope: { attack: 0.001, decay: 0.2, sustain: 0, release: 0.1 } }).connect(reverb);
  bell.volume.value = -14;
  return { ch, melody, noise, kick, bass, pad, padVol, marimba, glock, bell };
}

// Destellos que suenan en un paso (r = -1) o en una celda de exploración (r ≥ 0).
function pointsFor(c, r) {
  if (!A.points) return [];
  return r < 0 ? A.pointsByStep[c] : pick(A.points.filter(p => p.step === c && p.row === r));
}
const pointNote = p => {
  const n = SCALE[Math.round(p.pos * (SCALE.length - 1))];
  return p.warm ? n : Tone.Frequency(n).transpose(12).toNote(); // glockenspiel una octava arriba
};
async function ensureAudio() { if (!audio) { await Tone.start(); audio = buildAudio(); Tone.Destination.volume.value = masterDb; } }

function playStep(c, r, time) {
  const m = musicFor(c, r), a = audio, stepDur = Tone.Time("8n").toSeconds();
  if (m.mi >= 0) a.melody.triggerAttackRelease(SCALE[m.mi], m.v.melody > 0.7 ? "4n" : "8n", time, 0.25 + 0.75 * m.v.melody);
  for (let h = 0; h < m.hits; h++) a.noise.triggerAttackRelease("32n", time + h * stepDur / m.hits, 0.25 + 0.6 * m.v.rhythm);
  if (m.v.rhythm > 0.6 && c % 2 === 0) a.kick.triggerAttackRelease("C1", "8n", time, m.v.rhythm);
  if (m.bass) a.bass.triggerAttackRelease(m.v.bass > 0.6 && c % 2 ? m.chord.fifth : m.chord.root, "8n", time, 0.3 + 0.7 * m.v.bass);
  a.padVol.volume.rampTo(Tone.gainToDb(0.03 + 0.5 * m.v.pad), stepDur * 0.9, time);
  if (r >= 0 || c % Math.max(1, Math.round(A.steps / CHORDS.length)) === 0) a.pad.triggerAttackRelease(m.chord.notes, r >= 0 ? "2n" : "1m", time, 0.6);
  // Cada destello suena cuando el barrido pasa por él (su fracción dentro del paso)
  m.pts = pointsFor(c, r);
  for (const p of m.pts) (p.warm ? a.marimba : a.glock).triggerAttackRelease(pointNote(p), "16n", time + p.frac * stepDur * 0.95, p.vel);
  return m;
}

// ---------- Serie año por año ----------
// Cada año dura unos segundos (ajustable) y suena como una frase, no como una nota suelta, para que el oído
// alcance a fijarlo. Al empezar cada año suena una campanita. La melodía llega a la nota del año desde abajo
// si la capa creció respecto al año anterior y desde arriba si bajó; si casi no cambió, ronda la nota.
let yearSec = 4;
export function setYearSeconds(s) { yearSec = Math.max(1, Math.min(10, Number(s) || 4)); }
const isYears = () => !!A && A.scan === "years";
const deltaAt = (c, k) => c > 0 ? A.col[k][c] - A.col[k][c - 1] : 0;
const ticksPerYear = () => Math.max(1, Math.round(yearSec / Tone.Time("8n").toSeconds()));
const MOTIF = { up: [-2, -1, 0, 0], down: [2, 1, 0, 0], flat: [0, 1, 0, -1] };

function playYearTick(c, sub, n, time) {
  const m = musicFor(c, -1), a = audio, t8 = Tone.Time("8n").toSeconds();
  if (sub === 0) {
    a.bell.triggerAttackRelease("C7", "16n", time, 0.7);
    a.padVol.volume.rampTo(Tone.gainToDb(0.03 + 0.5 * m.v.pad), t8, time);
    a.pad.triggerAttackRelease(m.chord.notes, Math.max(t8, n * t8 * 0.95), time, 0.6);
  }
  // Melodía: una nota por negra (cada 2 corcheas), con el motivo según la tendencia
  if (m.mi >= 0 && sub % 2 === 0) {
    const d = deltaAt(c, "melody"), shape = c === 0 ? MOTIF.flat : d > 0.01 ? MOTIF.up : d < -0.01 ? MOTIF.down : MOTIF.flat;
    const q = (sub / 2) % 4, idx = Math.max(0, Math.min(SCALE.length - 1, m.mi + shape[q]));
    a.melody.triggerAttackRelease(SCALE[idx], "8n", time, (q === 0 ? 0.35 : 0.2) + 0.65 * m.v.melody);
  }
  for (let h = 0; h < m.hits; h++) a.noise.triggerAttackRelease("32n", time + h * t8 / m.hits, 0.25 + 0.6 * m.v.rhythm);
  if (m.v.rhythm > 0.6 && sub % 4 === 0) a.kick.triggerAttackRelease("C1", "8n", time, m.v.rhythm);
  if (m.bass && sub % 4 === 0) a.bass.triggerAttackRelease(m.chord.root, "4n", time, 0.3 + 0.7 * m.v.bass);
  if (m.bass && sub % 4 === 2 && m.v.bass > 0.6) a.bass.triggerAttackRelease(m.chord.fifth, "4n", time, 0.25 + 0.5 * m.v.bass);
  return m;
}

// Vibración: el bajo es un pulso largo y cada golpe de ritmo un pulso corto (Android/Chrome).
function vibrate(m) {
  if (!opts.haptics || !navigator.vibrate) return;
  const pat = [];
  if (m.bass) pat.push(Math.round(40 + 60 * m.v.bass), 40);
  for (let h = 0; h < m.hits; h++) pat.push(22, 38);
  if (m.pts && m.pts.length && !m.hits) for (let k = 0; k < Math.min(3, m.pts.length); k++) pat.push(10, 30); // destellos: toques muy cortos
  if (m.mi >= 0 && !pat.length) pat.push(12);
  if (pat.length) navigator.vibrate(pat);
}

// Se llama desde un clic (bienvenida) para que el navegador permita el audio aunque la música empiece más tarde.
export async function unlockAudio() { await ensureAudio(); }

export async function toggle() { if (playing) pause(); else await play(); }

export async function play() {
  if (!A || playing) return;
  await ensureAudio();
  Tone.Transport.cancel();
  if (row >= 0) row = -1;
  let c = step, sub = 0, n = 1;
  Tone.Transport.scheduleRepeat(time => {
    if (isYears()) {
      if (sub === 0) n = ticksPerYear();
      const cur = c, first = sub === 0;
      const m = playYearTick(cur, sub, n, time);
      if (++sub >= n) { sub = 0; c = (c + 1) % A.steps; }
      if (first) {
        const delay = Math.max(0, (time - Tone.now()) * 1000);
        setTimeout(() => { if (!playing) return; step = cur; draw(); vibrate(m); report(true); }, delay);
      }
      return;
    }
    const cur = c; c = (c + 1) % A.steps;
    const m = playStep(cur, -1, time);
    // setTimeout (no Tone.Draw/requestAnimationFrame): sigue funcionando con la pestaña en segundo plano
    // o la pantalla apagada, algo habitual al usar un lector de pantalla.
    const delay = Math.max(0, (time - Tone.now()) * 1000);
    setTimeout(() => { if (!playing) return; step = cur; draw(); vibrate(m); report(true); }, delay);
  }, "8n");
  Tone.Transport.start(); playing = true; report(true);
}

export function pause() {
  if (!playing) return;
  Tone.Transport.pause(); Tone.Transport.cancel(); playing = false; report(false);
}

function stopAll() { pause(); if (opts.haptics && navigator.vibrate) navigator.vibrate(0); }

export async function audition(c, r) {
  if (!A) return;
  if (playing) pause();
  step = Math.max(0, Math.min(A.steps - 1, c)); row = r == null ? row : r;
  draw();
  await ensureAudio();
  const t0 = Tone.now() + 0.05;
  // Serie de años: al moverse con las flechas suena el primer compás del año (campanita + motivo).
  const m = isYears() && row < 0
    ? (() => { const t8 = Tone.Time("8n").toSeconds(), n = Math.min(8, ticksPerYear()); let r0; for (let s = 0; s < n; s++) { const x = playYearTick(step, s, n, t0 + s * t8); r0 ??= x; } return r0; })()
    : playStep(step, row, t0);
  vibrate(m); report(false);
}

export function seek(c) { return audition(c, -1); }
// Reproduce de forma continua desde el paso c (lo usa el asistente: «haz sonar el Sahara»).
export async function playFrom(c) {
  if (!A) return;
  if (playing) pause();
  step = Math.max(0, Math.min(A.steps - 1, c)); row = -1;
  draw(); await play();
}
// Volumen general 0–100 (80 = 0 dB); la atenuación del asistente se aplica sobre este valor.
let masterDb = 0;
export function setVolume(pct) {
  masterDb = pct <= 0 ? -60 : 20 * Math.log10(pct / 80);
  if (audio) Tone.Destination.volume.rampTo(masterDb, 0.15);
}
export function setMute(role, isMuted) { muted[role] = isMuted; if (audio) audio.ch[role].mute = isMuted; }
export function scales() {
  return Object.entries(SCALES).map(([id, sc]) => ({
    id, name: sc.name, mood: sc.mood,
    notes: buildScale(sc.iv).slice(0, sc.iv.length + 1).map(esNote).join(" "),
    chords: sc.prog.map(([x, q]) => chord(x, q).name).join(" · "),
  }));
}
export function setScale(id) {
  const sc = SCALES[id];
  if (!sc) return false;
  SCALE = buildScale(sc.iv);
  CHORDS = sc.prog.map(([x, q]) => chord(x, q));
  return true;
}
export function setTempo(bpm) { Tone.Transport.bpm.value = bpm; }
export function setShowClass(on) { opts.showClass = on; draw(); }
export function setHaptics(on) { opts.haptics = on; }
export function hapticsSupported() { return typeof navigator.vibrate === "function"; }
export function testHaptics() { if (navigator.vibrate) navigator.vibrate([80, 60, 22, 38, 22, 38, 22]); }

function stepInfo(c, r, isPlaying, entered) {
  const m = musicFor(c, r);
  const bassNote = m.bass ? esNote(m.v.bass > 0.6 && c % 2 ? m.chord.fifth : m.chord.root) : null;
  const values = {}; ROLES.forEach(k => values[k] = Math.round(m.v[k] * 1000) / 1000);
  const pts = pointsFor(c, r);
  // Cambio respecto al año anterior (solo en la serie año por año)
  let delta = null;
  if (isYears() && r < 0 && c > 0) { delta = {}; ROLES.forEach(k => delta[k] = Math.round(deltaAt(c, k) * 1000) / 1000); }
  return {
    delta,
    step: c, row: r, playing: isPlaying, stepLabel: A.stepLabels[c], rowLabel: r >= 0 ? A.rowLabels[r] : null,
    landmark: A.landmarks[c], landmarkEntered: entered, values, note: m.mi >= 0 ? esNote(SCALE[m.mi]) : null,
    hits: m.hits, bass: m.bass, bassNote, chord: m.chord.name,
    points: pts.length, pointsWarm: pts.filter(p => p.warm).length,
  };
}

function report(isPlaying) {
  if (!A || !dn) return;
  const lm = A.landmarks[step], entered = !!lm && lm !== lastLandmark; lastLandmark = lm;
  dn.invokeMethodAsync("OnStep", stepInfo(step, row, isPlaying, entered));
}

export function allSteps() { return A ? Array.from({ length: A.steps }, (_, c) => stepInfo(c, -1, false, false)) : []; }

// ======================================================================================
// Dibujo
// ======================================================================================
function draw() {
  if (!A || !els) return;
  try { updateContext(); } catch { /* el mapa de contexto es opcional */ }
  try { updateLegend(); } catch { /* la leyenda es opcional */ }
  const { g, canvas, video } = els, W = canvas.width, H = canvas.height;
  g.clearRect(0, 0, W, H);
  if (A.mode === "image" && A.scan === "radar") {
    if (opts.showClass && A.classLayer) g.drawImage(A.classLayer, 0, 0);
    const { cx, cy, rmax } = A.bin, da = 2 * Math.PI / A.steps, a0 = -Math.PI / 2 + step * da, a1 = a0 + da;
    // Oscurece todo menos el sector actual (regla par-impar: rectángulo menos cuña)
    g.fillStyle = "rgba(3,6,12,0.5)"; g.beginPath(); g.rect(0, 0, W, H);
    g.moveTo(cx, cy); g.arc(cx, cy, rmax, a0, a1); g.closePath(); g.fill("evenodd");
    // Línea del radar y anillos guía
    g.strokeStyle = "rgba(255,255,255,0.18)"; g.lineWidth = 1;
    for (let k = 1; k < ROWS; k++) { g.beginPath(); g.arc(cx, cy, rmax * k / ROWS, 0, 2 * Math.PI); g.stroke(); }
    g.strokeStyle = "rgba(255,255,255,0.95)"; g.lineWidth = 2;
    g.beginPath(); g.moveTo(cx, cy); g.lineTo(cx + Math.cos(a1) * rmax, cy + Math.sin(a1) * rmax); g.stroke();
    if (row >= 0) {
      const r0 = rmax * row / ROWS, r1 = rmax * (row + 1) / ROWS;
      g.strokeStyle = "#ffd166"; g.lineWidth = 2.5; g.beginPath(); g.arc(cx, cy, r1, a0, a1); g.arc(cx, cy, Math.max(0.5, r0), a1, a0, true); g.closePath(); g.stroke();
    }
    g.fillStyle = "#ffd166"; g.beginPath(); g.arc(cx, cy, 3.5, 0, 2 * Math.PI); g.fill();
    drawPoints(g);
  } else if (A.mode === "image") {
    if (opts.showClass && A.classLayer) g.drawImage(A.classLayer, 0, 0);
    drawPoints(g);
    const w = W / A.steps;
    g.fillStyle = "rgba(3,6,12,0.45)";
    g.fillRect(0, 0, step * w, H); g.fillRect((step + 1) * w, 0, W - (step + 1) * w, H);
    g.strokeStyle = "rgba(255,255,255,0.95)"; g.lineWidth = 1.5; g.strokeRect(step * w + 0.75, 0.75, w - 1.5, H - 1.5);
    if (row >= 0) { const h = H / ROWS; g.strokeStyle = "#ffd166"; g.lineWidth = 2.5; g.strokeRect(step * w + 1, row * h + 1, w - 2, h - 2); }
  } else {
    // Serie de mapas: se muestra el mapa del año actual; video: se salta al fotograma.
    if (A.frameUrls) { const u = A.frameUrls[step]; if (els.img.dataset.frame !== u) { els.img.src = u; els.img.dataset.frame = u; } }
    else if (A.times && Math.abs(video.currentTime - A.times[step]) > 0.01) video.currentTime = A.times[step];
    const h = H / ROWS;
    if (row >= 0) { g.fillStyle = "rgba(3,6,12,0.45)"; g.fillRect(0, 0, W, row * h); g.fillRect(0, (row + 1) * h, W, H - (row + 1) * h); g.strokeStyle = "#ffd166"; g.lineWidth = 2.5; g.strokeRect(1, row * h + 1, W - 2, h - 2); }
    g.fillStyle = "rgba(255,255,255,0.25)"; g.fillRect(0, H - 6, W, 6);
    g.fillStyle = "#7cc4ff"; g.fillRect(0, H - 6, W * (step + 1) / A.steps, 6);
  }
}

// ======================================================================================
// Mapas con contexto (Leaflet + OpenStreetMap) y explorador mundial (NASA GIBS)
// ======================================================================================
const OSM_ATTR = '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';
const GIBS_ATTR = 'Cobertura terrestre: <a href="https://earthdata.nasa.gov/gibs">NASA GIBS</a> · MODIS MCD12Q1';
const gibsIgbp = yr => `https://gibs.earthdata.nasa.gov/wmts/epsg3857/best/MODIS_Combined_L3_IGBP_Land_Cover_Type_Annual/default/${yr}-01-01/GoogleMapsCompatible_Level8/{z}/{y}/{x}.png`;

// Mapa base de OpenStreetMap (sin clave de API), oscurecido con CSS (.osm-dark) para que se lean los colores de encima.
function baseLayers(map, noWrap = false) {
  L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", { attribution: OSM_ATTR, maxZoom: 19, className: "osm-dark", noWrap }).addTo(map);
}

// Máscara gris: todo lo que NO suena queda tapado; el recuadro que suena queda limpio y con borde.
const MASK_OUTER = [[-90, -540], [90, -540], [90, 540], [-90, 540]];
function setMask(m, map, box) {
  const b = [[Math.max(-85, box.latMin), box.lonMin], [Math.min(85, box.latMax), box.lonMax]];
  const hole = [[b[0][0], b[0][1]], [b[1][0], b[0][1]], [b[1][0], b[1][1]], [b[0][0], b[1][1]]];
  if (m) { m.shade.setLatLngs([MASK_OUTER, hole]); m.edge.setBounds(b); return m; }
  return {
    shade: L.polygon([MASK_OUTER, hole], { stroke: false, fillColor: "#05070b", fillOpacity: 0.72, interactive: false }).addTo(map),
    edge: L.rectangle(b, { color: "#7cc4ff", weight: 2, dashArray: "6 4", fill: false, interactive: false }).addTo(map),
  };
}

// Los mapas de SONORA están en lat/lon (equirectangular) y Leaflet en Mercator: se reproyecta por filas
// para que el mapa encaje con ríos y fronteras (en latitudes altas la diferencia es grande).
const mercY = lat => Math.log(Math.tan(Math.PI / 4 + Math.max(-85, Math.min(85, lat)) * Math.PI / 360));
const ctxCache = new Map();
async function mercatorUrl(url, box) {
  const key = url + "|" + box.latMin + "|" + box.latMax;
  if (ctxCache.has(key)) return ctxCache.get(key);
  const img = await loadImage(url);
  const W = Math.min(1200, img.naturalWidth), srcH = img.naturalHeight;
  const latMax = Math.min(85, box.latMax), latMin = Math.max(-85, box.latMin);
  const yT = mercY(latMax), yB = mercY(latMin);
  const H = Math.max(1, Math.round(W * (yT - yB) / ((box.lonMax - box.lonMin) * Math.PI / 180)));
  const c = document.createElement("canvas"); c.width = W; c.height = Math.min(H, 2400);
  const g = c.getContext("2d"); g.imageSmoothingEnabled = false;
  for (let oy = 0; oy < c.height; oy++) {
    const m = yT - (oy + 0.5) / c.height * (yT - yB), lat = Math.atan(Math.sinh(m)) * 180 / Math.PI;
    const sy = Math.min(srcH - 1, Math.max(0, Math.floor((box.latMax - lat) / (box.latMax - box.latMin) * srcH)));
    g.drawImage(img, 0, sy, img.naturalWidth, 1, 0, oy, W, 1);
  }
  const out = { url: c.toDataURL("image/png"), bounds: [[latMin, box.lonMin], [latMax, box.lonMax]] };  if (ctxCache.size > 60) ctxCache.clear();
  ctxCache.set(key, out);
  return out;
}

// ---------- Leyenda lateral de los mapas y series de /api/biomas, /api/mundo y /api/tendencias ----------
// Muestra el año del paso (o el periodo si el mapa resume varios años) y qué es cada color; en las tendencias,
// además el valor de cada variable en ese año y su tendencia de largo plazo.
const ROLE_ES = { melody: "Melodía", rhythm: "Ritmo", bass: "Bajo", pad: "Armonía" };
let LEG = null, legendEl = null;
export function initLegend(el) { legendEl = el || null; return !!legendEl; }
const escHtml = s => String(s ?? "").replace(/[&<>"]/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
function setLegend(info, series) {
  const frames = series ? info.frames : null;
  const years = frames ? frames.map(f => f.anio) : null;
  // Mapa único: año si lo tiene; si no, el periodo que resume (p. ej. "2001–2024" en el título).
  const range = (info.title || "").match(/(\d{4})\s*[–-]\s*(\d{4})/);
  LEG = { a: A, info, frames, years, single: frames ? null : (info.anio ? `Año ${info.anio}` : range ? `Periodo ${range[1]}–${range[2]}` : null), key: null };
}
function updateLegend() {
  if (!legendEl) return;
  const on = !!(LEG && A && LEG.a === A); // otra fuente (ejemplo NASA, archivo subido): sin leyenda
  legendEl.hidden = !on;
  if (!on) return;
  const key = LEG.frames ? step : -1;
  if (LEG.key === key) return;
  LEG.key = key;
  const { info, frames, years } = LEG, f = frames ? frames[Math.min(step, frames.length - 1)] : null;
  let h = "";
  if (f) {
    h += `<p class="leg-year">${f.anio}${f.provisional ? ' <span class="leg-tag">provisional</span>' : ""}</p>` +
         `<p class="leg-sub">Serie ${years[0]}–${years[years.length - 1]} · año ${step + 1} de ${years.length}</p>`;
  } else if (LEG.single) {
    h += `<p class="leg-year">${escHtml(LEG.single)}</p><p class="leg-sub">${LEG.single.startsWith("Periodo") ? "Un solo mapa que resume todo el periodo" : "Un solo año"}</p>`;
  }
  h += "<ul>";
  info.palette.forEach((p, k) => {
    const v = f && f.valores ? f.valores[k] : null, nota = info.notas ? info.notas[k] : null;
    h += `<li><span class="leg-sw" style="background:rgb(${p.rgb.join(",")})"></span><div>` +
         `<b>${escHtml(p.name)}</b> <span class="leg-role">${ROLE_ES[p.role] || ""}</span>` +
         (v ? `<span class="leg-val">${escHtml(v)}</span>` : "") +
         (nota ? `<span class="leg-note">${escHtml(nota)}</span>` : "") + "</div></li>";
  });
  h += "</ul>";
  if (info.tipo === "tendencias")
    h += `<p class="leg-sub">Barra más larga = valor más alto de toda la serie; más corta = el más bajo. Nota más aguda = valor más alto.</p>`;
  legendEl.innerHTML = h;
}

let ctx = null; // mapa de contexto del reproductor
export function initContextMap(el) {
  if (!window.L || !el) return false;
  // Un solo mundo (sin copias a los lados) y zoom fraccionario para que un mapa global llene el recuadro.
  const map = L.map(el, { keyboard: false, attributionControl: true, maxZoom: 18, fadeAnimation: false, zoomSnap: 0.25,
                          maxBounds: [[-88, -200], [88, 200]], maxBoundsViscosity: 1 }).setView([-9, -75], 5);
  baseLayers(map, true);
  ctx = { map, el, overlay: null, band: null, mask: null, boxKey: null, url: null };
  return true;
}

function updateContext() {
  if (!ctx || !A || !A.box) return;
  const box = A.box, boxKey = JSON.stringify(box);
  if (ctx.boxKey !== boxKey) {
    ctx.boxKey = boxKey;
    ctx.mask = setMask(ctx.mask, ctx.map, box);
    // Alto según la forma de la región en Mercator (un mapa global es apaisado): entre 260 y 520 px.
    const wpx = ctx.el.clientWidth || 800;
    const ratio = (mercY(Math.min(85, box.latMax)) - mercY(Math.max(-85, box.latMin))) / ((box.lonMax - box.lonMin) * Math.PI / 180);
    ctx.el.style.height = Math.round(Math.max(260, Math.min(520, wpx * ratio + 20))) + "px";
    setTimeout(() => { ctx.map.invalidateSize(); ctx.map.fitBounds([[Math.max(-85, box.latMin), box.lonMin], [Math.min(85, box.latMax), box.lonMax]], { padding: [10, 10] }); }, 50);
  }
  const url = A.frameUrls ? A.frameUrls[step] : els.img.src;
  if (url && ctx.url !== url) {
    ctx.url = url;
    mercatorUrl(url, box).then(o => {
      if (ctx.url !== url) return;
      if (ctx.overlay) ctx.overlay.setUrl(o.url).setBounds(L.latLngBounds(o.bounds));
      else ctx.overlay = L.imageOverlay(o.url, o.bounds, { opacity: 0.6, className: "ctx-overlay" }).addTo(ctx.map);
    }).catch(() => {});
  }
  // Franja que está sonando: columna de longitudes (y la franja de latitud si se explora una celda)
  if (A.mode === "image" && A.scan !== "radar") {
    const dx = (box.lonMax - box.lonMin) / A.steps, x0 = box.lonMin + step * dx;
    let la0 = box.latMin, la1 = box.latMax;
    if (row >= 0) { const dy = (box.latMax - box.latMin) / ROWS; la1 = box.latMax - row * dy; la0 = la1 - dy; }
    const b = [[Math.max(-85, la0), x0], [Math.min(85, la1), x0 + dx]];
    if (ctx.band) ctx.band.setBounds(b);
    else ctx.band = L.rectangle(b, { color: "#ffd166", weight: 2, fill: true, fillOpacity: 0.08, interactive: false }).addTo(ctx.map);
  } else if (ctx.band) { ctx.band.remove(); ctx.band = null; }
}

// ---------- Explorador mundial ----------
let world = null;
export function initWorldMap(el, year) {
  if (!window.L || !el) return false;
  // boxZoom desactivado: Mayús + arrastrar sirve para dibujar la zona que se va a escuchar.
  const map = L.map(el, { worldCopyJump: true, minZoom: 2, maxZoom: 18, boxZoom: false, fadeAnimation: false }).setView([-8, -62], 4);
  baseLayers(map);
  const igbp = L.tileLayer(gibsIgbp(year), { attribution: GIBS_ATTR, maxNativeZoom: 8, maxZoom: 18, opacity: 0.7, bounds: [[-85, -180], [85, 180]] }).addTo(map);
  world = { map, igbp, sel: null, mask: null };
  map.on("moveend zoomend resize", worldMask);
  // Mayús + arrastrar: recuadro propio. Clic derecho: vuelve al recuadro automático (60 % central de la vista).
  let start = null, tmp = null;
  map.on("mousedown", e => {
    if (!e.originalEvent.shiftKey) return;
    start = e.latlng; map.dragging.disable();
    tmp = L.rectangle([start, start], { color: "#ffd166", weight: 2, fillOpacity: 0.1, interactive: false }).addTo(map);
  });
  map.on("mousemove", e => { if (start) tmp.setBounds([start, e.latlng]); });
  map.on("mouseup", e => {
    if (!start) return;
    const b = L.latLngBounds(start, e.latlng); start = null; map.dragging.enable(); tmp.remove(); tmp = null;
    if (b.getEast() - b.getWest() > 0.05 && b.getNorth() - b.getSouth() > 0.05) world.sel = b;
    worldMask();
  });
  map.on("contextmenu", () => { world.sel = null; worldMask(); });
  setTimeout(worldMask, 0);
  return true;
}
function worldMask() { const v = worldView(); if (v) world.mask = setMask(world.mask, world.map, v); }
export function worldSetYear(year) { if (world) world.igbp.setUrl(gibsIgbp(year)); }
export function worldGoTo(lat, lon, zoom) { if (world) { world.sel = null; world.map.setView([lat, lon], zoom || 6); } }
// Zona que se va a escuchar (máx. 40° de lado): el recuadro dibujado o, si no hay, el 60 % central de la vista.
export function worldView() {
  if (!world) return null;
  const b = world.sel || world.map.getBounds(), c = b.getCenter(), f = world.sel ? 1 : 0.6;
  const lon = ((c.lng + 540) % 360) - 180, lat = Math.max(-80, Math.min(80, c.lat));
  const hw = Math.min(20, Math.max(0.05, f * (b.getEast() - b.getWest()) / 2)), hh = Math.min(20, Math.max(0.05, f * (b.getNorth() - b.getSouth()) / 2));
  const r = v => Math.round(v * 100) / 100;
  return { lonMin: r(Math.max(-180, lon - hw)), lonMax: r(Math.min(180, lon + hw)), latMin: r(Math.max(-85, lat - hh)), latMax: r(Math.min(85, lat + hh)) };
}

// Destellos: todos (tenues) si se ven las capas; los del paso actual siempre resaltados.
function drawPoints(g) {
  if (!A.points) return;
  if (opts.showClass) {
    for (const p of A.points) { g.strokeStyle = p.warm ? "rgba(255,170,90,.8)" : "rgba(150,210,255,.8)"; g.lineWidth = 1; g.beginPath(); g.arc(p.x + 0.5, p.y + 0.5, 2.5, 0, 2 * Math.PI); g.stroke(); }
  }
  for (const p of pointsFor(step, row)) {
    g.strokeStyle = p.warm ? "#ffb15a" : "#9fd8ff"; g.lineWidth = 2;
    g.beginPath(); g.arc(p.x + 0.5, p.y + 0.5, 5, 0, 2 * Math.PI); g.stroke();
  }
}

function degStr(v) { return `${(Math.round(v * 100) / 100).toString().replace(".", ",")}°`; }
function clockStr(v) { const h = Math.round(v / 30) % 12 || 12; return h === 1 ? "hacia la 1 en punto" : `hacia las ${h} en punto`; }

// ======================================================================================
// Voz y descargas
// ======================================================================================
function utterance(text, rate) {
  const u = new SpeechSynthesisUtterance(text);
  // Voz en el idioma elegido (botón ES/EN): el texto ya llega traducido por tr().
  u.lang = LANG === "en" ? "en-US" : "es-ES"; u.rate = rate || 1;
  const v = speechSynthesis.getVoices().find(x => x.lang && x.lang.toLowerCase().startsWith(LANG));
  if (v) u.voice = v;
  return u;
}
export async function speak(text, rate) {
  if (!("speechSynthesis" in window) || !lector.voiceEnabled()) return false;
  const gen = ++welcomeGen; // otra locución interrumpe la bienvenida
  const t = await tr(text);
  if (gen !== welcomeGen) return false;
  speechSynthesis.cancel();
  speechSynthesis.speak(utterance(t, rate)); return true;
}
export function stopSpeech() { welcomeGen++; if ("speechSynthesis" in window) speechSynthesis.cancel(); }

// Bienvenida: lee las frases una tras otra y resuelve cuando termina la última (o si se interrumpe).
// Se trocea por frases porque Chrome corta las locuciones largas (~15 s).
let welcomeGen = 0;
export async function speakAndWait(parts, rate, alreadyInLang = false) {
  if (!("speechSynthesis" in window) || !lector.voiceEnabled()) return false;
  speechSynthesis.cancel();
  const gen = ++welcomeGen;
  if (!alreadyInLang) parts = await Promise.all(parts.map(p => tr(p)));
  if (gen !== welcomeGen) return false;
  const queue = parts.flatMap(p => p.match(/[^.!?]+[.!?]*/g) || []).map(s => s.trim()).filter(Boolean);
  return new Promise(resolve => {
    const next = i => {
      if (gen !== welcomeGen) return resolve(false);
      if (i >= queue.length) return resolve(true);
      const u = utterance(queue[i], rate);
      let done = false;
      const go = () => { if (!done) { done = true; clearTimeout(t); next(i + 1); } };
      const t = setTimeout(go, 1500 + queue[i].length * 110); // respaldo si el navegador no avisa del final
      u.onend = go; u.onerror = go;
      speechSynthesis.speak(u);
    };
    next(0);
  });
}
export function speechSupported() { return "speechSynthesis" in window; }

// ---------- Asistente de voz: escuchar (Web Speech API), atenuar la música y avisos sonoros ----------
const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
let rec = null, earCtx = null, duckTimer = 0, sayGen = 0;

export function listenSupported() { return !!Recognition; }

// Aviso corto fuera de Tone.js (no se atenúa con la música): sube al empezar a escuchar, baja al terminar.
function earcon(up) {
  try {
    earCtx = earCtx || new AudioContext();
    const t = earCtx.currentTime, o = earCtx.createOscillator(), g = earCtx.createGain();
    o.frequency.setValueAtTime(up ? 660 : 880, t); o.frequency.linearRampToValueAtTime(up ? 880 : 660, t + 0.12);
    g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(0.25, t + 0.02); g.gain.exponentialRampToValueAtTime(0.0001, t + 0.18);
    o.connect(g).connect(earCtx.destination); o.start(t); o.stop(t + 0.2);
  } catch { /* sin audio: el estado se anuncia igualmente por aria-live */ }
}

// Baja la música mientras se escucha o habla el asistente (para que el micrófono no la capte).
function duck(on) {
  clearTimeout(duckTimer);
  if (audio) Tone.Destination.volume.rampTo(on ? masterDb - 22 : masterDb, 0.25);
  if (on) duckTimer = setTimeout(() => duck(false), 30000); // red de seguridad
}

// Escucha una frase en español. Devuelve { text, error }; con texto, la música sigue atenuada hasta assistantSay().
export function listen() {
  return new Promise(resolve => {
    if (!Recognition) { resolve({ text: null, error: "unsupported" }); return; }
    sayGen++; stopSpeech(); duck(true); earcon(true);
    let text = null, error = null;
    rec = new Recognition();
    rec.lang = LANG === "en" ? "en-US" : "es-ES"; rec.interimResults = false; rec.maxAlternatives = 1;
    rec.onresult = e => { text = e.results[0][0].transcript; };
    rec.onerror = e => { error = e.error; };
    // En inglés, la frase se pasa al español: las reglas del asistente están en español.
    rec.onend = async () => { rec = null; earcon(false); if (!text) duck(false); resolve({ text: text ? await toSpanish(text) : text, error }); };
    try { rec.start(); } catch { rec = null; duck(false); resolve({ text: null, error: "busy" }); }
  });
}
export function stopListening() { if (rec) rec.stop(); }

// Respuesta hablada del asistente (siempre suena, aunque la narración esté apagada). Se trocea por frases
// porque Chrome corta las locuciones largas; la música vuelve a su volumen al terminar.
// Resuelve cuando termina de hablar (o la interrumpen): el modo manos libres espera eso para volver a escuchar.
export async function assistantSay(text, rate) {
  const gen = ++sayGen; // una locución cancelada no debe devolver el volumen durante la siguiente
  // Sin voz propia (lector de pantalla): el texto ya está en la región aria-live; no se habla encima.
  if (!("speechSynthesis" in window) || !text || !lector.voiceEnabled()) { if (!lector.isReading()) duck(false); return; }
  text = await tr(text);
  if (gen !== sayGen) return;
  lector.stopReading(); speechSynthesis.cancel(); duck(true);
  const parts = (text.match(/[^.!?]+[.!?]*/g) || [text]).map(p => p.trim()).filter(Boolean);
  await new Promise(resolve => {
    // Respaldo si el navegador no avisa del final (pasa en algunos sistemas): según la longitud, no un minuto fijo.
    const safety = setTimeout(() => { if (gen === sayGen) duck(false); resolve(); }, Math.min(60000, 3000 + text.length * 90 / (rate || 1)));
    parts.forEach((p, i) => {
      const u = utterance(p, rate);
      if (i === parts.length - 1) u.onend = u.onerror = () => { clearTimeout(safety); if (gen === sayGen) duck(false); resolve(); };
      speechSynthesis.speak(u);
    });
  });
}

// ---------- Voz propia, interrupción y preferencias ----------
/** Corta al instante cualquier voz de SONORA (asistente, explicación, lectura de la página o bienvenida). */
export function stopVoice() {
  sayGen++; welcomeGen++;
  lector.stopReading();
  if ("speechSynthesis" in window) speechSynthesis.cancel();
  duck(false);
}
export function isSpeaking() { return "speechSynthesis" in window && (speechSynthesis.speaking || speechSynthesis.pending); }
export function setOwnVoice(on) { lector.setVoiceEnabled(on); if (!on) stopVoice(); }

// Preferencias locales (velocidad de narración, volumen, guía, voz propia). El micrófono nunca se guarda: al recargar
// siempre empieza apagado.
const PREFS = "sonora-prefs";
export function loadPrefs() {
  try { const p = JSON.parse(localStorage.getItem(PREFS) || "null"); return p && typeof p === "object" ? p : null; } catch { return null; }
}
export function savePrefs(p) { try { localStorage.setItem(PREFS, JSON.stringify(p)); } catch { /* modo privado o sin almacenamiento */ } }

// ---------- Bienvenida accesible (<dialog> modal) ----------
// Enter acepta y Escape descarta solo mientras la bienvenida está abierta; si el foco está en un botón, Enter pulsa ese botón.
let welcomeDlg = null, welcomeKeys = null, welcomeCancel = null, welcomeFirst = null;
export function openWelcome(dlg) {
  if (!dlg) return;
  welcomeDlg = dlg;
  welcomeKeys = e => {
    if (e.key !== "Enter" || e.isComposing) return;
    if (e.target.closest("button,a,input,select,textarea,summary")) return; // el control enfocado decide
    e.preventDefault();
    dlg.querySelector("[data-welcome-accept]")?.click();
  };
  welcomeCancel = e => { e.preventDefault(); dlg.querySelector("[data-welcome-dismiss]")?.click(); };
  dlg.addEventListener("keydown", welcomeKeys);
  dlg.addEventListener("cancel", welcomeCancel); // Escape
  if (!dlg.open) dlg.showModal();
  dlg.querySelector("[data-welcome-accept]")?.focus();
}
export function closeWelcome() {
  const dlg = welcomeDlg;
  if (!dlg) return;
  dlg.removeEventListener("keydown", welcomeKeys);
  dlg.removeEventListener("cancel", welcomeCancel);
  if (welcomeFirst) { ["pointerdown", "keydown"].forEach(t => document.removeEventListener(t, welcomeFirst, true)); welcomeFirst = null; }
  if (dlg.open) dlg.close();
  welcomeDlg = null;
}
/**
 * Dice la bienvenida si el navegador ya permite hablar (hubo interacción). Si no, espera a la primera tecla o toque
 * dentro de la bienvenida que no sea uno de sus botones (esos botones hablan por su cuenta). Nunca se interpreta el
 * silencio como respuesta: aquí solo se habla, no se decide nada.
 * Devuelve "spoken", "already" (bienvenida.js ya la dijo mientras cargaba), "waiting" o "off" (sin voz propia).
 */
export function speakWelcome(text, rate) {
  if (!("speechSynthesis" in window) || !lector.voiceEnabled()) return "off";
  if (window.__sonoraWelcome?.spoken) return "already";
  const go = () => { speechSynthesis.cancel(); speechSynthesis.speak(utterance(text, rate)); };
  if (navigator.userActivation?.hasBeenActive) { go(); return "spoken"; }
  welcomeFirst = e => {
    if (!welcomeDlg?.open) return;
    // Pulsar uno de sus botones (clic, Enter, Espacio) o Escape decide: no hace falta decir la bienvenida.
    // Tab u otras teclas solo exploran: entonces sí se dice.
    const decides = e.type === "pointerdown" ? !!e.target.closest?.("[data-welcome-actions] button") : /^(Enter| |Escape)$/.test(e.key);
    if (decides) return;
    ["pointerdown", "keydown"].forEach(t => document.removeEventListener(t, welcomeFirst, true));
    welcomeFirst = null;
    go();
  };
  ["pointerdown", "keydown"].forEach(t => document.addEventListener(t, welcomeFirst, true));
  return "waiting";
}

export function showDialog(el) { if (el && !el.open) el.showModal(); }
export function closeDialog(el) { if (el && el.open) el.close(); }
export function focusElement(el) { if (el) el.focus(); }

export function downloadText(fileName, text) {
  const a = document.createElement("a");
  a.href = URL.createObjectURL(new Blob([text], { type: "text/plain;charset=utf-8" }));
  a.download = fileName; a.click(); setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}

function lonStr(v) { const r = Math.round(v * 100) / 100; return `${Math.abs(r)}°${r < 0 ? "O" : r > 0 ? "E" : ""}`.replace(".", ","); }
function latStr(v) { const r = Math.round(v * 100) / 100; return `${Math.abs(r)}°${r > 0 ? "N" : r < 0 ? "S" : ""}`.replace(".", ","); }
