# Hojas de cuadros del Jukebox del EIC

El Jukebox no descarga videos de la NASA en tiempo de ejecución. Cada visualización vive en
`SONORA/SONORA/wwwroot/eic/` como una **hoja de cuadros WebP**: 48 cuadros de 480 px en una cuadrícula de 8 × 6.
Son exactamente los cuadros que SONORA analiza (muestreados en `(i + 0,5) · duración / 48`), así que la música es la
misma que con el video completo, pero cada hoja pesa ~0,4–1,5 MB en lugar de 4–113 MB.

Ventajas:

- Cabe en hostings pequeños (Somee: 200 MB en total).
- Se sirve desde el mismo origen: el navegador puede leer los píxeles sin CORS.
- No depende de que `svs.gsfc.nasa.gov` responda ni del TLS del servidor (Windows 10 / Schannel falla con SVS).

`wwwroot/eic/index.json` describe cada hoja:

```json
{ "5603": { "file": "5603.webp", "frames": 48, "cols": 8, "rows": 6, "w": 480, "h": 270, "duration": 60.1 } }
```

La clave es el `Id` del catálogo en `SONORA/SONORA/Eic/EicService.cs`. Una visualización de SVS sin hoja sigue
funcionando con el respaldo anterior (el servidor descarga su video la primera vez a `App_Data/eic`).

## Agregar o regenerar hojas

Solo hace falta Node.js y un navegador Chromium (Chrome o Edge). No se usa ffmpeg.

1. Pon los `.mp4` en una carpeta. El nombre del archivo sin `.mp4` debe ser el `Id` del catálogo (`5603.mp4`,
   `vegetation.mp4`…).
2. Arranca el convertidor (escucha solo en `127.0.0.1:5390`):

   ```bash
   node docs/herramientas/hojas-eic/server.js SONORA/SONORA/wwwroot/eic <carpeta-de-videos> [<otra-carpeta> ...]
   ```

3. Abre http://127.0.0.1:5390 y espera a que diga `LISTO`. Escribe un `.webp` por video y `index.json`.
4. Si es una visualización nueva, agrégala a `EicService.Catalogo` con sus textos (título, descripción, texto
   alternativo, contexto científico y fuente).

`index.json` se reescribe con los videos de esa corrida: pasa todas las carpetas a la vez.

## Créditos

- NASA Scientific Visualization Studio: https://svs.gsfc.nasa.gov (número SVS en cada tarjeta).
- NASA Earth Observatory · Global Maps: https://earthobservatory.nasa.gov/global-maps (mapas mensuales).
