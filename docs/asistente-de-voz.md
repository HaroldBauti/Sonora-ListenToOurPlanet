# Asistente de SONORA: guía hablada accesible

Pensado para personas ciegas o con baja visión, en computadora y en móvil. Todo funciona **sin micrófono** y **sin gestos personalizados**: teclado (Tab, Enter, Escape) o tocando botones. La voz es opcional.

## 1. Bienvenida
Al entrar aparece un diálogo modal:

> «Bienvenido a Sonora. Escucha nuestro planeta. ¿Necesitas ayuda para navegar? Puedes activar la guía hablada o continuar sin ella.»

- **Botones:** **Activar guía hablada** y **Ahora no**. Son grandes (≥ 48 px) y nativos, para que funcionen con TalkBack y VoiceOver.
- **Teclado** (solo mientras la bienvenida está abierta): **Enter** acepta y **Escape** descarta. Si el foco está en «Ahora no», Enter pulsa ese botón.
- **Voz:**
  - Habla en cuanto el navegador lo permite (`navigator.userActivation`).
  - Si hace falta un gesto, habla con la primera tecla o el primer toque que no sea elegir.
  - Mientras carga .NET, `bienvenida.js` dice el mismo texto y la app no lo repite.
- **Sin decisiones implícitas:**
  - El silencio nunca cuenta como respuesta.
  - No se reproduce nada ni se enciende el micrófono por iniciativa propia.
- **Sin repeticiones:** la bienvenida no se repite al cambiar de sección.

## 2. Guía hablada
`SONORA.Client/Services/GuideFlow.cs` es una máquina de estados sin dependencias de la interfaz, probada en `SONORA.Tests`.

- **Secciones reales:** Jukebox NASA, Tu archivo, Biomas de Perú, Clima de Perú y Todo el planeta.
- **Solo pide lo necesario:**

  | Sección | Datos que pide |
  |---|---|
  | Jukebox | tema → visualización |
  | Biomas | mapa → región → periodo (solo si el mapa usa año) → año |
  | Clima | región → datos → variables (las 4 que sugiere la red o elegidas una a una) |
  | Planeta | lugar → periodo → año |
  | Tu archivo | botón que abre el selector de archivos |

- **Grupos cortos:** las opciones van de 5 en 5, con números absolutos («la 7» vale en cualquier grupo), «Más opciones» y «Opciones anteriores».
- **Aprovecha lo ya dicho:** «biomas de Ucayali en 2010» rellena el mapa, la región y el año y carga directamente. «clima de Piura» salta a «¿qué datos?».
- **Regresar y corregir:**
  - «Volver» deshace la última elección.
  - «Cambiar región», «cambiar año», etc. corrigen un dato sin perder los demás. También hay un botón **Cambiar …** junto a cada elección.
  - «Menú» vuelve al inicio y conserva lo elegido en cada sección.
- **Ambigüedad:** si un nombre encaja con varias opciones («temperatura»), pregunta cuál.
- **Nada al azar:** solo propone algo si se pide una recomendación («recomiéndame»). La opción recomendada está marcada.
- **«Siguiente» y «anterior» según el contexto:**
  - Mientras se elige, pasan de grupo de opciones.
  - Con contenido listo, avanzan o retroceden un paso de la música.
  - Mientras se lee la página, pasan de párrafo.
- **Antes de reproducir** anuncia lo elegido y da la explicación sencilla. No reproduce solo.
- **La pantalla sigue a la guía:** la pestaña visible es la sección que se está eligiendo.

## 3. Explicaciones y «Aprender a escuchar»
`Explainer.cs` y `SoundRules.cs` usan solo los datos del análisis, del catálogo y las reglas reales de `sonora.js` (`playStep`, `playYearTick`).

- **Sencilla:** qué representa, fuente, periodo y el aviso «Es una sonificación: los sonidos se generan a partir de los datos. No es una grabación del lugar…», más qué capa suena como qué.
- **Detallada:**
  - Contexto científico y forma de recorrido.
  - Regla de cada capa: melodía más aguda con más intensidad; ritmo de 1, 2 o 4 golpes; bajo y armonía por volumen.
  - Destellos, campanita y motivo melódico en las series año por año.
  - Cómo se asignaron los colores (paleta oficial, 4 colores exactos o k-means), la escala, y que las intensidades son relativas.
- **Aprender a escuchar:**
  - Se elige una capa (o todas, una por una).
  - SONORA explica qué representa, la deja sonando sola donde tiene su máximo real y después donde tiene su mínimo.
  - Al terminar, o al pulsar «Detener voz», restaura las capas y la posición.

## 4. Controles y orientación
- **Con contenido listo:** Reproducir o Pausar, Describir el momento, Bajar y Subir volumen, Explicación sencilla, Explicación detallada, Aprender a escuchar, Cambiar contenido y Volver al menú.
- **«¿Dónde estoy?»:** sección, selección, contenido cargado, estado y paso actual. Funciona también sin la guía.
- **«¿Qué puedo hacer aquí?»:** las acciones de la etapa actual.
- **Botón «Asistente»:** fijo, con texto visible, en todas las secciones.

## 5. Narración, micrófono y reproducción
- **Micrófono:**
  - Activar la guía **no** enciende el micrófono. Solo se pide permiso al pulsar **Usar micrófono** (o la tecla A, o el modo manos libres).
  - Sin permiso o sin reconocimiento de voz, se avisa y todo sigue funcionando con teclado, tacto o texto.
  - El micrófono no escucha mientras SONORA habla (el modo manos libres espera a que termine), así no toma su propia voz como orden.
  - Al desactivar la escucha se cancelan también sus reinicios automáticos.
- **Explicaciones y música:**
  - Durante una explicación la música se pausa y después vuelve a su estado anterior.
  - Si la persona pausa o reproduce mientras tanto, se respeta su decisión.
  - Si una explicación interrumpe a otra, solo la última restaura el estado.
  - Las respuestas cortas solo bajan el volumen de la música.
- **Interrumpir la voz:** botón **Detener voz**, **Escape** o **Ctrl**. Escape corta la voz sin cerrar el panel.
- **Voz propia:** el interruptor **Voz de SONORA** (en el panel y en Accesibilidad) la apaga para usar el lector de pantalla. Los textos siguen llegando por las regiones `aria-live`. No se intenta detectar el lector.

## 6. Estado, correcciones y preferencias
- **Estados:** **Escuchando, Procesando, Cargando, Reproduciendo, En pausa**, en un chip visible y en una región `aria-live` que solo cambia cuando cambia el estado (`StatusAnnouncer`).
- **Corrección:** la orden reconocida se muestra («Entendí: «…»») con el botón **Corregir**, que la pone en el campo de texto para editarla.
- **Preferencias:** se guardan en `localStorage` (`sonora-prefs`) la velocidad de narración, el volumen, el uso de la guía y la voz propia. El micrófono **nunca** se restaura al recargar.
- **Errores:** si falla una carga se conservan las elecciones y se ofrece **Reintentar**, **Corregir la última elección** o **Volver al menú**.

## 7. Orden de interpretación de una frase
1. Orientación y explicaciones: «¿dónde estoy?», «¿qué puedo hacer aquí?», «explicación sencilla» o «explicación detallada» y «aprender a escuchar».
2. La guía, si está activa, según su etapa.
3. Lectura y navegación de la página (`lector.js`): «lee la página», «abre la pestaña clima»…
4. Reglas de música y mapa (`VoiceAssistant.Parse`).
5. Modelo de OpenAI (`POST /api/assistant`). Se le indica que nunca confirme una acción que no puede hacer, como cambiar de pestaña.

## Pruebas
- **Unitarias:** `dotnet test SONORA/SONORA.Tests`. Cubren las transiciones de la guía, la interpretación contextual, la ambigüedad, las correcciones, los errores, los estados y las explicaciones.
- **Extremo a extremo:** con SONORA en marcha, `python tests/e2e/test_asistente_accesible.py http://localhost:5241`. Cubre bienvenida, teclado, móvil emulado, correcciones, interrupción del audio, funcionamiento sin permiso de micrófono y sin reconocimiento, voz propia y preferencias. Simula el micrófono y la voz del navegador.

## Pendiente de verificar en dispositivos reales
- **Lectores de pantalla:** TalkBack (Android), VoiceOver (iOS y macOS) y NVDA/JAWS con la guía, el diálogo de bienvenida y las regiones `aria-live`. No se han probado en dispositivos reales.
- **Micrófono y voces reales:** reconocimiento de voz real, voces del sistema y permisos del micrófono en Chrome, Edge, Safari y Firefox.
- **Interrupción por voz:** cortar a SONORA hablándole mientras habla no está implementado. Haría falta cancelación de eco fiable, así que por ahora se interrumpe con el botón o el teclado.
