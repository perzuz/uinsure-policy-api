using Uinsure.PolicyApi.Core.Services;

namespace Uinsure.PolicyApi.Core.Tests.Services;

public sealed class ReferenceGeneratorTests
{
    [Fact]
    public void Payment_references_are_prefixed_and_unique()
    {
        var generator = new ReferenceGenerator();

        var references = Enumerable.Range(0, 1000).Select(_ => generator.NewPaymentReference()).ToList();

        references.ShouldAllBe(r => r.StartsWith("PAY-") && r.Length == 16);
        references.Distinct().Count().ShouldBe(references.Count);
    }
}
