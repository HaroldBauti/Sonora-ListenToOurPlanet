using SONORA.Assistant;
using SONORA.Client.Models;
using SONORA.Components;
using SONORA.Peru;
using SONORA.Sonification;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddHttpClient<OpenAIAssistant>(c =>
{
    c.BaseAddress = new Uri("https://api.openai.com/v1/");
    c.Timeout = TimeSpan.FromSeconds(25);
});

// Atlas de biomas de Perú: se prepara en segundo plano al arrancar (datos NASA + modelo entrenado).
builder.Services.AddSingleton<BiomasAtlasService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<BiomasAtlasService>());

// Cobertura terrestre de todo el planeta bajo demanda (NASA GIBS, MODIS IGBP).
builder.Services.AddHttpClient("gibs", c =>
{
    c.Timeout = TimeSpan.FromSeconds(40);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("SONORA/1.0");
});
builder.Services.AddSingleton<SONORA.Mundo.MundoService>();

// Tendencias del clima en Perú: datos y red neuronal de TrendDetective (tendencias_peru.json, sin descargas).
builder.Services.AddSingleton<SONORA.Tendencias.TendenciasService>();

// Jukebox del Earth Information Center: videos de NASA SVS en caché local (se descargan la primera vez).
builder.Services.AddHttpClient("eic", c =>
{
    c.Timeout = TimeSpan.FromMinutes(10);
    c.DefaultRequestHeaders.UserAgent.ParseAdd("SONORA/1.0");
});
builder.Services.AddSingleton<SONORA.Eic.EicService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();

// Optimiza los parámetros musicales con NSGA-II (HBA.Evolutionary) para las características de una imagen o video.
app.MapPost("/api/optimize", async (SonificationFeatures features, CancellationToken ct) =>
{
    if (MappingOptimizer.Validate(features) is { } error) return Results.BadRequest(new { error });
    var result = await Task.Run(() => MappingOptimizer.Run(features, ct: ct), ct);
    return Results.Ok(result);
}).DisableAntiforgery();

// Asistente de voz: preguntas libres que las reglas del cliente no entienden (OpenAI, salida estructurada).
app.MapPost("/api/assistant", async (AssistantRequest req, OpenAIAssistant assistant, ILogger<OpenAIAssistant> log, CancellationToken ct) =>
{
    if (!assistant.Configured) return Results.Problem("Falta OpenAI:ApiKey en la configuración del servidor.", statusCode: 503);
    if (string.IsNullOrWhiteSpace(req.Question) || req.Question.Length > 500 || req.Context.Length > 30_000 || req.History.Count > 20)
        return Results.BadRequest(new { error = "Pregunta vacía o demasiado larga." });
    try
    {
        return Results.Ok(await assistant.AskAsync(req, ct));
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
    {
        log.LogWarning(ex, "Fallo al consultar OpenAI");
        return Results.Problem("No se pudo consultar el modelo.", statusCode: 502);
    }
}).DisableAntiforgery();

// Mapas de biomas y tendencias de Perú bajo demanda (red neuronal + NASA GIBS).
app.MapBiomas();
SONORA.Mundo.MundoEndpoints.MapMundo(app);
SONORA.Tendencias.TendenciasEndpoints.MapTendencias(app);
SONORA.Eic.EicEndpoints.MapEic(app);

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(SONORA.Client._Imports).Assembly);

app.Run();
