using System.Globalization;
using SONORA.Client.Models;

namespace SONORA.Tendencias;

/// <summary>
/// API de tendencias climáticas de Perú: <c>/api/tendencias/catalogo</c>, <c>serie</c> (mismo formato que
/// <c>/api/biomas/serie</c>, así el cliente lo sonifica igual) y <c>cuadro</c> (PNG de un año).
/// </summary>
public static class TendenciasEndpoints
{
    static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
    static readonly string[] RolEs = ["melodía", "ritmo", "bajo", "armonía"];

    public static void MapTendencias(this WebApplication app)
    {
        var api = app.MapGroup("/api/tendencias");

        api.MapGet("/catalogo", (TendenciasService s) =>
        {
            var d = s.Datos;
            if (d is null)
                return Results.Ok(new TendenciasCatalog("error", "Falta tendencias_peru.json (expórtalo con TrendDetective.Sonora).", "", [], []));
            var vars = d.Variables.ToDictionary(v => (v.Fuente, v.Codigo));
            var regiones = d.Regiones.Select(r => new TendenciasRegion(r.Id, r.Nombre, r.Bioma, r.Lat, r.Lon,
                r.Series.Select(x =>
                {
                    var v = vars.GetValueOrDefault((x.Fuente, x.Variable));
                    return new TendenciasVariable(x.Fuente, x.Variable, v?.Nombre ?? x.Variable, v?.Que ?? "", v?.Unidad ?? "", v?.NotaUnidad ?? "",
                        x.Tendencia, x.SenDecada, x.P, x.Anios[0], x.Anios[^1],
                        x.Red is { } p ? p.PSube + p.PBaja : null, x.Red?.PSube, x.Red?.PBaja, x.Red?.RegionNoVista ?? false);
                }).ToList())).ToList();
            var fuentes = d.Fuentes.Select(f => new TendenciasFuente(f.Id, f.Nombre, f.AnioMin, f.AnioMax, f.AnioProvisional,
                f.Red is { } m ? new TendenciasRed(m.Nombre, m.Entrenada, m.Accuracy, m.MacroF1, m.Top3) : null)).ToList();
            return Results.Ok(new TendenciasCatalog("listo", null, d.Metodo, fuentes, regiones));
        });

        api.MapGet("/serie", (TendenciasService s, string region, string fuente, string vars) =>
        {
            if (Pick(s, region, fuente, vars) is not { } p) return Bad();
            var years = TendenciasService.CommonYears(p.Series);
            if (years.Count < 3) return Results.BadRequest(new { error = "Esas variables no tienen años en común suficientes." });
            return Results.Ok(Info(s, p.Region, fuente, p.Series, years, vars));
        });

        api.MapGet("/cuadro", (TendenciasService s, HttpContext http, string region, string fuente, string vars, int anio) =>
        {
            if (Pick(s, region, fuente, vars) is not { } p) return Bad();
            http.Response.Headers.CacheControl = "public, max-age=86400";
            return Results.File(s.Frame(region, fuente, p.Series, anio), "image/png");
        });
    }

    static IResult Bad() => Results.BadRequest(new { error = "Región, fuente o variables desconocidas (elige de 1 a 4 variables del catálogo)." });

    static (TendenciasService.Region Region, List<TendenciasService.Serie> Series)? Pick(TendenciasService s, string region, string fuente, string vars)
    {
        var r = s.Datos?.Regiones.FirstOrDefault(x => x.Id == region);
        if (r is null) return null;
        var codes = vars.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();
        if (codes.Count is < 1 or > 4) return null;
        var series = codes.Select(c => r.Series.FirstOrDefault(x => x.Fuente == fuente && x.Variable == c)).OfType<TendenciasService.Serie>().ToList();
        return series.Count == codes.Count ? (r, series) : null;
    }

    static string N(double v, string fmt = "0.##") => v.ToString(fmt, Es);

    /// <summary>"sube 0,081 °C por década, p = 0,007" o "sin tendencia clara, p = 0,32".</summary>
    static string Frase(TendenciasService.Serie x, TendenciasService.Variable? v)
    {
        string u = v?.Unidad switch { null or "" => "", "0-1" => " (en la escala de 0 a 1)", var un => " " + un };
        string p = x.P < 0.001 ? "p < 0,001" : $"p = {N(x.P, "0.###")}";
        // 3 cifras significativas: las pendientes van de 0,0004 (humedad del suelo) a cientos (NO₂).
        string sen = Math.Abs(x.SenDecada).ToString(Math.Abs(x.SenDecada) >= 100 ? "N0" : "G3", Es);
        // Datos con pocos decimales (p. ej. humedad del suelo): la mediana de Sen puede dar 0 exacto aunque
        // Mann-Kendall vea una tendencia clara. "baja 0 por década" confunde: se dice que es constante pero muy lenta.
        if (x.SenDecada == 0 && x.Tendencia is "sube" or "baja")
            return $"{x.Tendencia} de forma constante pero muy lenta (menos que el redondeo del dato por década), {p}";
        return x.Tendencia switch
        {
            "sube" => $"sube {sen}{u} por década, {p}",
            "baja" => $"baja {sen}{u} por década, {p}",
            _ => $"sin tendencia clara, {p}",
        };
    }

    static BiomasInfo Info(TendenciasService s, TendenciasService.Region r, string fuente, List<TendenciasService.Serie> series, List<int> years, string vars)
    {
        var d = s.Datos!;
        var f = d.Fuentes.First(x => x.Id == fuente);
        var vinfo = d.Variables.Where(v => v.Fuente == fuente).ToDictionary(v => v.Codigo);
        string Name(string code) => vinfo.GetValueOrDefault(code)?.Nombre ?? code;

        string title = $"Tendencias de {r.Nombre} · {years[0]}–{years[^1]}";
        var capas = series.Select((x, k) => $"{RolEs[k]}: {Name(x.Variable).ToLowerInvariant()} ({Frase(x, vinfo.GetValueOrDefault(x.Variable))})");
        bool prov = f.AnioProvisional is int py && years.Contains(py);
        string alt = $"Serie de {years.Count} años, de {years[0]} a {years[^1]}, del clima de {r.Nombre}, Perú. Cada paso es un año. " +
                     string.Join("; ", capas) + ". Cuanto más alto el valor de ese año, más larga la barra y más aguda la nota." +
                     (prov ? $" {f.AnioProvisional} es provisional: solo los meses ya publicados por la NASA." : "");

        var notas = new List<string>
        {
            $"Datos de {f.Nombre}, promedio de una caja de 2° × 2° (unos 220 km de lado) centrada en {N(Math.Abs(r.Lat), "0.#")}°S {N(Math.Abs(r.Lon), "0.#")}°O. " +
            "Cada año es la media anual sin estacionalidad (se quita el ciclo verano-invierno), calculada por TrendDetective. Tendencia: prueba de Mann-Kendall estacional y pendiente de Sen.",
        };
        if (f.Red is { } red)
        {
            var sug = series.Where(x => x.Red is not null)
                .Select(x => $"{Name(x.Variable).ToLowerInvariant()} {N((x.Red!.PSube + x.Red.PBaja) * 100, "0")} %");
            notas.Add($"El orden de las capas lo sugiere la red neuronal de TrendDetective, entrenada con 199 regiones del mundo " +
                      $"(acierta el {N(red.Accuracy * 100, "0")} % en regiones que no vio). Probabilidad de que cada variable esté cambiando: {string.Join(", ", sug)}." +
                      (series.Any(x => x.Red?.RegionNoVista == true) ? $" {r.Nombre} fue una región de prueba: la red nunca vio sus datos." : ""));
        }
        foreach (var x in series)
            if (vinfo.GetValueOrDefault(x.Variable) is { } v && v.Que.Length > 0)
                notas.Add($"{v.Nombre}: {v.Que}" + (v.NotaUnidad.Length > 0 ? $" Unidad: {v.NotaUnidad}." : ""));

        var palette = series.Select((x, k) => new PaletteColor(TendenciasService.Capas[k].Role, Name(x.Variable), TendenciasService.Capas[k].Rgb, 60)).ToList();
        string q = $"region={Uri.EscapeDataString(r.Id)}&fuente={Uri.EscapeDataString(fuente)}&vars={Uri.EscapeDataString(vars)}";
        // Leyenda: valor de cada variable en cada año (con unidad) y su tendencia de largo plazo.
        string Valor(TendenciasService.Serie x, int y)
        {
            int i = x.Anios.IndexOf(y);
            if (i < 0 || !double.IsFinite(x.Valores[i])) return "sin dato";
            double v = x.Valores[i];
            string u = vinfo.GetValueOrDefault(x.Variable)?.Unidad switch { null or "" => "", "0-1" => " (escala 0–1)", var un => " " + un };
            return v.ToString(Math.Abs(v) >= 1000 ? "N0" : "G3", Es) + u;
        }
        var frames = years.Select(y => new BiomasFrame(y, $"api/tendencias/cuadro?{q}&anio={y}",
            series.Select(x => Valor(x, y)).ToList(), f.AnioProvisional == y)).ToList();
        var legend = series.Select(x => $"Tendencia {x.Anios[0]}–{x.Anios[^1]}: {Frase(x, vinfo.GetValueOrDefault(x.Variable))}").ToList();
        var box = new GeoBox(r.Caja.LonMin, r.Caja.LonMax, r.Caja.LatMin, r.Caja.LatMax);
        return new BiomasInfo("tendencias", null, "peru-" + r.Id, title, alt, string.Join(" ", notas), box,
            [new GeoPlace(r.Nombre, r.Lon, r.Lat)], palette, frames[0].Url, frames[^1].Url, frames, legend);
    }
}
