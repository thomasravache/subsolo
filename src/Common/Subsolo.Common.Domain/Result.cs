namespace Subsolo.Common.Domain;

/// <summary>SPEC-0007 — resultado de uma operação: sucesso ou um <see cref="Domain.Error"/>.</summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (isSuccess != (error == Error.None))
        {
            throw new ArgumentException("Sucesso não tem erro, e falha sempre tem um erro.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>Erro da operação; só tem significado quando <see cref="IsFailure"/>.</summary>
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}
