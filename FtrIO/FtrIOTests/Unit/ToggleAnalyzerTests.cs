namespace FtrIOTests.Unit
{
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FtrIO.Analyzers;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Text;
    using NUnit.Framework;

    [TestFixture]
    public class ToggleAnalyzerTests
    {
        // Minimal stub of the Toggle attribute so test source compiles without referencing FtrIO.dll
        private const string ToggleAttributeStub = @"
            namespace FtrIO
            {
                using System;
                [AttributeUsage(AttributeTargets.Method)]
                public class Toggle : Attribute { }
            }";

        private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
            string source, string? appSettingsJson = null)
        {
            var references = System.AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>();

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[]
                {
                    CSharpSyntaxTree.ParseText(ToggleAttributeStub),
                    CSharpSyntaxTree.ParseText(source)
                },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var additionalFiles = appSettingsJson == null
                ? ImmutableArray<AdditionalText>.Empty
                : ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("appsettings.json", appSettingsJson));

            var compilationWithAnalyzers = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ToggleConfigAnalyzer()),
                new AnalyzerOptions(additionalFiles));

            return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        }

        [Test]
        public async Task ToggleMethodWithMatchingConfigKey_ReportsNoDiagnostic()
        {
            var source = @"
                using FtrIO;
                public class MyClass
                {
                    [Toggle] public void MyMethod() { }
                }";

            var appSettings = @"{ ""Toggles"": { ""MyMethod"": true } }";

            var diagnostics = await GetDiagnosticsAsync(source, appSettings);

            Assert.IsEmpty(diagnostics.Where(d => d.Id == ToggleConfigAnalyzer.DiagnosticId));
        }

        [Test]
        public async Task ToggleMethodMissingFromConfig_ReportsFTRIO001()
        {
            var source = @"
                using FtrIO;
                public class MyClass
                {
                    [Toggle] public void MyMethod() { }
                }";

            var appSettings = @"{ ""Toggles"": { } }";

            var diagnostics = await GetDiagnosticsAsync(source, appSettings);

            Assert.AreEqual(1, diagnostics.Count(d => d.Id == ToggleConfigAnalyzer.DiagnosticId));
        }

        [Test]
        public async Task MethodWithoutToggleAttribute_ReportsNoDiagnosticEvenIfMissingFromConfig()
        {
            var source = @"
                public class MyClass
                {
                    public void MyMethod() { }
                }";

            var appSettings = @"{ ""Toggles"": { } }";

            var diagnostics = await GetDiagnosticsAsync(source, appSettings);

            Assert.IsEmpty(diagnostics.Where(d => d.Id == ToggleConfigAnalyzer.DiagnosticId));
        }

        [Test]
        public async Task NoAppSettingsRegistered_ReportsNoDiagnosticRegardlessOfMissingKey()
        {
            var source = @"
                using FtrIO;
                public class MyClass
                {
                    [Toggle] public void MyMethod() { }
                }";

            var diagnostics = await GetDiagnosticsAsync(source, appSettingsJson: null);

            Assert.IsEmpty(diagnostics.Where(d => d.Id == ToggleConfigAnalyzer.DiagnosticId));
        }

        [Test]
        public async Task MultipleToggleMethods_ReportsDiagnosticOnlyForMissingOnes()
        {
            var source = @"
                using FtrIO;
                public class MyClass
                {
                    [Toggle] public void PresentMethod() { }
                    [Toggle] public void MissingMethod() { }
                }";

            var appSettings = @"{ ""Toggles"": { ""PresentMethod"": true } }";

            var diagnostics = await GetDiagnosticsAsync(source, appSettings);
            var ftrioDiagnostics = diagnostics.Where(d => d.Id == ToggleConfigAnalyzer.DiagnosticId).ToList();

            Assert.AreEqual(1, ftrioDiagnostics.Count);
            Assert.IsTrue(ftrioDiagnostics[0].GetMessage().Contains("MissingMethod"));
        }

        private class InMemoryAdditionalText : AdditionalText
        {
            private readonly string _text;
            public override string Path { get; }

            public InMemoryAdditionalText(string path, string text)
            {
                Path = path;
                _text = text;
            }

            public override SourceText? GetText(CancellationToken cancellationToken = default)
                => SourceText.From(_text);
        }
    }
}
