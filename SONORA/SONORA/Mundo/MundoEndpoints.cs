using System.Globalization;
using SONORA.Client.Models;
using CoreBox = BiomasPeru.GeoBox;

namespace SONORA.Mundo;

/// <summary>API de cobertura terrestre mundial: <c>/api/mundo/mapa</c> (PNG), <c>info</c> y <c>serie</c>.
/// Devuelve el mismo formato que <c>/api/biomas</c>, así el cliente lo sonifica igual.</summary>
public static class MundoEndpoints
{
    const double MaxSpan = 40, MinSpan = 0.2;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture, Es = CultureInfo.GetCultureInfo("es-ES");

    public static void MapMundo(this WebApplication app)
    {
        var api = app.MapGroup("/api/mundo");

        api.MapGet("/mapa", async (MundoService s, HttpContext http, double lonMin, double lonMax, double latMin, double latMax, int? anio, CancellationToken ct) =>
        {
            if (Box(lonMin, lonMax, latMin, latMax) is not { } b) return BadBox();
            try
            {
                var r = await s.GetAsync(b, Year(anio), ct);
                http.Response.Headers.CacheControl = "public, max-age=86400";
                return Results.File(r.Png, "image/png");
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(); }
        });

        api.MapGet("/info", async (MundoService s, double lonMin, double lonMax, double latMin, double latMax, int? anio, string? nombre, CancellationToken ct) =>
        {
            if (Box(lonMin, lonMax, latMin, latMax) is not { } b) return BadBox();
            int y = Year(anio);
            try
            {
                var r = await s.GetAsync(b, y, ct);
                return Results.Ok(Info(b, y, Name(nombre, b), r.Shares, serie: false));
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { return Unavailable(); }
        });

        api.MapGet("/serie", (double lonMin, double lonMax, double latMin, double latMax, string? nombre) =>
            Box(lonMin, lonMax, latMin, latMax) is { } b ? Results.Ok(Info(b, null, Name(nombre, b), null, serie: true)) : BadBox());
    }

    static int Year(int? anio) => Math.Clamp(anio ?? MundoService.AnioMax, MundoService.AnioMin, MundoService.AnioMax);

    static CoreBox? Box(double lonMin, double lonMax, double latMin, double latMax)
    {
        double R(double v) => Math.Round(v, 2);
        if (!double.IsFinite(lonMin + lonMax + latMin + latMax)) return null;
        lonMin = R(Math.Max(-180, lonMin)); lonMax = R(Math.Min(180, lonMax)); latMin = R(Math.Max(-85, latMin)); latMax = R(Math.Min(85, latMax));
        double w = lonMax - lonMin, h = latMax - latMin;
        if (w < MinSpan || h < MinSpan || w > MaxSpan || h > MaxSpan) return null;
        return new CoreBox(lonMin, lonMax, latMin, latMax);
    }

    static IResult BadBox() => Results.BadRequest(new { error = $"El recuadro debe medir entre {MinSpan} y {MaxSpan} grados de lado. Acerca o aleja el mapa." });
    static IResult Unavailable() => Results.Problem("No se pudo obtener la cobertura terrestre de NASA GIBS. Inténtalo de nuevo en unos segundos.", statusCode: StatusCodes.Status503ServiceUnavailable);

    static string Coord(double lat, double lon) =>
        $"{Math.Abs(lat).ToString("0.#", Es)}°{(lat < 0 ? "S" : "N")} {Math.Abs(lon).ToString("0.#", Es)}°{(lon < 0 ? "O" : "E")}";

    static string Name(string? nombre, CoreBox b)
    {
        var clean = new string((nombre ?? "").Where(c => !char.IsControl(c) && c is not '<' and not '>').Take(80).ToArray()).Trim();
        return clean.Length > 0 ? clean : $"Zona {Coord((b.LatMin + b.LatMax) / 2, (b.LonMin + b.LonMax) / 2)}";
    }

    static string Q(CoreBox b) => string.Create(Inv, $"lonMin={b.LonMin}&lonMax={b.LonMax}&latMin={b.LatMin}&latMax={b.LatMax}");

    static BiomasInfo Info(CoreBox b, int? anio, string name, double[]? shares, bool serie)
    {
        int y = anio ?? MundoService.AnioMax;
        string title = serie ? $"Cobertura terrestre · {name} · {MundoService.AnioMin}–{MundoService.AnioMax}" : $"Cobertura terrestre · {name} · {y}";
        string alt;
        if (serie)
            alt = $"Serie de {MundoService.AnioMax - MundoService.AnioMin + 1} mapas anuales, de {MundoService.AnioMin} a {MundoService.AnioMax}, de la cobertura terrestre de {name}. Cada paso es un año.";
        else
        {
            var parts = MundoService.Capas.Select((c, k) => (c.Name, S: shares![k])).Where(p => p.S >= 0.005).OrderByDescending(p => p.S)
                .Select(p => $"{p.Name.ToLowerInvariant()} {Math.Round(p.S * 100)} %").ToList();
            alt = $"Cobertura terrestre de {name} en {y}, de oeste a este. Proporción de la tierra firme: " +
                  (parts.Count > 0 ? string.Join(", ", parts) : "casi todo agua o sin datos") + ". El agua queda en silencio.";
        }
        string context =
            $"Cobertura terrestre MODIS IGBP de la NASA (producto MCD12Q1, 500 m, anual), servida por NASA GIBS, entre {Coord(b.LatMax, b.LonMin)} y {Coord(b.LatMin, b.LonMax)}. " +
            "Las 17 clases IGBP se agrupan en 4 capas: bosques; cultivos, mosaicos y zonas urbanas; sabanas, matorrales, pastizales y humedales; desierto, suelo desnudo y hielo. " +
            "Fuera de Perú no se usa la red neuronal de SONORA, que está entrenada solo con datos de Perú.";
        var palette = MundoService.Capas.Select(c => new PaletteColor(c.Role, c.Name, c.Rgb, 90)).ToList();
        var box = new GeoBox(b.LonMin, b.LonMax, b.LatMin, b.LatMax);
        string img = $"api/mundo/mapa?{Q(b)}&anio={y}";
        var frames = serie
            ? Enumerable.Range(MundoService.AnioMin, MundoService.AnioMax - MundoService.AnioMin + 1).Select(a => new BiomasFrame(a, $"api/mundo/mapa?{Q(b)}&anio={a}")).ToList()
            : null;
        return new BiomasInfo("cobertura", serie ? null : y, "mundo", title, alt, context, box, [], palette, img, img, frames);
    }
}
