namespace SONORA.Client.Models;

/// <summary>Visualización del Jukebox del EIC. <see cref="Hoja"/>: cuadros ya preparados en <c>wwwroot/eic</c> (abre al
/// instante); si es null se usa el video de NASA SVS (<see cref="EnCache"/>: ya descargado en el servidor).</summary>
public sealed record EicItem(string Id, string Titulo, string Tema, string Periodo, string Descripcion, string Alt, string Contexto,
                             string Fuente, string FuenteUrl, double Mb, bool EnCache, EicHoja? Hoja, EicEn? En = null);

/// <summary>Textos en inglés de una visualización (modo English).</summary>
public sealed record EicEn(string Titulo, string Periodo, string Descripcion, string Alt, string Contexto);

/// <summary>Hoja de cuadros WebP: <see cref="Cuadros"/> cuadros de <see cref="Ancho"/>×<see cref="Alto"/> en una cuadrícula de
/// <see cref="Cols"/> columnas, tomados del video original de <see cref="Duracion"/> segundos.</summary>
public sealed record EicHoja(string Url, int Cuadros, int Cols, int Filas, int Ancho, int Alto, double Duracion);
