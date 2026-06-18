using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace FtrIO.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ToggleConfigAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FTRIO001";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            id: DiagnosticId,
            title: "Missing toggle key in appsettings.json",
            messageFormat: "Method '{0}' is decorated with [Toggle] but has no entry in the Toggles section of appsettings.json",
            category: "FtrIO",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Every [Toggle]-decorated method must have a corresponding key in the Toggles section of appsettings.json.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationContext =>
            {
                AdditionalText? appSettings = null;
                foreach (var file in compilationContext.Options.AdditionalFiles)
                {
                    if (Path.GetFileName(file.Path).Equals("appsettings.json", System.StringComparison.OrdinalIgnoreCase))
                    {
                        appSettings = file;
                        break;
                    }
                }

                // No appsettings.json registered as AdditionalFile — skip analysis.
                // The runtime handles the missing-file case by treating everything as on.
                if (appSettings == null) return;

                var text = appSettings.GetText(compilationContext.CancellationToken);
                if (text == null) return;

                var toggleKeys = ParseToggleKeys(text.ToString());

                compilationContext.RegisterSymbolAction(symbolContext =>
                {
                    var method = (IMethodSymbol)symbolContext.Symbol;

                    bool hasToggleAttribute = false;
                    foreach (var attr in method.GetAttributes())
                    {
                        var name = attr.AttributeClass?.Name;
                        if (name == "Toggle" || name == "ToggleAttribute")
                        {
                            hasToggleAttribute = true;
                            break;
                        }
                    }

                    if (!hasToggleAttribute) return;

                    if (!toggleKeys.Contains(method.Name))
                    {
                        var location = method.Locations.IsEmpty ? Location.None : method.Locations[0];
                        symbolContext.ReportDiagnostic(Diagnostic.Create(Rule, location, method.Name));
                    }
                }, SymbolKind.Method);
            });
        }

        private static HashSet<string> ParseToggleKeys(string json)
        {
            var keys = new HashSet<string>();

            var togglesMatch = Regex.Match(json, @"""Toggles""\s*:\s*\{([^}]*)\}", RegexOptions.Singleline);
            if (!togglesMatch.Success) return keys;

            var togglesSection = togglesMatch.Groups[1].Value;
            foreach (Match m in Regex.Matches(togglesSection, @"""(\w+)""\s*:"))
                keys.Add(m.Groups[1].Value);

            return keys;
        }
    }
}
