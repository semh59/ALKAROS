namespace ALKAROS.ModuleComposition.Primitives;

/// <summary>
/// Discriminated result that either succeeds with a value or fails with
/// an error message. Contains no business logic.
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }

    public T? Value { get; }

    public string? Error { get; }

    public Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }
}