namespace FlagForge.Evaluation.Tests;

public sealed class DependencyTests
{
    [Fact]
    public void Evaluation_engine_references_only_the_base_class_library()
    {
        var references = typeof(FlagEvaluator).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        references.ShouldAllBe(name =>
            name == "netstandard" || name == "System" || name.StartsWith("System.", StringComparison.Ordinal));
    }
}
