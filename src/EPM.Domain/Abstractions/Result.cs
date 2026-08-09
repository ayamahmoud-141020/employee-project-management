namespace EPM.Domain.Abstractions;

/// <summary>
/// Outcome of an operation that is allowed to fail for ordinary business reasons.
/// </summary>
/// <remarks>
/// Rule of thumb used throughout this codebase: an expected refusal ("email already taken",
/// "project not found") returns a failed Result; a broken assumption or a dead dependency
/// throws. That keeps exceptions meaning "bug or outage" and keeps the happy path readable,
/// since a duplicate email is not exceptional — it is Tuesday.
/// </remarks>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        // Guards against the two states that would make the type meaningless: a success
        // carrying an error, or a failure with nothing to explain it.
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>
    /// Returns the first failure among <paramref name="results"/>, or success if there is none.
    /// Handy when a factory has to build several value objects before it can proceed.
    /// </summary>
    public static Result FirstFailureOrSuccess(params Result[] results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                return Failure(result.Error);
            }
        }

        return Success();
    }
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// The produced value. Throws if the result failed — check <see cref="Result.IsSuccess"/>
    /// first. Reading the value of a failure is a bug in the caller, not a business error.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    // Lets a factory end with `return employee;` instead of `return Result.Success(employee);`.
    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
