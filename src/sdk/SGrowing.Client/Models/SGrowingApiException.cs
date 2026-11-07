using System.Net;

namespace SGrowing.Client.Models;

/// <summary>
/// Exception thrown when the SGrowing Growth OS API responds with an unsuccessful HTTP status code.
/// </summary>
public class SGrowingApiException : Exception
{
    /// <summary>
    /// HTTP status code returned by the server.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Raw response body string if available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SGrowingApiException"/> class.
    /// </summary>
    public SGrowingApiException(string message, HttpStatusCode statusCode, string? responseBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
