namespace SONORA.Client.Models;

// Tendencias climáticas de Perú (datos y red neuronal de TrendDetective): /api/tendencias/*

/// <summary>Catálogo para la interfaz: fuentes de datos y, por región, las variables ordenadas por lo que sugiere la red.</summary>
public sealed record TendenciasCatalog(string Estado, string? Error, string Metodo, List<TendenciasFuente> Fuentes, List<TendenciasRegion> Regiones);

public sealed record TendenciasFuente(string Id, string Nombre, int AnioMin, int AnioMax, int? AnioProvisional, TendenciasRed? Red);

/// <summary>Red neuronal de TrendDetective que se usó para las sugerencias (métricas en regiones que no vio).</summary>
public sealed record TendenciasRed(string Nombre, string Entrenada, double Accuracy, double MacroF1, double Top3);

public sealed record TendenciasRegion(string Id, string Nombre, string Bioma, double Lat, double Lon, List<TendenciasVariable> Variables);

/// <summary>
/// Una serie de una región. <see cref="PTendencia"/> es lo que estima la red (null si no hay red para esa fuente);
/// <see cref="Tendencia"/> es el resultado real de Mann-Kendall con los datos.
/// </summary>
public sealed record TendenciasVariable(
    string Fuente, string Codigo, string Nombre, string Que, string Unidad, string NotaUnidad,
    string Tendencia, double SenDecada, double P, int AnioMin, int AnioMax,
    double? PTendencia, double? PSube, double? PBaja, bool RegionNoVista);
