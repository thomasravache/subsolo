namespace Subsolo.Common.Domain;

/// <summary>SPEC-0007 — resultado com valor em caso de sucesso.</summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    /// <summary>Valor do sucesso; acessá-lo numa falha é erro de programação.</summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("O valor de um resultado com falha não pode ser acessado.");
}
