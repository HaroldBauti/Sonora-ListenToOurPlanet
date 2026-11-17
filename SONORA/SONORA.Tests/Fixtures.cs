using SONORA.Client.Models;
using SONORA.Client.Services;

namespace SONORA.Tests;

/// <summary>Catálogos pequeños con la misma forma que los reales (EIC, biomas, tendencias y lugares del mundo).</summary>
static class Fixtures
{
    public static GuideFlow Flow()
    {
        var f = new GuideFlow();
        f.Data.Eic =
        [
            new("sample", "Aerosoles GEOS · 19 ago 2025", "Aire", "19 ago 2025", "NASA SVS 5669", "Polvo, humo, sulfatos y sal marina."),
            new("5603", "Anomalías de temperatura global, 1880–2025", "Temperatura", "1880–2025", "NASA SVS 5603", "Mapa del mundo año a año."),
            new("seaSurfaceTemperature", "Temperatura del mar", "Océano", "Mapas mensuales", "NASA Earth Observatory", "Temperatura del océano."),
            new("seaSurfaceTemperatureAnomaly", "Anomalía de la temperatura del mar", "Océano", "Mapas mensuales", "NASA Earth Observatory", "Anomalías."),
            new("landSurfaceTemperature", "Temperatura de la superficie terrestre", "Temperatura", "Mapas mensuales", "NASA Earth Observatory", "Suelo."),
            new("totalRainfall", "Lluvia total", "Agua", "Mapas mensuales", "NASA Earth Observatory", "Lluvia."),
            new("waterVapor", "Vapor de agua", "Agua", "Mapas mensuales", "NASA Earth Observatory", "Humedad."),
            new("cloudFraction", "Nubosidad", "Agua", "Mapas mensuales", "NASA Earth Observatory", "Nubes."),
            new("fire", "Incendios activos", "Incendios", "Mapas mensuales", "NASA Earth Observatory", "Fuego."),
            new("vegetation", "Vegetación (NDVI)", "Tierra", "Mapas mensuales", "NASA Earth Observatory", "Verdor."),
            new("carbonMonoxide", "Monóxido de carbono", "Aire", "Mapas mensuales", "NASA Earth Observatory", "CO."),
            new("snowCover", "Cobertura de nieve", "Hielo y nivel del mar", "Mapas mensuales", "NASA Earth Observatory", "Nieve."),
        ];
        var pal = new List<PaletteColor> { new("melody", "Selva", [60, 200, 110], 90), new("rhythm", "Agricultura", [255, 107, 91], 90),
                                           new("bass", "Andes", [184, 149, 106], 90), new("pad", "Costa", [242, 211, 91], 90) };
        f.Data.Biomas = new BiomasCatalog("listo", null, 2001, 2024,
            [new("biomas", "Biomas", true, "Bioma más probable.", pal), new("cambio_bosque", "Cambio de bosque 2001–2024", false, "Pérdida y ganancia.", pal),
             new("tendencia_ndvi", "Tendencia del verdor (NDVI)", false, "Verdor.", pal), new("anomalia_ndvi", "Anomalía del verdor (NDVI)", true, "Anomalía.", pal)],
            [new("peru", "Perú completo"), new("madre-de-dios", "Madre de Dios (minería aurífera, Interoceánica)"),
             new("ucayali", "Ucayali y Huánuco (Pucallpa, Aguaytía)"), new("san-martin", "San Martín (Tarapoto, Moyobamba)"),
             new("cordillera-blanca", "Cordillera Blanca (glaciares)"), new("costa-norte", "Costa norte: Piura y Tumbes (bosque seco, El Niño)"),
             new("altiplano", "Altiplano (Puno, Titicaca)"), new("loreto", "Loreto (Iquitos)")]);
        TendenciasVariable Var(string fuente, string code, string name, string tend) =>
            new(fuente, code, name, "", "", "", tend, 0.1, 0.01, 1981, 2025, 0.8, 0.7, 0.1, false);
        var vars = new List<TendenciasVariable>
        {
            Var("power", "T2M", "Temperatura media", "sube"), Var("power", "PRECTOT", "Lluvia", "sin"), Var("power", "RH2M", "Humedad", "baja"),
            Var("power", "WS2M", "Viento", "sin"), Var("power", "CLOUD_AMT", "Nubes", "baja"), Var("giovanni", "OMI_O3", "Ozono", "sube"),
        };
        f.Data.Tendencias = new TendenciasCatalog("listo", null, "",
            [new("power", "NASA POWER", 1981, 2026, 2026, null), new("giovanni", "NASA Giovanni", 2000, 2025, null, null)],
            [new("piura", "Piura y Sechura (costa norte)", "bosque seco", -5.2, -80.6, vars),
             new("lima", "Lima (costa central)", "desierto costero", -12.0, -77.0, vars)]);
        f.Data.Places = [new("rondonia", "Amazonía de Brasil: Rondônia (deforestación en espina de pez)"), new("sahara", "Sahara y Sahel (desierto que avanza hacia el sur)"),
                         new("groenlandia", "Groenlandia (hielo)")];
        return f;
    }

    public static AnalysisInfo Analysis(string source = "biomas", string scan = "linear") => new(
        "image", "Biomas · Ucayali y Huánuco · 2010", source, 4, 8,
        [new("melody", "Selva", [60, 200, 110], 0.6), new("rhythm", "Agricultura y urbano", [255, 107, 91], 0.1),
         new("bass", "Andes", [184, 149, 106], 0.2), new("pad", "Costa", [242, 211, 91], 0.1)],
        new() { ["melody"] = [0.9, 0.5, 0.2, 0.1], ["rhythm"] = [0, 0.3, 0.6, 0.1], ["bass"] = [0.1, 0.2, 0.8, 0.4], ["pad"] = [0.5, 0.5, 0.5, 0.5] },
        ["75° O", "74° O", "73° O", "72° O"], [null, "Pucallpa", null, null],
        "Contexto científico del mapa.", "Mapa de biomas de Ucayali en 2010. La selva domina el este.", 0, [], [], scan, null,
        new GeoBox(-76, -72, -10, -7));
}
