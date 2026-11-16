using SONORA.Client.Models;
using SONORA.Client.Services;

namespace SONORA.Tests;

public class StatusAnnouncerTests
{
    [Fact]
    public void Announces_only_when_the_state_changes()
    {
        var s = new StatusAnnouncer();
        Assert.Equal("Cargando", s.Update(AppState.Loading));
        Assert.Null(s.Update(AppState.Loading));
        Assert.Equal("En pausa", s.Update(AppState.Paused));
        Assert.Equal("Reproduciendo", s.Update(AppState.Playing));
        for (int i = 0; i < 10; i++) Assert.Null(s.Update(AppState.Playing)); // cada paso de la música
        Assert.Equal("En pausa", s.Update(AppState.Paused));
    }

    [Fact]
    public void Idle_is_not_announced()
    {
        var s = new StatusAnnouncer();
        Assert.Equal("Escuchando", s.Update(AppState.Listening));
        Assert.Null(s.Update(AppState.Idle));
    }

    [Theory]
    [InlineData(true, true, true, true, true, AppState.Listening)]
    [InlineData(false, true, true, true, true, AppState.Processing)]
    [InlineData(false, false, true, true, true, AppState.Loading)]
    [InlineData(false, false, false, true, true, AppState.Playing)]
    [InlineData(false, false, false, true, false, AppState.Paused)]
    [InlineData(false, false, false, false, false, AppState.Idle)]
    public void Compute_priority(bool listening, bool processing, bool loading, bool content, bool playing, AppState expected) =>
        Assert.Equal(expected, StatusAnnouncer.Compute(listening, processing, loading, content, playing));

    [Fact]
    public void Labels_match_the_required_wording()
    {
        Assert.Equal(["Escuchando", "Procesando", "Cargando", "Reproduciendo", "En pausa"],
            new[] { AppState.Listening, AppState.Processing, AppState.Loading, AppState.Playing, AppState.Paused }.Select(StatusAnnouncer.Label));
    }
}

public class ExplainerTests
{
    [Fact]
    public void Simple_explanation_has_source_period_disclaimer_and_real_layers()
    {
        var a = Fixtures.Analysis();
        string text = Explainer.Simple(a, new ContentMeta("NASA GIBS", "2010", "Biomas más probables."));
        Assert.StartsWith("Biomas · Ucayali y Huánuco · 2010.", text);
        Assert.Contains("Fuente: NASA GIBS.", text);
        Assert.Contains("Periodo: 2010.", text);
        Assert.Contains(SoundRules.Sonification, text);
        Assert.Contains("Selva suena como melodía", text);
        Assert.Contains("Andes, como bajo", text);
    }

    [Fact]
    public void Detailed_explanation_uses_context_and_the_real_rules()
    {
        var a = Fixtures.Analysis();
        string text = Explainer.Detailed(a);
        Assert.Contains("Contexto científico del mapa.", text);
        Assert.Contains("de oeste a este, en 4 pasos", text);
        Assert.Contains(SoundRules.Role("rhythm"), text);
        Assert.Contains("4 colores exactos", text);
        Assert.Contains("relativas", text);
        Assert.DoesNotContain("campanita", text); // no es una serie año por año
    }

    [Fact]
    public void Year_series_explain_the_bell_and_melody_motif()
    {
        var a = Fixtures.Analysis(scan: "years");
        Assert.Contains("campanita", Explainer.Detailed(a));
        Assert.Contains("Periodo: 75° O a 72° O", Explainer.Simple(a)); // periodo deducido de los pasos
    }

    [Fact]
    public void Layer_lesson_points_to_the_real_maximum_and_minimum()
    {
        var a = Fixtures.Analysis();
        var (text, max, min) = Explainer.LayerLesson(a, "bass");
        Assert.Equal(2, max);
        Assert.Equal(0, min);
        Assert.Contains("Andes suena como bajo", text);
        Assert.Contains("73° O, con 80 %", text);
        Assert.Contains("75° O, con 10 %", text);
    }

    [Fact]
    public void Uploaded_files_mention_kmeans_and_have_a_default_source()
    {
        var a = Fixtures.Analysis(source: "upload");
        Assert.Contains("k-means", Explainer.Detailed(a));
        Assert.Contains("Fuente: tu propio archivo.", Explainer.Simple(a));
    }

    [Fact]
    public void Sound_rules_follow_the_selected_scale()
    {
        var scale = new ScaleInfo("blues", "Blues", "con tensión", "Do Mi♭", "Do7 · Fa7");
        Assert.Contains("escala blues", SoundRules.Role("melody", scale));
        Assert.Contains("Do7 · Fa7", SoundRules.Role("pad", scale));
    }
}
