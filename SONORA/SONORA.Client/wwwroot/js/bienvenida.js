// Bienvenida por voz mientras arranca .NET (Blazor WebAssembly tarda unos segundos). Los navegadores solo dejan hablar
// después de una interacción: con el primer clic, toque o tecla se dice la bienvenida al instante. Cuando la app está
// lista abre su diálogo de bienvenida (botones «Activar guía hablada» y «Ahora no») y, si la voz ya sonó aquí, no la repite
// (window.__sonoraWelcome.spoken). Aquí nunca se decide nada: el silencio no cuenta como respuesta.
(function () {
  "use strict";
  var es = (navigator.language || "es").toLowerCase().indexOf("es") === 0;
  var w = window.__sonoraWelcome = { fired: false, acts: false, spoken: false, done: null, notify: null };
  if ("speechSynthesis" in window) { try { speechSynthesis.getVoices(); } catch (e) { /* las voces cargan solas */ } }
  // Quien desactivó la voz de SONORA (usa su lector de pantalla) no la oye tampoco aquí.
  var ownVoice = true;
  try { var p = JSON.parse(localStorage.getItem("sonora-prefs") || "null"); if (p && p.ownVoice === false) ownVoice = false; } catch (e) { /* sin almacenamiento */ }

  var TEXT = es
    ? ["Bienvenido a Sonora.", "Escucha nuestro planeta.", "¿Necesitas ayuda para navegar?", "Puedes activar la guía hablada o continuar sin ella."]
    : ["Welcome to Sonora.", "Listen to our planet.", "Do you need help to navigate?", "You can turn on the spoken guide or continue without it."];

  function voice(u) {
    u.lang = es ? "es-ES" : "en-US";
    try {
      var vs = speechSynthesis.getVoices();
      for (var i = 0; i < vs.length; i++) if (vs[i].lang && vs[i].lang.toLowerCase().indexOf(es ? "es" : "en") === 0) { u.voice = vs[i]; break; }
    } catch (e) { /* voz por defecto */ }
    return u;
  }
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
    w.acts = !!(t && t.closest && t.closest("button,a,input,select,textarea,label,summary,[role=tab],canvas"));
    // Si la app ya cargó, su diálogo de bienvenida se encarga de la voz.
    var appReady = !document.getElementById("sonora-cargando");
    var quiet = e.type === "keydown" && /^[aAlLmM]$/.test(e.key); // va a escuchar o a leer la página: no hablar encima
    if (!("speechSynthesis" in window) || !ownVoice || quiet || appReady) { w.done = Promise.resolve(false); }
    else { w.spoken = true; w.done = say(TEXT); }
    if (w.notify) w.notify();
  }
  document.addEventListener("pointerdown", first, true);
  document.addEventListener("keydown", first, true);
})();
