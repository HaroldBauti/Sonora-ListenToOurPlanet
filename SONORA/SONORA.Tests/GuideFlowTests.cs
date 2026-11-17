using SONORA.Client.Services;

namespace SONORA.Tests;

public class GuideFlowTests
{
    static GuideCommand Say(GuideFlow f, string text, out string? reply)
    {
        var cmd = f.Interpret(text);
        reply = f.Apply(cmd, text);
        return cmd;
    }

    [Fact]
    public void Start_presents_the_real_sections_in_interface_order()
    {
        var f = Fixtures.Flow();
        string prompt = f.Start();
        Assert.Equal(GuideStage.Menu, f.Stage);
        Assert.Equal(["eic", "archivo", "biomas", "clima", "mundo"], f.Options().Select(o => o.Id));
        Assert.Contains("1, Jukebox NASA", prompt);
        Assert.Contains("5, Todo el planeta", prompt);
    }

    [Fact]
    public void Biomas_asks_only_for_the_missing_data_and_then_loads()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "3", out _);                           // Biomas de Perú
        Assert.Equal("tipo", f.CurrentSlot);
        Say(f, "cambio de bosque", out _);            // no usa año: no se pregunta el periodo
        Assert.Equal("region", f.CurrentSlot);
        Say(f, "Madre de Dios", out var reply);
        Assert.Equal(GuideStage.Loading, f.Stage);
        Assert.Contains("Cargando", reply);
        var req = f.Request()!;
        Assert.Equal("biomas", req.Section);
        Assert.Equal("cambio_bosque", req.Values["tipo"]);
        Assert.Equal("madre-de-dios", req.Values["region"]);
        Assert.False(req.Values.ContainsKey("anio"));
    }

    [Fact]
    public void Data_given_in_one_sentence_is_reused()
    {
        var f = Fixtures.Flow();
        f.Start();
        var cmd = Say(f, "biomas de Ucayali en 2010", out _);
        Assert.Equal(GuideCommandKind.Prefilled, cmd.Kind);
        Assert.Equal(GuideStage.Loading, f.Stage);   // tipo, región y año ya estaban en la frase
        var req = f.Request()!;
        Assert.Equal(("biomas", "ucayali", "2010"), (req.Values["tipo"], req.Values["region"], req.Values["anio"]));
    }

    [Fact]
    public void Partial_sentence_jumps_to_the_first_missing_slot()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "clima de Piura", out var reply);
        Assert.Equal("clima", f.Section);
        Assert.Equal("fuente", f.CurrentSlot);       // la región ya está: no se vuelve a pedir
        Assert.Contains("región Piura", reply);
    }

    [Fact]
    public void Next_and_previous_page_options_while_choosing()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "jukebox", out _);
        Say(f, "todos los temas", out _);
        Assert.Equal("item", f.CurrentSlot);
        Assert.Equal(0, f.Page);
        Assert.Equal(GuideCommandKind.More, Say(f, "siguiente", out _).Kind);
        Assert.Equal(1, f.Page);
        Assert.Equal(6, f.PageOptions()[0].Number);  // numeración absoluta
        Assert.Equal(GuideCommandKind.Less, Say(f, "anterior", out _).Kind);
        Assert.Equal(0, f.Page);
    }

    [Fact]
    public void Next_and_previous_are_left_to_the_player_once_content_is_ready()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "biomas de Ucayali en 2010", out _);
        f.Loaded("Biomas · Ucayali · 2010");
        Assert.Equal(GuideCommandKind.None, f.Interpret("siguiente").Kind);
        Assert.Equal(GuideCommandKind.None, f.Interpret("anterior").Kind);
    }

    [Fact]
    public void Absolute_number_selects_even_outside_the_current_group()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "1", out _);
        Say(f, "todos", out _);
        Say(f, "la siete", out _);
        Assert.Equal(GuideStage.Loading, f.Stage);
        Assert.Equal("waterVapor", f.Request()!.Values["item"]);
    }

    [Fact]
    public void Ambiguous_name_asks_for_clarification_instead_of_guessing()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "1", out _);
        Say(f, "todos", out _);
        var cmd = Say(f, "temperatura", out var reply);
        Assert.Equal(GuideCommandKind.Ambiguous, cmd.Kind);
        Assert.True(cmd.Options!.Count > 1);
        Assert.Contains("¿Cuál quieres?", reply);
        Assert.Equal(GuideStage.Choose, f.Stage);    // no se eligió nada
        Say(f, "1", out _);                          // elige dentro de la aclaración
        Assert.Equal(GuideStage.Loading, f.Stage);
    }

    [Fact]
    public void Clear_name_wins_over_longer_similar_names()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "1", out _);
        Say(f, "todos", out _);
        Say(f, "temperatura del mar", out _);
        Assert.Equal("seaSurfaceTemperature", f.Request()!.Values["item"]);
    }

    [Fact]
    public void Back_undoes_the_last_choice_and_change_keeps_the_others()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "biomas de Ucayali en 2010", out _);
        f.Loaded("Biomas · Ucayali · 2010");

        Say(f, "cambiar región", out _);
        Assert.Equal("region", f.CurrentSlot);
        Assert.Equal("biomas", f.Values["tipo"]);    // el resto se conserva
        Assert.Equal("2010", f.Values["anio"]);
        Say(f, "Loreto", out _);
        Assert.Equal(GuideStage.Loading, f.Stage);
        Assert.Equal("loreto", f.Request()!.Values["region"]);

        f.Loaded("Biomas · Loreto · 2010");
        Say(f, "volver", out _);
        Assert.Equal(GuideStage.Choose, f.Stage);
        Assert.Equal("anio", f.CurrentSlot);         // el último dato era el año: se vuelve a pedir solo ese
        Assert.Equal("loreto", f.Values["region"]);
    }

    [Fact]
    public void Back_from_the_first_choice_returns_to_the_menu()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "clima", out _);
        Say(f, "volver", out _);
        Assert.Equal(GuideStage.Menu, f.Stage);
    }

    [Fact]
    public void Failed_load_keeps_choices_and_offers_retry()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "biomas de Ucayali en 2010", out _);
        string reply = f.Fail("el atlas se está preparando");
        Assert.Equal(GuideStage.Error, f.Stage);
        Assert.Contains("se conservan", reply);
        Assert.Equal(GuideCommandKind.Retry, Say(f, "reintentar", out _).Kind);
        Assert.Equal(GuideStage.Loading, f.Stage);
        Assert.Equal("ucayali", f.Request()!.Values["region"]);
    }

    [Fact]
    public void Nothing_is_chosen_at_random_unless_a_recommendation_is_requested()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "clima", out _);
        Say(f, "Piura", out _);
        Say(f, "NASA POWER", out _);
        Assert.Equal("vars", f.CurrentSlot);
        Assert.Equal(GuideCommandKind.None, f.Interpret("lo que sea").Kind);
        Assert.Equal("vars", f.CurrentSlot);
        Assert.Equal(GuideCommandKind.Recommend, Say(f, "recomiéndame", out var reply).Kind);
        Assert.Contains("sugiere la red", reply);
        Assert.Equal("red", f.Request()!.Values["vars"]);
    }

    [Fact]
    public void Variables_can_be_picked_one_by_one_and_unpicked()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "clima de Lima", out _);
        Say(f, "power", out _);
        Say(f, "elegir las variables una por una", out _);
        Assert.Equal("pick", f.CurrentSlot);
        Say(f, "Temperatura media", out _);
        Say(f, "Humedad", out _);
        Say(f, "Temperatura media", out _);          // quitar
        Assert.Equal(GuideCommandKind.Select, Say(f, "listo", out _).Kind);
        Assert.Equal(GuideStage.Loading, f.Stage);
        Assert.Equal("RH2M", f.Request()!.Values["pick"]);
    }

    [Fact]
    public void Latest_period_becomes_the_latest_year_in_the_request()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "mundo", out _);
        Say(f, "Sahara", out _);
        Say(f, "1", out _);                          // 2024, el más reciente
        Assert.Equal("2024", f.Request()!.Values["anio"]);
    }

    [Fact]
    public void Year_out_of_range_is_rejected_and_nothing_changes()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "biomas", out _);
        Say(f, "1", out _);
        Say(f, "Loreto", out _);
        Say(f, "elegir otro año", out _);
        Assert.Equal("anio", f.CurrentSlot);
        string reply = f.Select("year:1990");
        Assert.Contains("no está disponible", reply);
        Assert.Equal("anio", f.CurrentSlot);
        f.Select("year:2015");
        Assert.Equal("2015", f.Request()!.Values["anio"]);
    }

    [Fact]
    public void Where_reports_section_selection_and_state()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "biomas de Ucayali en 2010", out _);
        f.Loaded("Biomas · Ucayali · 2010");
        Assert.Equal(GuideCommandKind.Where, f.Interpret("¿Dónde estoy?").Kind);
        string where = f.Where("En pausa", null, "Biomas · Ucayali · 2010", "73° O");
        Assert.Contains("Biomas de Perú", where);
        Assert.Contains("región Ucayali", where);
        Assert.Contains("en pausa", where);
        Assert.Contains("73° O", where);
    }

    [Fact]
    public void What_can_I_do_depends_on_the_context()
    {
        var f = Fixtures.Flow();
        f.Start();
        Assert.Equal(GuideCommandKind.WhatCanIDo, f.Interpret("¿qué puedo hacer aquí?").Kind);
        Assert.Contains("elegir una sección", f.WhatCanIDo(false, false, true));
        Say(f, "biomas de Ucayali en 2010", out _);
        f.Loaded("x");
        string ready = f.WhatCanIDo(true, true, false);
        Assert.Contains("pausar", ready);
        Assert.Contains("aprender a escuchar", ready);
        Assert.Contains("micrófono no está disponible", ready);
    }

    [Fact]
    public void Free_questions_are_not_captured_by_the_guide()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "biomas de Ucayali en 2010", out _);
        f.Loaded("x");
        Assert.Equal(GuideCommandKind.None, f.Interpret("explícame por qué hay menos selva en el oeste").Kind);
        Assert.Equal(GuideCommandKind.Explain, f.Interpret("explícame").Kind);
        Assert.Equal(GuideCommandKind.ExplainMore, f.Interpret("más detalle").Kind);
        Assert.Equal(GuideCommandKind.Demo, f.Interpret("aprender a escuchar").Kind);
    }

    [Fact]
    public void Returning_to_a_completed_section_keeps_data_and_asks_the_last_slot_again()
    {
        var f = Fixtures.Flow();
        f.Start();
        Say(f, "biomas de Ucayali en 2010", out _);
        f.Loaded("x");
        Say(f, "menú", out _);
        Say(f, "biomas", out _);
        Assert.Equal(GuideStage.Choose, f.Stage);
        Assert.Equal("anio", f.CurrentSlot);
        Assert.Equal("ucayali", f.Values["region"]);
    }

    [Theory]
    [InlineData("3", 3)]
    [InlineData("la tres", 3)]
    [InlineData("opción 12", 12)]
    [InlineData("la primera", 1)]
    [InlineData("número cuatro", 4)]
    public void Option_numbers(string text, int expected) =>
        Assert.Equal(expected, GuideFlow.OptionNumber(VoiceAssistant.Normalize(text)));

    [Theory]
    [InlineData("biomas en 2010")]
    [InlineData("sube el volumen")]
    public void Option_numbers_ignore_longer_sentences(string text) =>
        Assert.Null(GuideFlow.OptionNumber(VoiceAssistant.Normalize(text)));
}
