// Bienvenida por voz inmediata. Se carga antes que .NET (Blazor WebAssembly tarda unos segundos en arrancar):
// con el primer clic, toque o tecla dice el saludo al instante. Cuando la app está lista, sonora.js continúa
// (qué está sonando, teclas y la pregunta del modo manos libres) a través de window.__sonoraWelcome.
(function () {
  "use strict";
  var es = (navigator.language || "es").toLowerCase().indexOf("es") === 0;
  var w = window.__sonoraWelcome = { fired: false, acts: false, done: null, notify: null };
  if ("speechSynthesis" in window) { try { speechSynthesis.getVoices(); } catch (e) { /* las voces cargan solas */ } }

  function voice(u) {
    u.lang = es ? "es-ES" : "en-US";
    try {
      var vs = speechSynthesis.getVoices();
      for (var i = 0; i < vs.length; i++) if (vs[i].lang && vs[i].lang.toLowerCase().indexOf(es ? "es" : "en") === 0) { u.voice = vs[i]; break; }
    } catch (e) { /* voz por defecto */ }
    return u;
  }
  // Dice las frases en orden; true si terminó, false si otra locución (u otra acción) lo cortó.
  function say(parts) {
    return new Promise(function (resolve) {
      var k = 0;
      function next() {
        if (k >= parts.length) return resolve(true);
        var u = voice(new SpeechSynthesisUtterance(parts[k++]));
        u.onend = next;
        u.onerror = function (ev) { resolve(!(ev.error === "interrupted" || ev.error === "canceled")); };
        speechSynthesis.speak(u);
      }
      next();
    });
  }

  function first(e) {
    document.removeEventListener("pointerdown", first, true);
    document.removeEventListener("keydown", first, true);
    w.fired = true;
    var t = e.target;
    w.acts = !!(t && t.closest && t.closest("button,a,input,select,textarea,label,summary,[role=tab],canvas"))
      || (e.type === "keydown" && /^( |Enter|Arrow\w+|[aAdDeEhHlLmMvV1-5])$/.test(e.key));
    var quiet = e.type === "keydown" && /^[aAlLmM]$/.test(e.key); // va a escuchar o a leer la página: no hablar encima
    if (!("speechSynthesis" in window) || quiet) { w.done = Promise.resolve(false); }
    else if (w.acts) { w.done = say([es ? "Bienvenido a SONORA." : "Welcome to SONORA."]).then(function () { return false; }); }
    else {
      w.done = say(es
        ? ["Bienvenido a SONORA.", "Escucha nuestro planeta.",
           "Convierte las visualizaciones de la Tierra de la NASA, o tu propia imagen o video, en música que se puede escuchar, explorar y entender sin necesidad de verla."]
        : ["Welcome to SONORA.", "Listen to our planet.",
           "It turns NASA's visualizations of Earth, or your own image or video, into music you can hear, explore and understand without needing to see it."]);
    }
    if (w.notify) w.notify();
  }
  document.addEventListener("pointerdown", first, true);
  document.addEventListener("keydown", first, true);
})();
