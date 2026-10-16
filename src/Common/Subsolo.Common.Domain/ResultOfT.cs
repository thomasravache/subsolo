namespace Subsolo.Common.Domain;

/// <summary>SPEC-0007 — resultado com valor em caso de sucesso.</summary>
public class Result<TValue> : Result
{
    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => throw new NotImplementedException();

    /// <summary>Valor do sucesso; acessá-lo numa falha é erro de programação.</summary>
    public TValue Value => throw new NotImplementedException();
}
