using System.Globalization;
using BiomasPeru;
using SONORA.Client.Models;
using CoreAtlas = BiomasPeru.Atlas;

namespace SONORA.Peru;

/// <summary>API de mapas de biomas de Perú bajo demanda: <c>/api/biomas/catalogo</c>, <c>mapa</c>, <c>info</c> y <c>serie</c>.</summary>
public static class BiomasEndpoints
{
    const int AnioMin = CoreAtlas.Year0, AnioMax = CoreAtlas.Year0 + CoreAtlas.Years - 1;

    public static void MapBiomas(this WebApplication app)
    {
        var api = app.MapGroup("/api/biomas");

        api.MapGet("/catalogo", (BiomasAtlasService s) => Results.Ok(new BiomasCatalog(
            s.Estado, s.Error, AnioMin, AnioMax,
            [.. CoreAtlas.Tipos.Select(t => new BiomasTipo(t.Id, t.Nombre, t.UsaAnio, t.Descripcion, Palette(t)))],
            [.. CoreAtlas.Regiones.Select(r => new BiomasRegion(r.Id, r.Nombre))])));

        api.MapGet("/mapa", (BiomasAtlasService s, HttpContext http, string tipo, int? anio, string? region, string? estilo) =>
        {
            if (Check(s, http, tipo, region) is { } problem) return problem;
            var (png, _) = s.Get(tipo, Year(anio), region ?? "peru", estilo != "vista");
            http.Response.Headers.CacheControl = "public, max-age=3600";
            return Results.File(png, "image/png");
        });

        api.MapGet("/info", (BiomasAtlasService s, HttpContext http, string tipo, int? anio, string? region) =>
            Check(s, http, tipo, region) ?? Results.Ok(Info(s, tipo, anio, region ?? "peru", serie: false)));

        api.MapGet("/serie", (BiomasAtlasService s, HttpContext http, string tipo, string? region) =>
        {
            if (Check(s, http, tipo, region) is { } problem) return problem;
            if (!CoreAtlas.Tipo(tipo)!.UsaAnio) return Results.BadRequest(new { error = $"El mapa «{tipo}» ya resume 2001–2024; no tiene serie por año." });
            return Results.Ok(Info(s, tipo, null, region ?? "peru", serie: true));
        });
    }

    static int Year(int? anio) => Math.Clamp(anio ?? AnioMax, AnioMin, AnioMax);

    static IResult? Check(BiomasAtlasService s, HttpContext http, string tipo, string? region)
    {
        if (CoreAtlas.Tipo(tipo) is null)
            return Results.BadRequest(new { error = $"Tipo desconocido. Usa: {string.Join(", ", CoreAtlas.Tipos.Select(t => t.Id))}." });
        if (region is not null && CoreAtlas.RegionPorId(region) is null)
            return Results.BadRequest(new { error = $"Región desconocida. Usa: {string.Join(", ", CoreAtlas.Regiones.Select(r => r.Id))}." });
        if (s.Atlas is null)
        {
            http.Response.Headers.RetryAfter = "5";
            return Results.Problem(s.Error is null ? $"El atlas de biomas se está preparando ({s.Estado})." : $"El atlas de biomas no está disponible: {s.Error}",
                                   statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        return null;
    }

    static List<PaletteColor> Palette(TipoMapa t) => [.. t.Paleta.Select(c => new PaletteColor(c.Role, c.Name, c.Rgb, c.Reach))];

    static string Url(string tipo, int? anio, string region, string estilo) =>
        $"api/biomas/mapa?tipo={tipo}{(anio is int a ? $"&anio={a}" : "")}&region={region}&estilo={estilo}";

    static BiomasInfo Info(BiomasAtlasService s, string tipoId, int? anio, string regionId, bool serie)
    {
        var tipo = CoreAtlas.Tipo(tipoId)!;
        var region = CoreAtlas.RegionPorId(regionId)!;
        int? year = tipo.UsaAnio && !serie ? Year(anio) : null;
        int renderYear = year ?? AnioMax;
        var (_, map) = s.Get(tipoId, renderYear, regionId, sonora: true);

        var es = CultureInfo.GetCultureInfo("es-ES");
        var st = s.Atlas!.Stats(region);
        string stats = st.Celdas == 0 ? "" :
            $" En esta zona, la probabilidad media de bosque pasó de {st.BosqueProb2001.ToString("P0", es)} en 2001 a {st.BosqueProb2024.ToString("P0", es)} en 2024; " +
            $"el verdor baja de forma significativa en el {st.PctNdviBajando.ToString("0.#", es)} % de la superficie y sube en el {st.PctNdviSubiendo.ToString("0.#", es)} %. " +
            $"Bioma dominante en 2024: {st.BiomaDominante2024.ToLowerInvariant()}.";

        string title = serie ? $"{tipo.Nombre} · {region.Nombre} · {AnioMin}–{AnioMax}"
                             : $"{tipo.Nombre} · {region.Nombre}{(year is int y ? $" · {y}" : "")}";
        string alt = serie
            ? $"Serie de {CoreAtlas.Years} mapas, uno por año de {AnioMin} a {AnioMax}: {tipo.Nombre.ToLowerInvariant()} de {region.Nombre}. Cada paso es un año."
            : CoreAtlas.AltText(tipo, region, year, map);
        var box = new SONORA.Client.Models.GeoBox(map.Box.LonMin, map.Box.LonMax, map.Box.LatMin, map.Box.LatMax);
        var places = CoreAtlas.Ciudades
            .Where(c => c.Lon >= box.LonMin && c.Lon <= box.LonMax && c.Lat >= box.LatMin && c.Lat <= box.LatMax)
            .Select(c => new GeoPlace(c.Nombre, c.Lon, c.Lat)).ToList();
        var frames = serie
            ? Enumerable.Range(AnioMin, CoreAtlas.Years).Select(y => new BiomasFrame(y, Url(tipoId, y, regionId, "sonora"))).ToList()
            : null;

        return new BiomasInfo(tipoId, year, regionId, title, alt, tipo.Contexto + stats, box, places, Palette(tipo),
                              Url(tipoId, year ?? (tipo.UsaAnio ? renderYear : null), regionId, "sonora"),
                              Url(tipoId, year ?? (tipo.UsaAnio ? renderYear : null), regionId, "vista"), frames);
    }
}
