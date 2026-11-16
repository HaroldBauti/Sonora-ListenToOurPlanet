using System.Text.Json;
using System.Text.Json.Nodes;
using SONORA.Client.Models;

namespace SONORA.Assistant;

/// <summary>
/// Responde las preguntas libres del asistente de voz con un modelo de OpenAI (Chat Completions con salida estructurada).
/// Las órdenes habituales se resuelven antes en el cliente con reglas (<c>VoiceAssistant.Parse</c>); aquí solo llega lo demás.
/// Configuración: <c>OpenAI:ApiKey</c> (user-secrets o variable de entorno <c>OpenAI__ApiKey</c>), <c>OpenAI:Model</c>, <c>OpenAI:ReasoningEffort</c>.
/// </summary>
public sealed class OpenAIAssistant(HttpClient http, IConfiguration config)
{
    // Debe coincidir con los casos de Execute() en SONORA.Client/Pages/Home.razor.
    static readonly string[] ActionTypes =
    [
        "play", "pause", "play_at", "play_lon", "goto_step", "describe", "summary", "set_tempo", "set_volume", "set_balance", "manual_params",
        "set_scale", "mute", "unmute", "solo", "unmute_all", "scan_radar", "scan_linear", "set_center", "show_layers",
        "narration", "set_speech_rate", "haptics", "high_contrast", "large_text", "download_transcript", "help", "load_map",
    ];

    const string SystemPrompt = """
        Eres SONORA, el asistente de voz que CONTROLA una aplicación web accesible que convierte visualizaciones de la Tierra de
        la NASA (o imágenes y videos que sube la persona) en música. Muchas personas usuarias son ciegas o tienen baja visión.

        Tu prioridad es ACTUAR, no conversar:
        - Si la persona pide algo que la app puede hacer, hazlo con acciones y confirma en muy pocas palabras (una frase corta,
          por ejemplo "Listo, más lento." o "Sonando el Sahara."). Lo que la persona oye es la respuesta.
        - Si pide buscar, ir, escuchar o hacer sonar una zona o un paso, usa play_at: va ahí y reproduce.
          Si nombra un lugar geográfico (país, ciudad, océano, volcán…) y lo que suena es un mapa (el de la NASA o uno de Perú)
          en recorrido lineal, usa play_lon con la longitud de ese lugar: la app calcula el paso exacto. En otras imágenes no hay
          geografía: dilo.
        - Si pide ver, escuchar o generar un mapa de Perú (biomas, deforestación, verdor, sequía, El Niño, una región o un año),
          usa load_map: la app lo genera al instante con su red neuronal y datos de la NASA.
        - Si pide cambiar un parámetro ("más científico", "baja el volumen", "más grave", "que suene más triste"), elige la
          acción y el valor más razonables a partir del ESTADO actual del contexto.
        - Solo explica cuando te hacen una pregunta (por qué, qué es, cómo). Entonces responde en 1 a 3 frases cortas.
        - Si lo que pide no está en la lista de acciones (por ejemplo cambiar de pestaña, abrir una sección o elegir un archivo),
          NO digas que lo hiciste: explica en una frase cómo hacerlo (botón Asistente, guía hablada o «abre la pestaña…»).
          Nunca confirmes una acción que no enviaste.
        - Responde siempre en español, para oírse: sin markdown, listas, emojis ni URLs.
        - Para hablar de la imagen usa el CONTEXTO. Las intensidades son relativas (0 a 100 dentro de esta imagen): no inventes
          cifras ni lugares. Puedes usar conocimiento científico general con prudencia.

        Acciones (value siempre es texto; "" si no aplica; "+N" o "-N" es relativo al valor actual):
        - play, pause.
        - play_at: value = paso (1 al total): salta ahí y reproduce. goto_step: igual pero sin reproducir (suena solo ese paso).
        - play_lon: value = longitud en grados, de -180 (oeste) a 180 (este), por ejemplo "-51.9" para Brasil: salta ahí y reproduce.
        - describe: lee el detalle del paso actual tras tu respuesta. summary: lee la descripción de la imagen.
        - set_tempo: 50 a 160 BPM. set_volume: 0 a 100.
        - set_balance: 0 a 100 (0 = lo más musical, 100 = lo más científico o fiel a los datos). manual_params: parámetros manuales.
        - set_scale: id de escala disponible (más triste: minor o pent-minor; más alegre: major o pent-major; misteriosa: whole-tone).
        - mute, unmute, solo: value = rol (melody, rhythm, bass, pad o points). unmute_all.
        - scan_radar, scan_linear. set_center: "x,y" en fracciones 0 a 1 de la imagen (centro del radar; en el mapa de la NASA,
          x = (longitud + 180) / 360 e y = (90 - latitud) / 180).
        - show_layers, narration, haptics, high_contrast, large_text: value "on" u "off".
        - set_speech_rate: velocidad de tu voz, 0.6 a 1.8 (o "+0.2" / "-0.2").
        - download_transcript, help.
        - load_map: value = "tipo;año;región" (un mapa) o "serie;tipo;región" (un mapa por año de 2001 a 2024).
          Tipos: biomas (bioma más probable), cambio_bosque (pérdida o ganancia de bosque 2001–2024), tendencia_ndvi (verdor que
          sube o baja 2001–2024), anomalia_ndvi (verdor de un año frente a lo normal: El Niño, sequías). El año (2001 a 2024) solo
          aplica a biomas y anomalia_ndvi; vacío = 2024. La serie solo existe para biomas y anomalia_ndvi.
          Regiones: peru, madre-de-dios, ucayali, san-martin, cordillera-blanca, costa-norte (Piura y Tumbes), altiplano (Puno), loreto.
          Ejemplos: "cambio_bosque;;madre-de-dios", "biomas;2010;peru", "serie;anomalia_ndvi;costa-norte".
        """;

    public bool Configured => !string.IsNullOrWhiteSpace(config["OpenAI:ApiKey"]);

    public async Task<AssistantReply> AskAsync(AssistantRequest req, CancellationToken ct)
    {
        var messages = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = SystemPrompt },
            new JsonObject { ["role"] = "system", ["content"] = "CONTEXTO\n" + req.Context },
        };
        foreach (var t in req.History.TakeLast(6))
            messages.Add(new JsonObject { ["role"] = t.Role == "assistant" ? "assistant" : "user", ["content"] = t.Text });
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = req.Question });

        var body = new JsonObject
        {
            ["model"] = config["OpenAI:Model"] is { Length: > 0 } model ? model : "gpt-5.4-mini",
            ["messages"] = messages,
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject { ["name"] = "sonora_reply", ["strict"] = true, ["schema"] = Schema() },
            },
        };
        if (config["OpenAI:ReasoningEffort"] is { Length: > 0 } effort) body["reasoning_effort"] = effort;

        using var msg = new HttpRequestMessage(HttpMethod.Post, "chat/completions") { Content = JsonContent.Create(body) };
        msg.Headers.Authorization = new("Bearer", config["OpenAI:ApiKey"]);
        using var resp = await http.SendAsync(msg, ct);
        var json = await resp.Content.ReadFromJsonAsync<JsonNode>(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"OpenAI {(int)resp.StatusCode}: {json?["error"]?["message"]}");

        var message = json!["choices"]![0]!["message"]!;
        if (message["content"]?.GetValue<string>() is not { Length: > 0 } content)
            return new("No puedo ayudarte con eso.", []);
        var reply = JsonSerializer.Deserialize<AssistantReply>(content, JsonSerializerOptions.Web)!;
        return reply with { Actions = reply.Actions.Where(a => ActionTypes.Contains(a.Type)).ToList() };
    }

    static JsonNode Schema() => JsonNode.Parse($$"""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["reply", "actions"],
          "properties": {
            "reply": { "type": "string" },
            "actions": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["type", "value"],
                "properties": {
                  "type": { "type": "string", "enum": {{JsonSerializer.Serialize(ActionTypes)}} },
                  "value": { "type": "string" }
                }
              }
            }
          }
        }
        """)!;
}
