using Uinsure.PolicyApi.Core.Services;

namespace Uinsure.PolicyApi.Core.Tests.Services;

public sealed class ReferenceGeneratorTests
{
    [Fact]
    public void GIVEN_the_generator_WHEN_creating_payment_references_THEN_they_are_prefixed_and_unique()
    {
        // Arrange
        var generator = new ReferenceGenerator();

        // Act
        var references = Enumerable.Range(0, 1000).Select(_ => generator.NewPaymentReference()).ToList();

        // Assert
        references.ShouldAllBe(r => r.StartsWith("PAY-") && r.Length == 16);
        references.Distinct().Count().ShouldBe(references.Count);
    }
}
