namespace SxgEvalPlatformApi.Common;

/// <summary>
/// Generic result pattern for better error handling and functional programming approach
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public sealed class Result<T>
{
    private readonly T? _value;
    private readonly string? _error;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        _value = value;
     _error = null;
        _isSuccess = true;
    }

    private Result(string error)
    {
     _value = default;
      _error = error;
        _isSuccess = false;
    }

    /// <summary>
  /// Gets whether the result represents a success
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets whether the result represents a failure
 /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the success value (throws if result is failure)
    /// </summary>
    public T Value => _isSuccess ? _value! : throw new InvalidOperationException($"Cannot access Value when result is failure: {_error}");

    /// <summary>
    /// Gets the error message (throws if result is success)
    /// </summary>
    public string Error => !_isSuccess ? _error! : throw new InvalidOperationException("Cannot access Error when result is success");

    /// <summary>
    /// Creates a success result
    /// </summary>
    public static Result<T> Success(T value) => new(value);

    /// <summary>
  /// Creates a failure result
    /// </summary>
    public static Result<T> Failure(string error) => new(error);

    /// <summary>
  /// Transforms the success value using the provided function
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> func)
  {
    return _isSuccess ? Result<TNew>.Success(func(_value!)) : Result<TNew>.Failure(_error!);
    }

 /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
    if (_isSuccess)
          action(_value!);
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure
    /// </summary>
    public Result<T> OnFailure(Action<string> action)
    {
        if (!_isSuccess)
      action(_error!);
        return this;
    }

    /// <summary>
    /// Returns the value if successful, otherwise returns the default value
    /// </summary>
    public T GetValueOrDefault(T defaultValue = default!) => _isSuccess ? _value! : defaultValue;

    /// <summary>
    /// Implicit conversion from T to Result<T>
    /// </summary>
 public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Deconstructs the result into success flag and value/error
    /// </summary>
    public void Deconstruct(out bool isSuccess, out T? value, out string? error)
    {
        isSuccess = _isSuccess;
        value = _value;
        error = _error;
    }
}

/// <summary>
/// Non-generic result for operations that don't return a value
/// </summary>
public sealed class Result
{
    private readonly string? _error;
    private readonly bool _isSuccess;

    private Result(bool isSuccess, string? error = null)
    {
        _isSuccess = isSuccess;
   _error = error;
    }

    /// <summary>
    /// Gets whether the result represents a success
    /// </summary>
    public bool IsSuccess => _isSuccess;

/// <summary>
    /// Gets whether the result represents a failure
    /// </summary>
 public bool IsFailure => !_isSuccess;

    /// <summary>
 /// Gets the error message (throws if result is success)
    /// </summary>
    public string Error => !_isSuccess ? _error! : throw new InvalidOperationException("Cannot access Error when result is success");

    /// <summary>
    /// Creates a success result
  /// </summary>
    public static Result Success() => new(true);

    /// <summary>
    /// Creates a failure result
    /// </summary>
    public static Result Failure(string error) => new(false, error);

    /// <summary>
    /// Executes an action if the result is successful
    /// </summary>
    public Result OnSuccess(Action action)
    {
        if (_isSuccess)
            action();
  return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure
    /// </summary>
    public Result OnFailure(Action<string> action)
    {
    if (!_isSuccess)
     action(_error!);
return this;
    }

    /// <summary>
    /// Converts to generic result
    /// </summary>
    public Result<T> Map<T>(Func<T> func)
    {
        return _isSuccess ? Result<T>.Success(func()) : Result<T>.Failure(_error!);
    }

    /// <summary>
    /// Deconstructs the result into success flag and error
    /// </summary>
    public void Deconstruct(out bool isSuccess, out string? error)
    {
    isSuccess = _isSuccess;
     error = _error;
    }
}