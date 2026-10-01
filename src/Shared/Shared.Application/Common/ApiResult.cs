namespace Shared.Application.Common;

public sealed class ApiResult<T>
{
    public bool Success { get; private init; }

    public string Message { get; private init; } = string.Empty;

    public T? Data { get; private init; }

    public int StatusCode { get; private init; }

    public static ApiResult<T> Ok(T data, string message = "OK") =>
        new()
        {
            Success = true,
            Message = message,
            Data = data,
            StatusCode = 200
        };

    public static ApiResult<T> Created(T data, string message = "Created") =>
        new()
        {
            Success = true,
            Message = message,
            Data = data,
            StatusCode = 201
        };

    public static ApiResult<T> Fail(string message, int statusCode = 400) =>
        new()
        {
            Success = false,
            Message = message,
            Data = default,
            StatusCode = statusCode
        };

    public ApiResult<T> WithStatus(int statusCode) =>
        new()
        {
            Success = Success,
            Message = Message,
            Data = Data,
            StatusCode = statusCode
        };
}
