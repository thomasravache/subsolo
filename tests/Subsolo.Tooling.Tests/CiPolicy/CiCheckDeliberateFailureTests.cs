namespace Subsolo.Tooling.Tests.CiPolicy;

public sealed class CiCheckDeliberateFailureTests
{
    [Fact]
    [Trait("Spec", "SPEC-0006:IT-02")]
    public void CiCheckDeliberateFailure() => Assert.Fail("ci-check IT-02: teste propositalmente vermelho");
}
