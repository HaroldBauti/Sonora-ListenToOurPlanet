namespace SONORA.Client.Services;

/// <summary>Estado visible de SONORA (chip del asistente y anuncio breve para lectores de pantalla).</summary>
public enum AppState { Idle, Listening, Processing, Loading, Playing, Paused }

/// <summary>
/// Decide el estado a partir de las banderas de la app y avisa solo cuando cambia, para no repetir el anuncio
/// en cada paso de la música (OnStep llega varias veces por segundo mientras suena).
/// </summary>
public sealed class StatusAnnouncer
{
    AppState last = AppState.Idle;

    public AppState Current => last;

    /// <summary>Prioridad: escuchar el micrófono, procesar una orden, cargar, reproducir, pausa (con algo cargado), nada.</summary>
    public static AppState Compute(bool listening, bool processing, bool loading, bool hasContent, bool playing) =>
        listening ? AppState.Listening
        : processing ? AppState.Processing
        : loading ? AppState.Loading
        : playing ? AppState.Playing
        : hasContent ? AppState.Paused
        : AppState.Idle;

    public static string Label(AppState s) => s switch
    {
        AppState.Listening => "Escuchando",
        AppState.Processing => "Procesando",
        AppState.Loading => "Cargando",
        AppState.Playing => "Reproduciendo",
        AppState.Paused => "En pausa",
        _ => ""
    };

    /// <summary>Devuelve el texto a anunciar si el estado cambió (null si es el mismo o si pasa a reposo).</summary>
    public string? Update(AppState s)
    {
        if (s == last) return null;
        last = s;
        return s == AppState.Idle ? null : Label(s);
    }
}
