using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeViewer.Services;

namespace CodeViewer.Benchmarks.Benchmarks;

public static class ThemeAndSyntaxBenchmarks
{
    public static List<BenchmarkResult> Run(string tempRoot, bool quick)
    {
        var results = new List<BenchmarkResult>();
        var langService = new LanguageService();
        var themeDir = Path.Combine(tempRoot, "themes_bench");
        Directory.CreateDirectory(themeDir);

        results.Add(BenchmarkRunner.Measure(
            "2. Themes & Syntax Highlighting",
            "BuiltInThemes.GetAll (20 themes)",
            quick ? 50 : 200,
            () => _ = BuiltInThemes.GetAll(),
            warmupCount: 5,
            notes: "ColorTheme dictionary instantiation"));

        results.Add(BenchmarkRunner.Measure(
            "2. Themes & Syntax Highlighting",
            "ThemeService Cold Initialization",
            quick ? 5 : 15,
            () => _ = new ThemeService(themeDir),
            warmupCount: 2,
            notes: "Sync theme lookup + async IO defer"));

        var extensions = new[]
        {
            "Program.cs", "main.py", "app.ts", "widget.dart", "README.md",
            "config.json", "deploy.yaml", "script.ps1", "run.sh", "query.sql"
        };

        results.Add(BenchmarkRunner.Measure(
            "2. Themes & Syntax Highlighting",
            "LanguageService.DetectLanguage (10 exts)",
            quick ? 50 : 300,
            () =>
            {
                foreach (var ext in extensions)
                {
                    _ = langService.DetectLanguage(ext);
                }
            },
            warmupCount: 10,
            notes: "10 extension classifications per op"));

        var langs = new[] { "C#", "Python", "JavaScript", "Dart", "Markdown", "JSON", "XML" };
        var definitions = langs.Select(l => langService.GetHighlightingDefinition(l)).Where(d => d != null).ToList();
        var themes = BuiltInThemes.GetAll();
        int themeIdx = 0;

        results.Add(BenchmarkRunner.Measure(
            "2. Themes & Syntax Highlighting",
            "HighlightingThemeApplicator (7 langs)",
            quick ? 20 : 100,
            () =>
            {
                var theme = themes[themeIdx++ % themes.Count];
                foreach (var def in definitions)
                {
                    HighlightingThemeApplicator.Apply(def, theme);
                }
            },
            warmupCount: 5,
            notes: "Live syntax brush switch across open tabs"));

        return results;
    }
}
