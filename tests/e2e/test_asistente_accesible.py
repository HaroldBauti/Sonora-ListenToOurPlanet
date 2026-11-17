"""
Pruebas de extremo a extremo del asistente accesible de SONORA (Playwright + Chrome sin interfaz).

Requisitos: SONORA en marcha (por defecto http://localhost:5241) y `pip install playwright` con Chrome instalado.
Uso:        python tests/e2e/test_asistente_accesible.py [url_base]

El micrófono y la voz se simulan en el navegador:
- SpeechRecognition falso: cada start() toma la siguiente frase de window.__voiceQueue (o da "no-speech"),
  o devuelve "not-allowed" si window.__micDenied es true.
- speechSynthesis registrado en window.__spoken; cada locución dura ~600 ms (o hasta speechSynthesis.cancel()),
  así se puede comprobar la interrupción y que la música se pausa durante las explicaciones.
No sustituye a una prueba con TalkBack, VoiceOver o NVDA en dispositivos reales.
"""
import re, sys, time
from playwright.sync_api import sync_playwright, expect

sys.stdout.reconfigure(encoding="utf-8")
BASE = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5241").rstrip("/")
results = []

FAKE = r"""
(() => {
  window.__voiceQueue = []; window.__spoken = []; window.__recStarts = 0; window.__cancels = 0; window.__micDenied = false;
  if (!window.__noRecognition) {
    class FakeRec {
      constructor() { this.lang = "es-ES"; this._stopped = false; }
      start() {
        window.__recStarts++;
        setTimeout(() => {
          if (window.__micDenied) { this.onerror && this.onerror({ error: "not-allowed" }); this.onend && this.onend(); return; }
          if (this._stopped) { this.onerror && this.onerror({ error: "aborted" }); this.onend && this.onend(); return; }
          const text = window.__voiceQueue.shift();
          if (text === undefined) { this.onerror && this.onerror({ error: "no-speech" }); this.onend && this.onend(); return; }
          this.onresult && this.onresult({ results: [[{ transcript: text, confidence: 0.95 }]] });
          this.onend && this.onend();
        }, 300);
      }
      stop() { this._stopped = true; } abort() { this._stopped = true; }
    }
    window.SpeechRecognition = window.webkitSpeechRecognition = FakeRec;
  } else {
    delete window.SpeechRecognition; delete window.webkitSpeechRecognition;
  }
  const ss = window.speechSynthesis, queue = [];
  let speaking = null, timer = 0;
  const next = () => {
    speaking = queue.shift() || null;
    if (!speaking) return;
    window.__spoken.push(speaking.text);
    timer = setTimeout(() => { const u = speaking; speaking = null; try { u.onend && u.onend(new Event("end")); } catch (e) {} next(); }, 600);
  };
  ss.speak = u => { queue.push(u); if (!speaking) next(); };
  ss.cancel = () => {
    window.__cancels++; clearTimeout(timer);
    const all = [speaking, ...queue.splice(0)].filter(Boolean); speaking = null;
    all.forEach(u => { try { u.onerror && u.onerror({ error: "interrupted" }); } catch (e) {} });
  };
  Object.defineProperty(ss, "speaking", { get: () => !!speaking });
  Object.defineProperty(ss, "pending", { get: () => queue.length > 0 });
})();
"""


def rec(area, name, ok, note=""):
    results.append((area, name, bool(ok), note))
    print(f"{'PASS' if ok else 'FAIL'}  [{area}] {name}  {note}", flush=True)


def check(area, name, fn):
    try:
        out = fn()
        ok, note = out if isinstance(out, tuple) else (bool(out), "")
        rec(area, name, ok, note)
    except Exception as e:
        rec(area, name, False, f"{type(e).__name__}: {str(e).splitlines()[0][:160]}")


def new_page(browser, **ctx_args):
    no_rec = ctx_args.pop("no_recognition", False)
    ctx = browser.new_context(locale="es-PE", **ctx_args)
    if no_rec:
        ctx.add_init_script("window.__noRecognition = true;")
    ctx.add_init_script(FAKE)
    page = ctx.new_page()
    page.goto(BASE + "/", wait_until="load")
    page.wait_for_selector("dialog.welcome[open]", timeout=60000)
    return ctx, page


def spoken(page): return page.evaluate("window.__spoken.join(' | ')")
def playing(page): return "Pausar" in page.locator("button.btn-play").inner_text()
def title(page): return page.locator("#playerTitle").inner_text()


def wait_until(fn, timeout=20):
    t0 = time.time()
    while time.time() - t0 < timeout:
        if fn(): return True
        time.sleep(0.2)
    return False


def type_cmd(page, text):
    page.fill("#askText", text)
    page.press("#askText", "Enter")


with sync_playwright() as p:
    browser = p.chromium.launch(channel="chrome", headless=True, args=["--autoplay-policy=no-user-gesture-required"])

    # ===================== 1. Bienvenida (escritorio) =====================
    ctx, page = new_page(browser, viewport={"width": 1366, "height": 900})
    check("bienvenida", "Diálogo modal con el texto pedido", lambda: (
        page.locator("#welcomeTitle").inner_text() == "Bienvenido a Sonora"
        and "¿Necesitas ayuda para navegar? Puedes activar la guía hablada o continuar sin ella." in page.locator("#welcomeText").inner_text(),
        page.locator("#welcomeText").inner_text()))
    check("bienvenida", "Foco inicial en «Activar guía hablada»", lambda: page.evaluate("document.activeElement.textContent.trim()") == "Activar guía hablada")
    page.wait_for_timeout(800)
    active = page.evaluate("!!(navigator.userActivation && navigator.userActivation.hasBeenActive)")
    if active:
        # Este navegador ya permite hablar sin gesto: la bienvenida se dice al abrir el diálogo.
        check("bienvenida", "Con permiso de audio, la voz empieza al abrir el diálogo", lambda: ("Bienvenido a Sonora" in spoken(page), spoken(page)[:90]))
    else:
        check("bienvenida", "Sin interacción no habla (política del navegador)", lambda: page.evaluate("window.__spoken.length") == 0)
    check("bienvenida", "Nada se decide sin la persona", lambda: page.locator("dialog.welcome[open]").count() == 1)
    page.keyboard.press("Tab")
    page.wait_for_timeout(400)
    check("bienvenida", "Tab mueve el foco a «Ahora no» y la bienvenida se ha dicho una vez", lambda: (
        page.evaluate("document.activeElement.textContent.trim()") == "Ahora no" and spoken(page).count("Bienvenido a Sonora") == 1, spoken(page)[:90]))
    page.wait_for_timeout(5000)  # el silencio no es aceptación
    check("bienvenida", "El silencio no acepta: el diálogo sigue abierto", lambda: page.locator("dialog.welcome[open]").count() == 1)
    page.keyboard.press("Enter")  # foco en «Ahora no»: Enter respeta el botón enfocado
    page.wait_for_timeout(600)
    check("bienvenida", "Enter sobre «Ahora no» descarta (respeta el botón enfocado)", lambda: (
        page.locator("dialog.welcome[open]").count() == 0 and page.locator(".guide").count() == 0))
    check("bienvenida", "El foco vuelve al título de la página", lambda: page.evaluate("document.activeElement.id") == "sonoraTitle")
    check("bienvenida", "No se enciende el micrófono ni se reproduce solo", lambda: (
        page.evaluate("window.__recStarts") == 0 and not playing(page)))
    # Cambiar de sección no repite la bienvenida
    page.click("#src-tab-biomas"); page.click("#src-tab-eic")
    check("bienvenida", "Cambiar de pestaña no repite la bienvenida", lambda: page.locator("dialog.welcome[open]").count() == 0)
    # «abre la pestaña clima» (antes fallaba por la palabra «pestaña»)
    page.click(".assistant-fab")
    type_cmd(page, "abre la pestaña clima")
    check("navegación", "«abre la pestaña clima» abre la pestaña", lambda: (
        wait_until(lambda: page.locator("#src-tab-clima").get_attribute("aria-selected") == "true", 8), ""))
    type_cmd(page, "¿dónde estoy?")
    check("orientación", "«¿Dónde estoy?» sin guía: pestaña, contenido y estado", lambda: (
        wait_until(lambda: "Estás en la pestaña Clima de Perú" in page.locator(".status").inner_text(), 8), page.locator(".status").inner_text()[:140]))
    ctx.close()

    # ===================== 2. Guía con teclado =====================
    ctx, page = new_page(browser, viewport={"width": 1366, "height": 900})
    page.keyboard.press("Escape")  # Escape descarta…
    check("bienvenida", "Escape descarta la bienvenida", lambda: page.locator("dialog.welcome[open]").count() == 0)
    page.reload(wait_until="load"); page.wait_for_selector("dialog.welcome[open]", timeout=60000)
    page.wait_for_timeout(500)
    page.keyboard.press("Enter")  # …y Enter acepta (foco en «Activar guía hablada»)
    page.wait_for_selector(".guide", timeout=8000)
    page.wait_for_timeout(800)
    check("guía", "Enter activa la guía y presenta las 5 secciones reales", lambda: (
        page.locator(".guide-options .guide-opt").count() == 5
        and [re.sub(r"^\d+\.\s*", "", t).split("\n")[0] for t in page.locator(".guide-options .guide-label").all_inner_texts()]
        == ["Jukebox NASA", "Tu archivo", "Biomas de Perú", "Clima de Perú", "Todo el planeta"],
        "; ".join(page.locator(".guide-options .guide-label").all_inner_texts())))
    check("guía", "La guía no activa el micrófono", lambda: page.evaluate("window.__recStarts") == 0)
    check("guía", "El foco pasa a la primera opción", lambda: "Jukebox NASA" in page.evaluate("document.activeElement.textContent"))
    check("guía", "La guía habla y menciona que el micrófono es opcional", lambda: ("Usar micrófono" in spoken(page), spoken(page)[:120]))
    page.keyboard.press("Tab"); page.keyboard.press("Tab"); page.keyboard.press("Enter")  # 3. Biomas de Perú
    page.wait_for_timeout(500)
    check("guía", "Teclado: Biomas → pide el mapa", lambda: ("¿Qué mapa quieres?" in page.locator(".guide-prompt").inner_text(), page.locator(".guide-prompt").inner_text()))
    check("guía", "La pestaña visible sigue a la guía", lambda: page.locator("#src-tab-biomas").get_attribute("aria-selected") == "true")
    page.keyboard.press("Enter")  # 1. Biomas
    page.wait_for_timeout(400)
    page.keyboard.press("Tab"); page.keyboard.press("Tab"); page.keyboard.press("Enter")  # 3. Ucayali
    page.wait_for_timeout(400)
    check("guía", "Solo pide el dato que falta (periodo)", lambda: ("¿Qué periodo?" in page.locator(".guide-prompt").inner_text(), page.locator(".guide-prompt").inner_text()))
    page.keyboard.press("Enter")  # 2024
    check("guía", "Carga y anuncia antes de reproducir (no reproduce solo)", lambda: (
        wait_until(lambda: "Listo" in page.locator(".guide-prompt").inner_text(), 30) and "Ucayali" in title(page) and not playing(page),
        title(page)))
    check("explicación", "Explicación sencilla con fuente, periodo y aviso de sonificación", lambda: (
        wait_until(lambda: "Fuente:" in spoken(page) and "sonificación" in spoken(page) and "Periodo: 2024" in spoken(page), 15), ""))

    # Corrección: cambiar región sin perder lo demás
    page.locator(".guide-picks button", has_text="Cambiar región").click()
    page.wait_for_timeout(400)
    check("corrección", "«Cambiar región» vuelve a pedir solo la región", lambda: "¿Qué región?" in page.locator(".guide-prompt").inner_text())
    page.locator(".guide-nav button", has_text="Más opciones").click()
    page.wait_for_timeout(300)
    page.locator(".guide-opt", has_text="Loreto").click()
    check("corrección", "Recarga con la región corregida y conserva mapa y año", lambda: (
        wait_until(lambda: "Loreto" in title(page), 30) and "Biomas" in title(page) and "2024" in title(page), title(page)))

    # Contexto: «siguiente» en la música cuando el contenido está listo
    page.wait_for_timeout(1500)
    page.locator(".guide-actions button", has_text="Reproducir").click()
    check("controles", "Reproducir desde la guía", lambda: wait_until(lambda: playing(page), 8))
    check("estado", "Estado «Reproduciendo» visible y anunciado", lambda: (
        wait_until(lambda: "Reproduciendo" in page.locator(".state-chip").inner_text(), 5)
        and wait_until(lambda: page.locator("[role=status][aria-live=polite].visually-hidden").inner_text() == "Reproduciendo", 5),
        page.locator(".state-chip").inner_text()))

    # Explicación: pausa la música, «Detener voz» la corta y la música vuelve a su estado
    n0 = page.evaluate("window.__cancels")
    page.locator(".guide-actions button", has_text="Explicación detallada").click()
    check("audio", "La música se pausa durante la explicación", lambda: wait_until(lambda: not playing(page), 5))
    page.wait_for_timeout(700)
    page.locator("button", has_text="Detener voz").click()
    check("audio", "«Detener voz» corta la explicación al instante", lambda: (
        wait_until(lambda: page.evaluate("window.__cancels") > n0 and not page.evaluate("speechSynthesis.speaking"), 3), ""))
    check("audio", "La música se restaura al terminar la explicación", lambda: wait_until(lambda: playing(page), 5))
    # Si la persona pausa durante la explicación, no se reanuda
    page.locator(".guide-actions button", has_text="Explicación sencilla").click()
    wait_until(lambda: not playing(page), 5)
    type_cmd(page, "pausa")
    page.wait_for_timeout(300)
    page.locator("button", has_text="Detener voz").click()
    page.wait_for_timeout(1500)
    check("audio", "No se reanuda si la persona pidió pausar", lambda: not playing(page))
    # Escape también detiene la voz
    page.locator(".guide-actions button", has_text="Explicación detallada").click()
    page.wait_for_timeout(500)
    page.keyboard.press("Escape")
    check("audio", "Escape detiene la voz sin cerrar el panel", lambda: (
        wait_until(lambda: not page.evaluate("speechSynthesis.speaking"), 3) and page.locator("#assistantPanel").is_visible(), ""))

    # Órdenes contextuales escritas
    type_cmd(page, "¿dónde estoy?")
    check("orientación", "«¿Dónde estoy?» con guía: sección, selección y estado", lambda: (
        wait_until(lambda: "Biomas de Perú" in page.locator(".status").inner_text() and "región Loreto" in page.locator(".status").inner_text(), 8),
        page.locator(".status").inner_text()[:160]))
    type_cmd(page, "¿qué puedo hacer aquí?")
    check("orientación", "«¿Qué puedo hacer aquí?» lista acciones del contexto", lambda: (
        wait_until(lambda: "Aquí puedes" in page.locator(".status").inner_text() and "aprender a escuchar" in page.locator(".status").inner_text(), 8),
        page.locator(".status").inner_text()[:160]))
    type_cmd(page, "siguiente")
    check("contexto", "«siguiente» con contenido listo avanza un paso de la música", lambda: (
        wait_until(lambda: re.search(r"Longitud|Columna|paso", page.locator(".status").inner_text()) is not None, 8), page.locator(".status").inner_text()[:100]))
    type_cmd(page, "menú")
    wait_until(lambda: "¿Qué quieres escuchar?" in page.locator(".guide-prompt").inner_text(), 5)
    type_cmd(page, "jukebox")
    page.wait_for_timeout(500)
    type_cmd(page, "todos los temas")
    page.wait_for_timeout(500)
    type_cmd(page, "siguiente")
    check("contexto", "«siguiente» mientras se elige muestra el siguiente grupo", lambda: (
        wait_until(lambda: page.locator(".guide-num").first.inner_text() == "6", 5), page.locator(".guide-num").first.inner_text()))
    type_cmd(page, "temperatura")
    check("ambigüedad", "Un nombre ambiguo pide aclaración", lambda: (
        wait_until(lambda: "varias opciones parecidas" in page.locator(".guide-prompt").inner_text(), 5), page.locator(".guide-prompt").inner_text()))
    type_cmd(page, "volver")
    page.wait_for_timeout(500)

    # Aprender a escuchar
    type_cmd(page, "la 6")  # Lluvia total u otra del grupo: lo importante es que cargue
    wait_until(lambda: "Listo" in page.locator(".guide-prompt").inner_text(), 30)
    page.locator(".guide-actions button", has_text="Aprender a escuchar").click()
    check("demostración", "Ofrece las capas reales para escucharlas solas", lambda: (
        wait_until(lambda: page.locator(".guide-options .guide-opt").count() >= 4, 5), "; ".join(page.locator(".guide-options .guide-label").all_inner_texts())[:140]))
    page.locator(".guide-options .guide-opt").first.click()
    check("demostración", "Aísla la capa (las demás en silencio) y explica máximo y mínimo", lambda: (
        wait_until(lambda: "máximo" in spoken(page) and page.locator(".layer.is-muted").count() >= 3, 25),
        f"capas en silencio={page.locator('.layer.is-muted').count()}, última frase={page.evaluate('window.__spoken.slice(-1)[0]')!r}"))
    page.locator("button", has_text="Detener voz").click()
    check("demostración", "Al detenerla, todas las capas vuelven a sonar", lambda: wait_until(lambda: page.locator(".layer.is-muted").count() == 0, 6))

    # Voz propia desactivable
    page.locator("button", has_text="Voz de SONORA: activada").click()
    n = page.evaluate("window.__spoken.length")
    type_cmd(page, "¿qué puedo hacer aquí?")
    page.wait_for_timeout(1200)
    check("voz", "Con la voz de SONORA desactivada no habla (el texto sigue en pantalla)", lambda: (
        page.evaluate("window.__spoken.length") == n and "Aquí puedes" in page.locator(".status").inner_text()))
    page.locator("button", has_text="Voz de SONORA: desactivada").click()

    # Micrófono: orden reconocida visible y corregible
    page.evaluate("window.__voiceQueue.push('biomas de loreto')")
    page.locator("button", has_text="Usar micrófono").click()
    check("micrófono", "Muestra la orden reconocida", lambda: (
        wait_until(lambda: "biomas de loreto" in page.locator(".heard").inner_text(), 8), page.locator(".heard").inner_text() if page.locator(".heard").count() else ""))
    page.locator(".heard button", has_text="Corregir").click()
    check("micrófono", "«Corregir» pone la orden en el campo para editarla", lambda: (
        page.input_value("#askText") == "biomas de loreto" and page.evaluate("document.activeElement.id") == "askText"))

    # Preferencias: se recuerdan, el micrófono no
    prefs = page.evaluate("localStorage.getItem('sonora-prefs')")
    check("preferencias", "Se guardan velocidad, volumen, guía y voz", lambda: (
        all(k in (prefs or "") for k in ["speechRate", "volume", "guide", "ownVoice"]) and "micro" not in (prefs or "").lower(), prefs))
    page.reload(wait_until="load"); page.wait_for_selector("dialog.welcome[open]", timeout=60000)
    check("preferencias", "Al recargar no se enciende el micrófono", lambda: page.evaluate("window.__recStarts") == 0)
    ctx.close()

    # ===================== 3. Sin permiso de micrófono =====================
    ctx, page = new_page(browser, viewport={"width": 1366, "height": 900})
    page.locator("dialog.welcome button", has_text="Activar guía hablada").click()
    page.wait_for_selector(".guide")
    page.evaluate("window.__micDenied = true")
    page.locator("button", has_text="Usar micrófono").click()
    check("sin micrófono", "Permiso denegado: lo explica y ofrece teclado y tacto", lambda: (
        wait_until(lambda: "No tengo permiso" in page.locator(".status").inner_text(), 8)
        and "Todo funciona igual con el teclado" in page.locator("#assistantPanel").inner_text(), page.locator(".status").inner_text()[:120]))
    page.locator(".guide-opt", has_text="Clima de Perú").click()
    page.wait_for_timeout(400)
    check("sin micrófono", "La guía sigue funcionando con botones", lambda: "¿Qué región?" in page.locator(".guide-prompt").inner_text())
    ctx.close()

    ctx, page = new_page(browser, viewport={"width": 1366, "height": 900}, no_recognition=True)
    page.locator("dialog.welcome button", has_text="Activar guía hablada").click()
    page.wait_for_selector(".guide")
    check("sin micrófono", "Sin reconocimiento de voz: botón deshabilitado y aviso", lambda: (
        page.locator("button", has_text="Usar micrófono").is_disabled() and "no reconoce la voz" in page.locator("#assistantPanel").inner_text()))
    page.locator(".guide-opt", has_text="Todo el planeta").click()
    page.wait_for_timeout(300)
    page.locator(".guide-opt", has_text="Sahara").click()
    page.wait_for_timeout(300)
    page.locator(".guide-opt").first.click()  # el año más reciente
    check("sin micrófono", "Carga Todo el planeta solo con botones", lambda: (wait_until(lambda: "Cobertura terrestre" in title(page), 60), title(page)))
    ctx.close()

    # ===================== 4. Móvil (emulación táctil) =====================
    ctx, page = new_page(browser, **p.devices["Pixel 7"])
    def sizes(sel): return page.eval_on_selector_all(sel, "els => els.map(e => Math.round(e.getBoundingClientRect().height))")
    check("móvil", "Botones de bienvenida de 48 px o más", lambda: (min(sizes(".welcome-actions button")) >= 48, sizes(".welcome-actions button")))
    page.locator("dialog.welcome button", has_text="Activar guía hablada").tap()
    page.wait_for_selector(".guide", timeout=8000)
    page.wait_for_timeout(500)
    check("móvil", "Opciones de la guía de 48 px o más", lambda: (min(sizes(".guide-opt")) >= 48, sizes(".guide-opt")))
    check("móvil", "Sin desplazamiento horizontal", lambda: page.evaluate("document.documentElement.scrollWidth <= window.innerWidth + 1"))
    check("móvil", "El panel cabe en la pantalla", lambda: page.evaluate(
        "(() => { const r = document.querySelector('#assistantPanel').getBoundingClientRect(); return r.left >= 0 && r.right <= innerWidth + 1; })()"))
    page.locator(".guide-opt", has_text="Biomas de Perú").tap()
    page.wait_for_timeout(300)
    page.locator(".guide-opt", has_text="Cambio de bosque").tap()
    page.wait_for_timeout(300)
    page.locator(".guide-opt", has_text="Madre de Dios").tap()
    check("móvil", "Elegir y cargar tocando", lambda: (wait_until(lambda: "Madre de Dios" in title(page), 30), title(page)))
    page.locator(".assistant-fab").tap()
    check("móvil", "El botón «Asistente» tiene texto visible y es táctil", lambda: (
        "Asistente" in page.locator(".assistant-fab").inner_text() and page.locator(".assistant-fab").bounding_box()["height"] >= 48))
    ctx.close()

    browser.close()

print("\n===== RESUMEN =====")
areas = {}
for a, n, ok, note in results:
    areas.setdefault(a, [0, 0])[0 if ok else 1] += 1
for a, (ok, ko) in areas.items():
    print(f"{a:14s} pass={ok} fail={ko}")
fails = [r for r in results if not r[2]]
print(f"TOTAL pass={len(results) - len(fails)} fail={len(fails)}")
for a, n, ok, note in fails:
    print(f"  FAIL [{a}] {n}: {note}")
sys.exit(1 if fails else 0)
