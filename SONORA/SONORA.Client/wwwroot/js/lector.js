// ======================================================================================
// Lector de página y navegación por voz (para personas ciegas o con baja visión)
//
// - Lee la página en voz alta, bloque a bloque, en orden: encabezados, textos, pestañas, botones, casillas,
//   deslizadores y listas, diciendo qué es cada cosa ("Botón…", "Pestaña…, seleccionada").
//   Resalta y desplaza hasta lo que está leyendo (útil también con baja visión).
// - Entiende órdenes de navegación ("ve a accesibilidad", "abre la pestaña biomas", "pulsa descargar video",
//   "escucha la número 3", "qué secciones hay", "siguiente sección"…) buscando en los textos de la página,
//   así funciona con cualquier botón o sección nueva sin tocar este archivo.
// - Las órdenes llegan en español (en modo inglés, sonora.js ya traduce lo que dice la persona); se comparan con
//   el texto original en español de cada elemento (esText) y también con el texto visible.
// ======================================================================================
import { LANG, esText, esAttr } from "./i18n.js";

const EN = () => LANG === "en";
const L = (es, en) => (EN() ? en : es);

// ---------- Texto ----------
const STOP = new Set("el la los las lo de del un una unos unas y o u a al en con para por que se su sus mi me te tu es esta este esto le les the of to and an on in for with my".split(" "));
export function norm(s) {
  return (s || "").normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase().replace(/[^a-z0-9ñ]+/g, " ").trim();
}
const words = s => norm(s).split(" ").filter(w => w && !STOP.has(w));
const clean = s => (s || "").replace(/[\p{Extended_Pictographic}\u{1F1E6}-\u{1F1FF}️‍★☆⏺⏹⏸▶⏳⬆⬇↗↻✕]/gu, " ").replace(/\s+/g, " ").trim();
const NUM = { uno: 1, una: 1, primera: 1, primero: 1, dos: 2, segunda: 2, segundo: 2, tres: 3, tercera: 3, tercero: 3, cuatro: 4, cuarta: 4, cuarto: 4,
  cinco: 5, quinta: 5, quinto: 5, seis: 6, sexta: 6, sexto: 6, siete: 7, septima: 7, septimo: 7, ocho: 8, octava: 8, octavo: 8, nueve: 9, novena: 9,
  noveno: 9, diez: 10, decima: 10, decimo: 10, once: 11, doce: 12, trece: 13, catorce: 14, quince: 15, dieciseis: 16, diecisiete: 17,
  dieciocho: 18, diecinueve: 19, veinte: 20, veintiuno: 21, veintidos: 22, veintitres: 23, veinticuatro: 24 };

/** Parecido entre lo que dijo la persona y la etiqueta de un elemento (0 a 1). */
function score(target, label) {
  const tw = words(target), lw = words(label);
  if (!tw.length || !lw.length) return 0;
  const same = (a, b) => a === b || (a.length >= 4 && b.length >= 4 && (a.startsWith(b) || b.startsWith(a)));
  const hitT = tw.filter(w => lw.some(x => same(w, x))).length / tw.length;
  const hitL = lw.filter(w => tw.some(x => same(w, x))).length / lw.length;
  if (hitT < 0.6 || hitL < 0.34) return 0;
  return hitT * 0.7 + hitL * 0.3;
}

// ---------- Qué hay en la página ----------
const root = () => document.querySelector(".sonora") || document.body;
function visible(el) {
  if (!el || el.closest("[hidden],[aria-hidden='true'],.leaflet-container,.skip-link,script,style,noscript,template")) return false;
  if (el.closest("dialog") && !el.closest("dialog[open]")) return false;
  return el.getClientRects().length > 0;
}
const controlOf = lab => lab.control || lab.querySelector("input,select,textarea");
function ownText(el, skip) {
  // Texto del elemento sin el de sus controles (una etiqueta con un <select> traería todas las opciones).
  const c = el.cloneNode(true);
  c.querySelectorAll(skip || "select,input,textarea,.val,[aria-hidden='true']").forEach(x => x.remove());
  return clean(c.textContent);
}
function nameOf(el) {
  const aria = el.getAttribute("aria-label");
  if (aria && /^(BUTTON|A)$/.test(el.tagName)) return clean(aria);
  if (el.tagName === "LABEL") return ownText(el);
  return clean(el.innerText || el.textContent);
}
function esNameOf(el) {
  const aria = esAttr(el, "aria-label");
  if (aria && /^(BUTTON|A)$/.test(el.tagName)) return clean(aria);
  return clean(esText(el));
}

/** Cómo se anuncia un elemento: tipo, nombre y estado. */
export function describe(el) {
  const name = nameOf(el), tag = el.tagName, role = el.getAttribute("role");
  if (/^H[1-4]$/.test(tag)) return `${L("Sección", "Section")}: ${name}.`;
  if (role === "tab") return `${L("Pestaña", "Tab")} ${name}${el.getAttribute("aria-selected") === "true" ? L(", seleccionada", ", selected") : ""}.`;
  if (tag === "BUTTON") {
    let s = `${L("Botón", "Button")} ${name}`;
    if (el.getAttribute("aria-pressed") === "true") s += L(", activado", ", on");
    if (el.getAttribute("aria-expanded") === "true") s += L(", abierto", ", open");
    if (el.disabled) s += L(", no disponible ahora", ", not available now");
    return s + ".";
  }
  if (tag === "A") return `${L("Enlace", "Link")} ${name}.`;
  if (tag === "SUMMARY") return `${L("Desplegable", "Expandable")} ${name}, ${el.parentElement?.open ? L("abierto", "open") : L("cerrado", "closed")}.`;
  if (tag === "LABEL") {
    const c = controlOf(el);
    if (!c) return name;
    if (c.type === "checkbox" || c.type === "radio") return `${L("Casilla", "Checkbox")} ${name}, ${c.checked ? L("marcada", "checked") : L("sin marcar", "not checked")}.`;
    if (c.type === "range") return `${L("Deslizador", "Slider")} ${name}, ${c.getAttribute("aria-valuetext") || c.value}.`;
    if (c.tagName === "SELECT") return `${L("Lista", "List")} ${name}, ${L("opción elegida", "selected option")}: ${clean(c.selectedOptions[0]?.textContent)}.`;
    if (c.type === "file") return `${L("Botón para elegir archivo", "File picker button")}: ${name}.`;
    return `${L("Campo de texto", "Text field")} ${name}${c.value ? `, ${L("contiene", "contains")}: ${c.value}` : ""}.`;
  }
  if (tag === "LI" && el.closest("ol.chat")) return name;
  return name;
}

const BLOCKS = "h1,h2,h3,h4,p,li,dt,dd,th,td,legend,figcaption,button,a[href],label,summary,[role=tab]";
/** Bloques de lectura en orden, sin repetir: si un bloque contiene otro, se leen los de dentro. */
export function blocks() {
  const all = [...root().querySelectorAll(BLOCKS)].filter(visible)
    .filter(el => !el.closest(".assistant-float") || el.closest(".assistant-float:not([hidden])"));
  const set = new Set(all);
  return all.filter(el => {
    if (el.tagName === "LABEL" || el.tagName === "BUTTON" || el.tagName === "A" || el.getAttribute("role") === "tab") {
      // un botón o enlace dentro de otro bloque leíble (p. ej. un enlace dentro de un párrafo) se lee con el párrafo
      for (let p = el.parentElement; p; p = p.parentElement) if (set.has(p) && /^(P|TD|TH|DD)$/.test(p.tagName)) return false;
      return !!clean(describe(el));
    }
    if ([...el.querySelectorAll(BLOCKS)].some(c => set.has(c))) return false;
    return !!clean(el.innerText || el.textContent);
  });
}
const isHeading = el => /^H[1-3]$/.test(el.tagName);

// ---------- Lectura ----------
let hooks = { duck: () => {}, rate: () => 1 };
export function setReaderHooks(h) { hooks = { ...hooks, ...h }; }
// Voz propia de SONORA. Quien usa un lector de pantalla (NVDA, JAWS, VoiceOver, TalkBack) puede apagarla para que no
// se superpongan dos voces: los textos siguen llegando por las regiones aria-live. No se intenta detectar el lector.
let voiceOn = true;
export function setVoiceEnabled(on) { voiceOn = !!on; if (!voiceOn) stopReading(); }
export function voiceEnabled() { return voiceOn; }
const R = { list: [], i: 0, gen: 0, active: false, paused: false, at: 0, idle: [] };
let current = null;
function mark(el) {
  current?.classList.remove("lector-actual");
  current = el;
  if (!el) return;
  el.classList.add("lector-actual");
  try { el.scrollIntoView({ block: "center", behavior: matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth" }); } catch { /* opcional */ }
}
function voiceFor(u) {
  u.lang = EN() ? "en-US" : "es-ES"; u.rate = hooks.rate() || 1;
  const v = speechSynthesis.getVoices().find(x => x.lang && x.lang.toLowerCase().startsWith(LANG));
  if (v) u.voice = v;
  return u;
}
/** Dice un texto; resuelve "end" al terminar o "cut" si otra locución lo interrumpió. */
function say(text, gen) {
  const parts = (text.match(/[^.!?;:]+[.!?;:]*/g) || [text]).map(s => s.trim()).filter(Boolean);
  return new Promise(res => {
    let k = 0;
    const next = () => {
      if (gen !== R.gen) return res("cut");
      if (k >= parts.length) return res("end");
      const u = voiceFor(new SpeechSynthesisUtterance(parts[k++]));
      u.onend = next;
      u.onerror = e => res(e.error === "interrupted" || e.error === "canceled" ? "cut" : "end");
      speechSynthesis.speak(u);
    };
    next();
  });
}
function idleNow() { const w = R.idle; R.idle = []; w.forEach(f => f()); }
/** Promesa que se cumple cuando no se está leyendo (el modo manos libres espera a que termine la lectura). */
export function waitReaderIdle() { return R.active ? new Promise(r => R.idle.push(r)) : Promise.resolve(); }
export function isReading() { return R.active; }

async function readFrom(i, list) {
  if (!("speechSynthesis" in window) || !voiceOn) return;
  R.list = list || blocks();
  R.i = Math.max(0, Math.min(i, R.list.length - 1));
  const gen = ++R.gen;
  speechSynthesis.cancel();
  R.active = true; R.paused = false; hooks.duck(true);
  while (gen === R.gen && R.i < R.list.length) {
    const el = R.list[R.i];
    if (!el.isConnected || !visible(el)) { R.i++; continue; }
    mark(el);
    const r = await say(describe(el), gen);
    if (r === "cut" || gen !== R.gen) { if (gen === R.gen) { R.active = false; R.paused = true; R.at = Date.now(); hooks.duck(false); idleNow(); } return; }
    R.i++;
  }
  if (gen === R.gen) {
    R.active = false; R.paused = false; mark(null); hooks.duck(false); idleNow();
    if (R.i >= R.list.length) await say(L("Fin de la página. Di «lee la página» para empezar de nuevo.", "End of the page. Say «read the page» to start again."), gen);
  }
}
/** Detiene la lectura (y se puede reanudar donde iba). */
export function stopReading() {
  if (!R.active) return false;
  R.gen++; speechSynthesis.cancel();
  R.active = false; R.paused = true; R.at = Date.now(); hooks.duck(false); idleNow();
  return true;
}
/** Tecla L: empieza, detiene o reanuda la lectura. */
export function toggleReading() {
  if (R.active) { stopReading(); return; }
  readFrom(R.paused ? R.i : 0, R.paused ? R.list : undefined);
}
const indexIn = (list, el) => { const k = list.indexOf(el); if (k >= 0) return k; const all = list; return Math.max(0, all.findIndex(b => el.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING)); };
function hereIndex(list) {
  if (R.paused && R.list === list) return R.i;
  const f = document.activeElement;
  return f && f !== document.body ? indexIn(list, f) : 0;
}

// ---------- Secciones, controles y visualizaciones ----------
function sectionOf(el) {
  const sec = el?.closest?.("section, .tabpanel:not([hidden]), .assistant-float, dialog[open]");
  return sec || root();
}
function hereElement() {
  if (current) return current;
  if (R.paused && R.list[R.i]) return R.list[R.i];
  const f = document.activeElement;
  return f && f !== document.body ? f : document.querySelector("#player");
}
function controlsIn(scope) {
  return [...scope.querySelectorAll("button,a[href],label,summary,[role=tab]")].filter(visible)
    .filter(el => el.tagName !== "LABEL" || controlOf(el));
}
function cards() { return [...document.querySelectorAll(".eic-grid .eic-card")].filter(visible); }
function cardTitle(c) { return clean(c.querySelector("h3")?.textContent); }
function openTab(id) { const t = document.getElementById(id); if (t && t.getAttribute("aria-selected") !== "true") t.click(); }

function listSections() {
  const hs = [...root().querySelectorAll("h2")].filter(visible);
  const parts = hs.map(h => {
    const sec = h.closest("section");
    const tabs = sec ? [...sec.querySelectorAll("[role=tab]")].filter(visible).map(nameOf) : [];
    return tabs.length ? `${nameOf(h)} (${L("pestañas", "tabs")}: ${tabs.join(", ")})` : nameOf(h);
  });
  return `${L("La página tiene", "The page has")} ${hs.length} ${L("secciones", "sections")}: ${parts.join("; ")}. ` +
    L("Además, el botón del asistente abajo a la derecha. Di «ve a» y el nombre de una sección o pestaña.",
      "There is also the assistant button at the bottom right. Say «go to» and the name of a section or tab.");
}
function listControls() {
  const sec = sectionOf(hereElement());
  const head = sec.querySelector("h2,h3");
  const items = controlsIn(sec).slice(0, 18).map(el => clean(describe(el)).replace(/\.$/, ""));
  if (!items.length) return L("Aquí no hay controles.", "There are no controls here.");
  return `${head ? `${nameOf(head)}. ` : ""}${L("Aquí hay", "Here there are")} ${items.length} ${L("controles", "controls")}: ${items.join("; ")}. ` +
    L("Di «pulsa» y su nombre.", "Say «press» and its name.");
}
function listCards() {
  openTab("src-tab-eic");
  const cs = cards();
  if (!cs.length) return L("El catálogo todavía se está cargando. Pregunta otra vez en un momento.", "The catalog is still loading. Ask again in a moment.");
  return `${cs.length} ${L("visualizaciones", "visualizations")}: ` + cs.map((c, k) => `${k + 1}, ${cardTitle(c)}`).join("; ") + ". " +
    L("Di «escucha la número» y el número, o el nombre.", "Say «listen to number» and the number, or the name.");
}

// Palabras que nombran el tipo de control y no su nombre: «abre la pestaña clima» busca «clima».
// Sin quitarlas, «pestaña» contaba como una palabra que no coincide y «Clima de Perú» quedaba por debajo del umbral.
const FILLER = /\b(pestanas?|tabs?|secciones|seccion|apartados?|botones|boton|buttons?|sections?|opcion|opciones)\b/g;

/** El mejor elemento para lo que nombró la persona entre los candidatos. */
function best(target, cands) {
  target = norm(target).replace(FILLER, " ").replace(/\s+/g, " ").trim() || target;
  let top = null, s0 = 0;
  for (const c of cands) {
    const s = Math.max(score(target, c.es), score(target, c.cur));
    if (s > s0) { s0 = s; top = c; }
  }
  return top;
}
const cand = (el, label, labelEs) => ({ el, cur: label ?? nameOf(el), es: labelEs ?? esNameOf(el) });

function focusOn(el) {
  if (!el.matches("button,a,input,select,textarea,summary,[tabindex]")) el.setAttribute("tabindex", "-1");
  el.focus({ preventScroll: true });
  mark(el); setTimeout(() => { if (!R.active) mark(null); }, 4000);
}

// ---------- Órdenes ----------
// En español y en inglés (en modo inglés sin traductor del navegador la frase llega tal cual).
const RE_GO = /^(?:por favor |please )?(?:ve|ir|vamos|vete|llevame|lleva|anda|salta|enfoca|navega|muevete|go|go to|take me to|navigate to|move to|jump to)\s+(?:(?:a|al|a la|hacia|hasta|en|to)\s+)?(.+)$/;
const RE_OPEN = /^(?:por favor |please )?(?:abre|abrir|abreme|muestra|muestrame|mostrar|ensename|cambia|pasa|open|show|show me|switch to)\s+(?:(?:a|al|a la)\s+)?(.+)$/;
const RE_PRESS = /^(?:por favor |please )?(?:pulsa|pulsar|presiona|presionar|aprieta|haz clic|haz click|clic|click|dale clic|da clic|toca el boton|selecciona|seleccionar|elige|escoge|usa|press|click on|tap|select|choose|hit)\s+(?:(?:en|sobre|on)\s+)?(.+)$/;
const RE_CHECK = /^(?:por favor |please )?(activa|activar|marca|marcar|enciende|desactiva|desactivar|desmarca|desmarcar|apaga|quita|turn on|enable|check|turn off|disable|uncheck)\s+(.+)$/;
const RE_LISTEN = /^(?:por favor |please )?(?:escucha|escuchar|quiero escuchar|quiero oir|pon|ponme|reproduce|carga|abre|listen to|play|load|open)\s+(.+)$/;
const RE_READ = /^(?:por favor |please )?(?:lee|leer|leeme|read)\s+(.+)$/;
const ON_RE = /^(activa|activar|marca|marcar|enciende|turn on|enable|check)$/;
function numberIn(t) {
  const m = t.match(/(?:numero|number|n|la|el|tarjeta|visualizacion|opcion|card)\s+(\d+)\b/) || t.match(/^(\d+)$/);
  if (m) return +m[1];
  for (const w of t.split(" ")) if (NUM[w]) return NUM[w];
  return null;
}

/**
 * Intenta entender una orden de lectura o navegación. Devuelve la respuesta que debe decir el asistente
 * ("" si no hay que decir nada porque ya empezó la lectura) o null si no es una orden de este tipo.
 */
export function pageCommand(text) {
  const t = norm(text).replace(/^(?:sonora|oye sonora|oye)\s+/, "");
  if (!t) return null;
  const recentPause = R.paused && Date.now() - R.at < 120000;

  // --- Lectura ---
  if (/^(lee|leer|leeme)( toda| todo)?( la| el)? (pagina|web|sitio|contenido)|^(lee|leeme) todo$|^lee desde (el )?(principio|inicio|comienzo)|^lectura completa|^read( the| this| the whole| all the)? (page|website|site)|^read everything/.test(t))
    { readFrom(0); return ""; }
  if (/(sigue|continua|reanuda|continuar|seguir) (leyendo|la lectura|con la lectura)|(keep|continue|resume) reading/.test(t) || (recentPause && /^(sigue|continua|continuar|seguir|reanuda|continue|resume|go on)$/.test(t)))
    { readFrom(R.paused ? R.i : 0, R.paused ? R.list : undefined); return ""; }
  if (/(para|deja|dejes|deten|detener|termina|basta|parar)( de)? (leer|la lectura)|stop reading|^(silencio|callate|calla|basta|stop|quiet|be quiet)$/.test(t) || ((R.active || recentPause) && /^(para|detente|alto)$/.test(t))) {
    const was = stopReading() || R.paused; R.paused = false;
    return was ? L("Lectura detenida.", "Reading stopped.") : (/^(silencio|callate|calla|basta)$/.test(t) ? "" : null);
  }
  if (/^(lee|leeme|lee de nuevo)? ?(esta|la) seccion$|^lee aqui$|^read (this|the) section$/.test(t)) {
    const list = blocks(), sec = sectionOf(hereElement());
    const k = list.findIndex(b => sec.contains(b));
    readFrom(k < 0 ? 0 : k, list); return "";
  }
  const secMove = t.match(/^(siguiente|proxima|anterior) (seccion|apartado|parte)$|^(seccion|apartado) (siguiente|anterior)$|^(next|previous) (section|heading)$/);
  if (secMove) {
    const fwd = !/anterior|previous/.test(t), list = blocks(), here = hereIndex(list);
    const heads = list.map((b, k) => [b, k]).filter(([b]) => isHeading(b)).map(([, k]) => k);
    const k = fwd ? heads.find(h => h > here) : [...heads].reverse().find(h => h < here);
    if (k === undefined) return fwd ? L("No hay más secciones.", "There are no more sections.") : L("Estás en la primera sección.", "You are at the first section.");
    readFrom(k, list); return "";
  }
  if (/^(siguiente|proximo) (parrafo|elemento|bloque|texto|linea)$/.test(t) || ((R.active || recentPause) && /^(siguiente|adelante)$/.test(t)))
    { readFrom((R.paused || R.active ? R.i : hereIndex(R.list = blocks())) + 1, R.list.length ? R.list : undefined); return ""; }
  if (/^(parrafo|elemento|bloque) anterior$|^anterior (parrafo|elemento|bloque)$/.test(t) || ((R.active || recentPause) && /^(anterior|atras)$/.test(t)))
    { readFrom(Math.max(0, (R.paused || R.active ? R.i : hereIndex(R.list = blocks())) - 1), R.list.length ? R.list : undefined); return ""; }
  if (recentPause && /^(repite|repitelo|otra vez|de nuevo)$/.test(t)) { readFrom(R.i, R.list); return ""; }

  // --- Qué hay ---
  if (/que (secciones|partes|apartados|pestanas) hay|^(indice|menu|estructura)( de la pagina)?$|que hay en (la|esta) pagina|como es la pagina|de que se compone|what (sections|tabs|parts)|what is on (the|this) page/.test(t))
    return listSections();
  if (/que (botones|opciones|controles|cosas) (hay|tengo|puedo)|que puedo (hacer|pulsar|tocar) aqui|que hay aqui$|what (buttons|controls|options)|what is here|what can i (do|press) here/.test(t))
    return listControls();
  if (/que (visualizaciones|mapas|videos|opciones de musica) hay|lista de (visualizaciones|mapas)|^catalogo|que puedo escuchar|que hay en el jukebox|what (visualizations|maps|videos)|what can i listen to/.test(t))
    return listCards();
  if (/donde estoy en la pagina|en que (seccion|parte) estoy|donde esta el foco|where am i on the page|which section am i/.test(t)) {
    const el = hereElement(), sec = sectionOf(el), h = sec.querySelector("h2,h3");
    return `${h ? `${L("Estás en", "You are in")} ${nameOf(h)}. ` : ""}${el && el !== h ? describe(el) : ""}`;
  }

  // --- Escuchar una visualización (número o nombre) ---
  let m = t.match(RE_LISTEN);
  if (m) {
    const target = m[1];
    const n = numberIn(target);
    if (n && /numero|number|tarjeta|visualizacion|opcion|card|^\d+$|^(la|el) (\d+|[a-z]+)$/.test(target)) {
      openTab("src-tab-eic");
      const c = cards()[n - 1];
      if (!c) return L(`No hay una visualización número ${n}. Hay ${cards().length}.`, `There is no visualization number ${n}. There are ${cards().length}.`);
      c.querySelector("button")?.click();
      return L(`Abriendo la número ${n}: ${cardTitle(c)}.`, `Opening number ${n}: ${cardTitle(c)}.`);
    }
    const listenBtns = [...document.querySelectorAll("button")].filter(visible)
      .filter(b => /^escuchar\b|^listen\b/.test(norm(esAttr(b, "aria-label") || esText(b))));
    const cs = [...cards().map(c => cand(c.querySelector("button"), cardTitle(c), clean(esText(c.querySelector("h3"))))),
                ...listenBtns.map(b => cand(b, clean(nameOf(b)).replace(/^(listen to|listen|escuchar)\s*/i, ""), clean(esNameOf(b)).replace(/^escuchar\s*/i, "")))];
    const hit = best(target, cs);
    if (hit && score(target, hit.es) + score(target, hit.cur) > 0) {
      if (hit.el.closest(".tabpanel[hidden]")) return null;
      hit.el.click();
      return L(`Abriendo ${hit.cur}.`, `Opening ${hit.cur}.`);
    }
    if (!/^(abre|carga|open|load)/.test(t)) return null; // "escucha el polvo", "pon la escala blues"… → reglas del asistente
  }

  // --- Leer algo concreto: "lee la descripción", "lee el contexto científico" ---
  m = t.match(RE_READ);
  if (m) {
    const list = blocks();
    const hit = best(m[1], list.filter(b => isHeading(b) || b.tagName === "LEGEND" || b.tagName === "SUMMARY").map(b => cand(b)));
    if (hit) { readFrom(list.indexOf(hit.el), list); return ""; }
    return null;
  }

  // --- Ir a / abrir / pulsar / activar ---
  const tabs = () => [...document.querySelectorAll("[role=tab]")].filter(visible).map(e => cand(e));
  const heads = () => [...root().querySelectorAll("h2,h3,legend,summary")].filter(visible).map(e => cand(e));
  const ctrls = () => controlsIn(document).map(e => cand(e));
  const special = target => {
    if (/^(arriba|inicio|principio|comienzo)( de la pagina)?$/.test(target)) return document.querySelector("h1");
    if (/^(abajo|final|fin)( de la pagina)?$/.test(target)) return [...root().querySelectorAll("h2")].filter(visible).pop() || null;
    if (/asistente/.test(target)) return document.querySelector(".assistant-fab");
    if (/atajos|ayuda de teclado|teclas/.test(target)) return [...document.querySelectorAll("button")].find(b => /atajos|keyboard/i.test(b.textContent)) || null;
    return null;
  };
  const act = (el, how) => {
    if (el.getAttribute("role") === "tab") { el.click(); setTimeout(() => focusOn(el), 50); return L(`Pestaña ${nameOf(el)} abierta.`, `Tab ${nameOf(el)} open.`) + " " + L("Di «qué hay aquí» o «lee esta sección».", "Say «what is here» or «read this section»."); }
    if (how === "go" && !/^(BUTTON|A|SUMMARY|LABEL)$/.test(el.tagName)) { focusOn(el); return `${describe(el)} ${L("Di «lee esta sección» para oírla.", "Say «read this section» to hear it.")}`; }
    if (el.tagName === "LABEL") { const c = controlOf(el); if (c?.type === "checkbox" || c?.type === "radio") { c.click(); return describe(el); } c?.focus(); return describe(el); }
    if (el.disabled) return `${describe(el)}`;
    el.click(); focusOn(el);
    return L(`Pulsado: ${nameOf(el)}.`, `Pressed: ${nameOf(el)}.`);
  };

  m = t.match(RE_CHECK);
  if (m) {
    const on = ON_RE.test(m[1]);
    const labels = [...document.querySelectorAll("label")].filter(visible).filter(l => controlOf(l)?.type === "checkbox").map(e => cand(e));
    const hit = best(m[2], labels);
    if (hit) {
      const c = controlOf(hit.el);
      if (c.checked === on) return L(`${nameOf(hit.el)} ya está ${on ? "activado" : "desactivado"}.`, `${nameOf(hit.el)} is already ${on ? "on" : "off"}.`);
      c.click(); return describe(hit.el);
    }
    return null; // "activa la vibración", "desactiva la narración"… → reglas del asistente
  }
  m = t.match(RE_GO);
  if (m) {
    const sp = special(m[1]);
    if (sp) return act(sp, sp.matches(".assistant-fab") ? "press" : "go");
    const hit = best(m[1], [...tabs(), ...heads(), ...cards().map(c => cand(c.querySelector("h3")))]);
    return hit ? act(hit.el, "go") : null; // "llévame al Sahara" → reglas (lugar del mapa)
  }
  m = t.match(RE_OPEN);
  if (m) {
    const sp = special(m[1]);
    if (sp) return act(sp, "press");
    const hit = best(m[1], [...tabs(), ...ctrls(), ...heads()]);
    return hit ? act(hit.el, "press") : null;
  }
  m = t.match(RE_PRESS);
  if (m) {
    const sp = special(m[1]);
    if (sp) return act(sp, "press");
    const hit = best(m[1], [...ctrls(), ...tabs()]);
    return hit ? act(hit.el, "press") : L(`No encontré «${m[1]}» en la página. Di «qué hay aquí» para oír los controles.`, `I could not find «${m[1]}» on the page. Say «what is here» to hear the controls.`);
  }
  return null;
}

/** Ayuda de navegación por voz. */
export function navigationHelp() {
  return L("Para recorrer la página: di «lee la página», «para de leer», «sigue leyendo», «siguiente sección», «lee esta sección», " +
    "«qué secciones hay», «qué hay aquí», «ve a accesibilidad», «abre la pestaña clima», «pulsa descargar video», «activa alto contraste», " +
    "«qué visualizaciones hay» y «escucha la número 3». Teclas: L lee o detiene la lectura, M activa el modo manos libres, A habla conmigo.",
    "To move around the page say «read the page», «stop reading», «keep reading», «next section», «read this section», «what sections are there», " +
    "«what is here», «go to accessibility», «open the climate tab», «press download video», «turn on high contrast», «what visualizations are there» " +
    "and «listen to number 3». Keys: L reads or stops reading, M turns on hands-free mode, A talks to me.");
}
