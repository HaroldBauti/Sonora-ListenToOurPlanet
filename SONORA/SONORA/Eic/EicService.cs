using System.Collections.Concurrent;
using System.Text.Json;
using SONORA.Client.Models;

namespace SONORA.Eic;

/// <summary>
/// Jukebox del Earth Information Center (EIC): visualizaciones de la Tierra de la NASA (Scientific Visualization
/// Studio y Earth Observatory) listas para escuchar.
///
/// Cada visualización se guarda como una <b>hoja de cuadros</b> WebP en <c>wwwroot/eic/</c>: 48 cuadros de 480 px en
/// una cuadrícula, que son exactamente los cuadros que SONORA analiza. Pesan 1–2 MB en lugar de 4–113 MB del video,
/// se sirven desde el mismo origen (el navegador puede leer los píxeles) y no dependen de ningún servidor externo.
/// <c>wwwroot/eic/index.json</c> describe cada hoja (lo genera el convertidor, ver <c>docs/eic-hojas.md</c>).
///
/// Respaldo: si una visualización de SVS no tiene hoja, el servidor descarga su video la primera vez y lo guarda en
/// <c>Eic:CacheDir</c> (por defecto <c>App_Data/eic</c>; límite opcional <c>Eic:MaxCacheMb</c>).
/// </summary>
public sealed class EicService(IHttpClientFactory http, IConfiguration config, IWebHostEnvironment env, ILogger<EicService> log)
{
    public sealed record Item(string Id, string Titulo, string Tema, string Periodo, string Descripcion, string Alt, string Contexto,
                              string Fuente, string FuenteUrl, string? VideoUrl = null, double Mb = 0);

    const string Svs = "NASA Scientific Visualization Studio", Eo = "NASA Earth Observatory · Global Maps";
    const string EoUrl = "https://earthobservatory.nasa.gov/global-maps";
    const string EoMensual = "Mapas mensuales del planeta";

    /// <summary>Catálogo curado. Textos en español escritos para SONORA.</summary>
    public static readonly Item[] Catalogo =
    [
        // ---------- NASA SVS (visualizaciones del Earth Information Center) ----------
        new("5603", "Anomalías de temperatura global, 1880–2025", "Temperatura", "1880–2025",
            "Mapa del mundo que cambia año a año: azul donde ese año fue más frío que el promedio 1951–1980 y rojo donde fue más cálido.",
            "Animación de un mapa mundial de anomalías de temperatura de la NASA (GISTEMP) de 1880 a 2025. Al principio predominan tonos azules y blancos; con los años el mapa se llena de naranjas y rojos, sobre todo en el Ártico y los continentes del norte.",
            "Datos GISTEMP del Instituto Goddard de Estudios Espaciales. La anomalía compara cada año con el promedio 1951–1980. 2023, 2024 y 2025 fueron los años más cálidos del registro.",
            Svs + " 5603", "https://svs.gsfc.nasa.gov/5603/", "https://svs.gsfc.nasa.gov/vis/a000000/a005600/a005603/GISTEMP_Map2025_HD_smoothed.mp4", 89.5),
        new("5688", "El Niño 2026: temperatura del mar (2025 frente a 2026)", "Océano", "2025–2026",
            "Dos globos lado a lado: la anomalía de temperatura del mar en 2025 y en 2026, día a día. El Pacífico ecuatorial se calienta con El Niño.",
            "Dos globos terráqueos centrados en el Pacífico. Rojos y naranjas marcan agua más caliente de lo normal y azules agua más fría. En el globo de 2026 crece una lengua cálida frente a Perú y Ecuador a lo largo del ecuador.",
            "Anomalía de la temperatura superficial del mar actualizada a diario para el EIC. El calentamiento del Pacífico ecuatorial frente a Sudamérica es la firma de El Niño, que trae lluvias intensas a la costa norte de Perú.",
            Svs + " 5688", "https://svs.gsfc.nasa.gov/5688/", "https://svs.gsfc.nasa.gov/vis/a000000/a005600/a005688/elnino_eic_ssta_globes_wLegend_3340x1670_latest.mp4", 74.0),
        new("5687", "El Niño 2026: color del océano (2025 frente a 2026)", "Océano", "2025–2026",
            "El color aparente del océano, que revela el fitoplancton: dónde el mar es más verde (más vida) o más azul.",
            "Dos mapas del océano, 2025 y 2026, coloreados por la longitud de onda visible aparente: verdes y amarillos donde abunda el fitoplancton, azules intensos en mar abierto pobre en nutrientes.",
            "La longitud de onda visible aparente (AVW) resume el color del agua medido por satélite. Con El Niño se debilita el afloramiento de aguas frías y ricas en nutrientes frente a Perú, y el mar se vuelve más azul.",
            Svs + " 5687", "https://svs.gsfc.nasa.gov/5687/", "https://svs.gsfc.nasa.gov/vis/a000000/a005600/a005687/elnino_avw_2025_2026_daily_1900x800.mp4", 9.1),
        new("5572", "Aerosoles GEOS: la Tierra girando", "Aire", "2024",
            "Polvo, humo, sulfatos y sal marina que viajan por la atmósfera, vistos en un globo que gira.",
            "Globo terráqueo que gira con remolinos de colores: lila para el polvo del desierto, naranja y rojo para el humo, verde para los sulfatos y gris azulado para la sal marina sobre los océanos.",
            "Modelo GEOS de la Oficina de Modelado y Asimilación Global de la NASA. Los aerosoles afectan la calidad del aire, las nubes y el clima, y pueden cruzar océanos enteros.",
            Svs + " 5572", "https://svs.gsfc.nasa.gov/5572/", "https://svs.gsfc.nasa.gov/vis/a000000/a005500/a005572/Aerosols_EarthRotating_1920x1080.mp4", 112.5),
        new("5436", "Dióxido de carbono global (DYAMOND)", "Gases de efecto invernadero", "2020",
            "El CO₂ de la atmósfera en alta resolución: de dónde sale y cómo lo mueven los vientos.",
            "Mapa mundial donde las plumas de dióxido de carbono aparecen como columnas brillantes sobre ciudades, industrias e incendios, y se estiran con los vientos sobre los océanos.",
            "Simulación DYAMOND del modelo GEOS. El CO₂ es el principal gas de efecto invernadero; las plumas muestran fuentes humanas y naturales y cómo la circulación las mezcla.",
            Svs + " 5436", "https://svs.gsfc.nasa.gov/5436/", "https://svs.gsfc.nasa.gov/vis/a000000/a005400/a005436/soscomp_1-29-2025a_1024p30.mp4", 27.2),
        new("5554", "Metano atmosférico por origen", "Gases de efecto invernadero", "2020",
            "El metano coloreado según de dónde viene: agricultura, industria, incendios y humedales.",
            "Mapa mundial con nubes de metano de varios colores, uno por origen: agricultura y ganadería, combustibles fósiles e industria, incendios y humedales naturales.",
            "El metano calienta unas 80 veces más que el CO₂ en 20 años. Separarlo por origen ayuda a decidir dónde reducir emisiones.",
            Svs + " 5554", "https://svs.gsfc.nasa.gov/5554/", "https://svs.gsfc.nasa.gov/vis/a000000/a005500/a005554/edgeRerender_five_6-30-2025a_1024p30_h264.mp4", 42.0),
        new("5624", "Pronóstico global de humo de incendios", "Incendios", "Actualizado a diario",
            "El humo de los incendios del planeta en los próximos días, según el pronóstico de la NASA.",
            "Mapa mundial oscuro donde el humo aparece en amarillos, naranjas y rojos según su intensidad, saliendo de las zonas con incendios y viajando con el viento.",
            "Pronóstico GEOS del humo de incendios forestales. El humo puede degradar la calidad del aire a miles de kilómetros de las llamas.",
            Svs + " 5624", "https://svs.gsfc.nasa.gov/5624/", "https://svs.gsfc.nasa.gov/vis/a000000/a005600/a005624/GMAO_global_smoke_forecast_4096x2048.mp4", 16.6),
        new("5616", "ICESat-2: el planeta medido con láser", "Hielo y nivel del mar", "2018–2025",
            "Las trayectorias del láser del satélite ICESat-2 que miden la altura del hielo, el mar y los bosques.",
            "Globo terráqueo que se va cubriendo de líneas de colores: cada línea es una pasada del láser de ICESat-2 que mide alturas de hielo, océano, bosques y ciudades.",
            "ICESat-2 mide la altura de la superficie con pulsos láser. Sus datos muestran cuánto hielo pierden Groenlandia y la Antártida y cómo eso eleva el nivel del mar.",
            Svs + " 5616", "https://svs.gsfc.nasa.gov/5616/", "https://svs.gsfc.nasa.gov/vis/a000000/a005600/a005616/icesat2_global_data_1080p60.mp4", 38.5),

        // ---------- NASA Earth Observatory: mapas globales mensuales de satélite ----------
        new("totalRainfall", "Lluvia total", "Agua", EoMensual,
            "Cuánta lluvia cae en cada lugar del planeta, mes a mes.",
            "Mapa mundial mensual de lluvia: azules y verdes intensos en la franja ecuatorial y las zonas monzónicas, colores pálidos en los desiertos. La franja de lluvias se desplaza al norte y al sur con las estaciones.",
            "Estimaciones de lluvia por satélite (misiones TRMM y GPM). Muestran el cinturón de lluvias tropicales que sigue al sol y los monzones de Asia y África.",
            Eo, EoUrl),
        new("waterVapor", "Vapor de agua", "Agua", EoMensual,
            "La humedad de la atmósfera: dónde el aire lleva más agua.",
            "Mapa mundial mensual del vapor de agua en la atmósfera: tonos intensos sobre los trópicos húmedos y tonos claros sobre los desiertos y los polos.",
            "Medido por el sensor MODIS. El vapor de agua es el gas de efecto invernadero más abundante y aumenta cuando el aire se calienta.",
            Eo, EoUrl),
        new("cloudFraction", "Nubosidad", "Agua", EoMensual,
            "Qué parte del cielo está cubierta de nubes en cada lugar del mundo.",
            "Mapa mundial mensual de la fracción de nubes: blancos donde el cielo casi siempre está cubierto, como los océanos del sur, y oscuros en los desiertos despejados.",
            "Medido por MODIS. Las nubes enfrían el planeta al reflejar la luz del sol y lo calientan al atrapar el calor; su cambio es una gran incógnita del clima.",
            Eo, EoUrl),
        new("snowCover", "Cobertura de nieve", "Hielo y nivel del mar", EoMensual,
            "Dónde hay nieve en la superficie, mes a mes.",
            "Mapa mundial mensual de nieve: el blanco avanza sobre Norteamérica, Europa y Asia en invierno y se retira en verano; los Andes y el Himalaya conservan nieve todo el año.",
            "Medido por MODIS. La nieve refleja la luz solar y guarda el agua que luego alimenta ríos; su retroceso adelantado es una señal del calentamiento.",
            Eo, EoUrl),
        new("seaSurfaceTemperature", "Temperatura del mar", "Océano", EoMensual,
            "Qué tan caliente está la superficie del océano en cada lugar.",
            "Mapa mundial mensual de la temperatura del mar: rojos y naranjas en los trópicos, azules y morados hacia los polos. Frente a Perú aparece una franja más fría, la corriente de Humboldt.",
            "Medida por satélite. La temperatura del mar controla la evaporación, las lluvias y fenómenos como El Niño.",
            Eo, EoUrl),
        new("seaSurfaceTemperatureAnomaly", "Anomalía de la temperatura del mar", "Océano", EoMensual,
            "Dónde el mar está más caliente o más frío que lo normal para ese mes.",
            "Mapa mundial mensual de anomalías: rojos donde el mar está más caliente de lo habitual y azules donde está más frío. Durante El Niño aparece una gran mancha roja a lo largo del Pacífico ecuatorial.",
            "La anomalía compara cada mes con su promedio de largo plazo. Es la forma más clara de ver El Niño y La Niña.",
            Eo, EoUrl),
        new("chlorophyllConcentration", "Clorofila en el océano", "Océano", EoMensual,
            "Dónde el océano tiene más fitoplancton, la base de la vida marina.",
            "Mapa mundial mensual de clorofila: verdes y amarillos en las costas y las zonas frías ricas en nutrientes, como frente a Perú; azules y morados en el centro de los océanos.",
            "Medida por MODIS a partir del color del agua. El fitoplancton produce cerca de la mitad del oxígeno del planeta y sostiene la pesca peruana.",
            Eo, EoUrl),
        new("landSurfaceTemperature", "Temperatura de la superficie terrestre", "Temperatura", EoMensual,
            "Qué tan caliente está el suelo durante el día, mes a mes.",
            "Mapa mundial mensual de la temperatura del suelo de día: amarillos y rojos en los desiertos, azules en las zonas frías; el calor se desplaza de hemisferio con las estaciones.",
            "Medida por MODIS. La temperatura del suelo influye en las sequías, los cultivos y la salud de las personas.",
            Eo, EoUrl),
        new("landSurfaceTemperatureAnomaly", "Anomalía de la temperatura terrestre", "Temperatura", EoMensual,
            "Dónde el suelo estuvo más caliente o más frío que lo normal ese mes.",
            "Mapa mundial mensual de anomalías de temperatura del suelo: rojos donde hizo más calor de lo habitual y azules donde hizo más frío. Las olas de calor aparecen como manchas rojas intensas.",
            "Compara cada mes con su promedio de largo plazo. Las anomalías cálidas cada vez más frecuentes son una señal del cambio climático.",
            Eo, EoUrl),
        new("netRadiation", "Radiación neta", "Temperatura", EoMensual,
            "El balance de energía de la Tierra: cuánta energía del sol entra y cuánta sale.",
            "Mapa mundial mensual del balance de radiación: tonos cálidos donde la Tierra gana energía (trópicos y el hemisferio en verano) y fríos donde la pierde (polos y el hemisferio en invierno).",
            "Medida por el instrumento CERES. El desbalance entre la energía que entra y la que sale es lo que calienta el planeta.",
            Eo, EoUrl),
        new("vegetation", "Vegetación (NDVI)", "Tierra", EoMensual,
            "Qué tan verde y activa está la vegetación en cada lugar.",
            "Mapa mundial mensual del índice de vegetación: verdes intensos en la Amazonía, el Congo y Asia del sudeste; marrones en los desiertos. El verde del hemisferio norte crece en primavera y se apaga en otoño.",
            "Índice NDVI de MODIS. Permite ver estaciones de cultivo, sequías y deforestación.",
            Eo, EoUrl),
        new("fire", "Incendios activos", "Incendios", EoMensual,
            "Dónde hay fuego en la superficie del planeta, mes a mes.",
            "Mapa mundial mensual de incendios: puntos amarillos, naranjas y rojos que se mueven con las estaciones de quema, desde la sabana africana hasta la Amazonía y el sudeste asiático.",
            "Detectados por MODIS. Muchos incendios son quemas agrícolas; su humo afecta la calidad del aire y libera carbono.",
            Eo, EoUrl),
        new("carbonMonoxide", "Monóxido de carbono", "Aire", EoMensual,
            "La contaminación por monóxido de carbono de incendios y combustión.",
            "Mapa mundial mensual de monóxido de carbono: nubes amarillas y rojas sobre las zonas de quema en África y Sudamérica y sobre las regiones industriales, que el viento arrastra sobre los océanos.",
            "Medido por el instrumento MOPITT. El monóxido de carbono revela de dónde sale la contaminación y cómo viaja entre continentes.",
            Eo, EoUrl),
        new("aerosolOpticalDepth", "Aerosoles (espesor óptico)", "Aire", EoMensual,
            "Cuántas partículas en el aire (polvo, humo, contaminación) bloquean la luz del sol.",
            "Mapa mundial mensual de aerosoles: amarillos y rojos sobre el Sahara, el Medio Oriente, la India, China y las zonas de quema; tonos claros en el aire limpio del océano.",
            "Medido por MODIS. Un espesor óptico alto significa aire cargado de partículas, con efectos en la salud y en el clima.",
            Eo, EoUrl),
        new("aerosolSize", "Tamaño de los aerosoles", "Aire", EoMensual,
            "Si las partículas del aire son grandes (polvo, sal marina) o finas (humo, contaminación).",
            "Mapa mundial mensual del tamaño de los aerosoles: un color para las partículas grandes, como el polvo del desierto y la sal marina, y otro para las finas, como el humo y la contaminación urbana.",
            "Medido por MODIS. Las partículas finas suelen venir de actividades humanas y son las más dañinas para respirar.",
            Eo, EoUrl),
    ];

    readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    readonly Lazy<Dictionary<string, EicHoja>> _hojas = new(() => LoadHojas(env, log));
    string CacheDir => config["Eic:CacheDir"] is { Length: > 0 } d ? d : Path.Combine(env.ContentRootPath, "App_Data", "eic");

    sealed record HojaJson(string File, int Frames, int Cols, int Rows, int W, int H, double Duration);
    static Dictionary<string, EicHoja> LoadHojas(IWebHostEnvironment env, ILogger log)
    {
        string path = Path.Combine(env.WebRootPath ?? "wwwroot", "eic", "index.json");
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, HojaJson>>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            return raw.ToDictionary(kv => kv.Key, kv => new EicHoja($"eic/{kv.Value.File}", kv.Value.Frames, kv.Value.Cols, kv.Value.Rows, kv.Value.W, kv.Value.H, kv.Value.Duration));
        }
        catch (Exception ex) { log.LogWarning("EIC: sin hojas de cuadros ({Msg}); se usarán los videos de SVS", ex.Message); return []; }
    }

    public static Item? Find(string id) => Catalogo.FirstOrDefault(i => i.Id == id);
    public string PathOf(Item i) => Path.Combine(CacheDir, $"{i.Id}.mp4");

    /// <summary>Solo se listan las visualizaciones que se pueden escuchar: con hoja de cuadros o con video de respaldo.</summary>
    public IEnumerable<EicItem> Disponibles() => Catalogo
        .Where(i => _hojas.Value.ContainsKey(i.Id) || i.VideoUrl is not null)
        .Select(ToDto);

    public EicItem ToDto(Item i)
    {
        var hoja = _hojas.Value.GetValueOrDefault(i.Id);
        return new(i.Id, i.Titulo, i.Tema, i.Periodo, i.Descripcion, i.Alt, i.Contexto, i.Fuente, i.FuenteUrl,
                   i.Mb, hoja is not null || File.Exists(PathOf(i)), hoja, EicIngles.Textos.GetValueOrDefault(i.Id));
    }

    /// <summary>Límite de la caché de videos en MB (<c>Eic:MaxCacheMb</c>, 0 = sin límite): borra los usados hace más tiempo.</summary>
    void MakeRoom(double neededMb)
    {
        if (!double.TryParse(config["Eic:MaxCacheMb"], System.Globalization.CultureInfo.InvariantCulture, out double max) || max <= 0) return;
        var files = new DirectoryInfo(CacheDir).GetFiles("*.mp4").OrderBy(f => f.LastAccessTimeUtc).ToList();
        double used = files.Sum(f => f.Length) / 1e6;
        foreach (var f in files)
        {
            if (used + neededMb <= max) break;
            try { used -= f.Length / 1e6; f.Delete(); log.LogInformation("EIC: caché llena, se borra {File}", f.Name); }
            catch (IOException) { /* en uso: se intenta con el siguiente */ }
        }
    }

    /// <summary>Respaldo sin hoja: descarga el video de SVS a la caché si falta (una descarga a la vez por visualización).</summary>
    public async Task<string> EnsureAsync(Item i, CancellationToken ct)
    {
        if (i.VideoUrl is null) throw new IOException($"La visualización {i.Id} no tiene video de respaldo.");
        string path = PathOf(i);
        if (File.Exists(path)) return path;
        var gate = _locks.GetOrAdd(i.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (File.Exists(path)) return path;
            Directory.CreateDirectory(CacheDir);
            MakeRoom(i.Mb);
            string part = path + ".part";
            log.LogInformation("EIC: descargando {Id} ({Mb} MB)…", i.Id, i.Mb);
            using var client = http.CreateClient("eic");
            HttpResponseMessage resp;
            try { resp = await client.GetAsync(i.VideoUrl, HttpCompletionOption.ResponseHeadersRead, ct); }
            catch (HttpRequestException ex) when (ex.InnerException is System.Security.Authentication.AuthenticationException)
            {
                // Windows 10 (Schannel) no negocia el TLS que exige svs.gsfc.nasa.gov ("TLS alert: ProtocolVersion").
                // SVS también sirve sus archivos por HTTP; son datos públicos y se comprueba que llegue un video.
                log.LogWarning("EIC: TLS con SVS no disponible en este sistema ({Msg}); se descarga por HTTP", ex.InnerException.Message);
                resp = await client.GetAsync("http://" + i.VideoUrl["https://".Length..], HttpCompletionOption.ResponseHeadersRead, ct);
            }
            using (resp)
            {
                resp.EnsureSuccessStatusCode();
                if (resp.Content.Headers.ContentType?.MediaType is { } mt && !mt.StartsWith("video/"))
                    throw new IOException($"SVS devolvió {mt} en lugar de un video.");
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(part);
                await src.CopyToAsync(dst, ct);
            }
            File.Move(part, path, overwrite: true);
            log.LogInformation("EIC: {Id} en caché", i.Id);
            return path;
        }
        finally { gate.Release(); }
    }
}

public static class EicEndpoints
{
    public static void MapEic(this WebApplication app)
    {
        var api = app.MapGroup("/api/eic");
        api.MapGet("/catalogo", (EicService s) => Results.Ok(s.Disponibles()));
        // Solo para visualizaciones sin hoja de cuadros: prepara la caché (la primera vez descarga el video de SVS).
        api.MapPost("/preparar/{id}", async (string id, EicService s, CancellationToken ct) =>
        {
            if (EicService.Find(id) is not { } i) return Results.NotFound();
            try { await s.EnsureAsync(i, ct); return Results.Ok(s.ToDto(i)); }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            { return Results.Problem($"No se pudo descargar la visualización {id} de NASA SVS: {ex.Message}", statusCode: 502); }
        });
        api.MapGet("/video/{id}", async (string id, EicService s, CancellationToken ct) =>
        {
            if (EicService.Find(id) is not { } i) return Results.NotFound();
            string path = await s.EnsureAsync(i, ct);
            return Results.File(path, "video/mp4", enableRangeProcessing: true);
        });
    }
}
