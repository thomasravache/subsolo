namespace Subsolo.Common.Domain;

/// <summary>SPEC-0007 — resultado de uma operação: sucesso ou um <see cref="Domain.Error"/>.</summary>
public class Result
{
    protected Result(bool isSuccess, Error error) => throw new NotImplementedException();

    public bool IsSuccess { get; }

    public bool IsFailure => throw new NotImplementedException();

    /// <summary>Erro da operação; só tem significado quando <see cref="IsFailure"/>.</summary>
    public Error Error { get; }

    public static Result Success() => throw new NotImplementedException();

    public static Result Failure(Error error) => throw new NotImplementedException();

    public static Result<TValue> Success<TValue>(TValue value) => throw new NotImplementedException();

    public static Result<TValue> Failure<TValue>(Error error) => throw new NotImplementedException();
}
