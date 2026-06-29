using System.Text.Json.Serialization;

namespace PBS.ERP.Shared.Models;

public sealed class ApiResponse<T>
{
    [JsonPropertyOrder(1)]
    public string Message { get; set; } = "";

    [JsonPropertyOrder(2)]
    public bool Success { get; set; }

    [JsonPropertyOrder(3)]
    public T? Data { get; set; }

    [JsonPropertyOrder(4)]
    public object? Errors { get; set; }

    public static ApiResponse<T> Ok(T? data, string message)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Errors = null
        };
    }

    public static ApiResponse<T> Fail(string message, object? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Data = default,
            Errors = errors
        };
    }
}