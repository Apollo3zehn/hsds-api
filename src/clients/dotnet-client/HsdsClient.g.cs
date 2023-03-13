#nullable enable

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hsds.Api;

/// <summary>
/// A client for the Hsds system.
/// </summary>
public interface IHsdsClient
{
    /// <summary>
    /// Gets the <see cref="IDomainClient"/>.
    /// </summary>
    IDomainClient Domain { get; }




}

/// <inheritdoc />
public class HsdsClient : IHsdsClient, IDisposable
{
    private HttpClient _httpClient;

    private DomainClient _domain;

    /// <summary>
    /// Initializes a new instance of the <see cref="HsdsClient"/>.
    /// </summary>
    /// <param name="baseUrl">The base URL to connect to.</param>
    public HsdsClient(Uri baseUrl) : this(new HttpClient() { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(60) })
    {
        //
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HsdsClient"/>.
    /// </summary>
    /// <param name="httpClient">The HTTP client to use.</param>
    public HsdsClient(HttpClient httpClient)
    {
        if (httpClient.BaseAddress is null)
            throw new Exception("The base address of the HTTP client must be set.");

        _httpClient = httpClient;

        _domain = new DomainClient(this);

    }


    /// <inheritdoc />
    public IDomainClient Domain => _domain;





    internal T Invoke<T>(string method, string relativeUrl, string? acceptHeaderValue, string? contentTypeValue, HttpContent? content)
    {
        // prepare request
        using var request = BuildRequestMessage(method, relativeUrl, content, contentTypeValue, acceptHeaderValue);

        // send request
        var response = _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);

        // process response
        if (!response.IsSuccessStatusCode)
        {

            if (!response.IsSuccessStatusCode)
            {
                var message = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
                var statusCode = $"H00.{(int)response.StatusCode}";

                if (string.IsNullOrWhiteSpace(message))
                    throw new HsdsException(statusCode, $"The HTTP request failed with status code {response.StatusCode}.");

                else
                    throw new HsdsException(statusCode, $"The HTTP request failed with status code {response.StatusCode}. The response message is: {message}");
            }
        }

        try
        {
            if (typeof(T) == typeof(object))
            {
                return default!;
            }

            else if (typeof(T) == typeof(HttpResponseMessage))
            {
                return (T)(object)(response);
            }

            else
            {
                var stream = response.Content.ReadAsStream();

                try
                {
                    return JsonSerializer.Deserialize<T>(stream, Utilities.JsonOptions)!;
                }
                catch (Exception ex)
                {
                    throw new HsdsException("H01", "Response data could not be deserialized.", ex);
                }
            }
        }
        finally
        {
            if (typeof(T) != typeof(HttpResponseMessage))
                response.Dispose();
        }
    }

    internal async Task<T> InvokeAsync<T>(string method, string relativeUrl, string? acceptHeaderValue, string? contentTypeValue, HttpContent? content, CancellationToken cancellationToken)
    {
        // prepare request
        using var request = BuildRequestMessage(method, relativeUrl, content, contentTypeValue, acceptHeaderValue);

        // send request
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        // process response
        if (!response.IsSuccessStatusCode)
        {

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                var statusCode = $"H00.{(int)response.StatusCode}";

                if (string.IsNullOrWhiteSpace(message))
                    throw new HsdsException(statusCode, $"The HTTP request failed with status code {response.StatusCode}.");

                else
                    throw new HsdsException(statusCode, $"The HTTP request failed with status code {response.StatusCode}. The response message is: {message}");
            }
        }

        try
        {
            if (typeof(T) == typeof(object))
            {
                return default!;
            }

            else if (typeof(T) == typeof(HttpResponseMessage))
            {
                return (T)(object)(response);
            }

            else
            {
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

                try
                {
                    return (await JsonSerializer.DeserializeAsync<T>(stream, Utilities.JsonOptions))!;
                }
                catch (Exception ex)
                {
                    throw new HsdsException("H01", "Response data could not be deserialized.", ex);
                }
            }
        }
        finally
        {
            if (typeof(T) != typeof(HttpResponseMessage))
                response.Dispose();
        }
    }
    
    private static readonly HttpRequestOptionsKey<bool> WebAssemblyEnableStreamingResponseKey = new HttpRequestOptionsKey<bool>("WebAssemblyEnableStreamingResponse");

    private HttpRequestMessage BuildRequestMessage(string method, string relativeUrl, HttpContent? content, string? contentTypeHeaderValue, string? acceptHeaderValue)
    {
        var requestMessage = new HttpRequestMessage()
        {
            Method = new HttpMethod(method),
            RequestUri = new Uri(relativeUrl, UriKind.Relative),
            Content = content
        };

        if (contentTypeHeaderValue is not null && requestMessage.Content is not null)
            requestMessage.Content.Headers.ContentType = MediaTypeWithQualityHeaderValue.Parse(contentTypeHeaderValue);

        if (acceptHeaderValue is not null)
            requestMessage.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(acceptHeaderValue));

        // For web assembly
        // https://docs.microsoft.com/de-de/dotnet/api/microsoft.aspnetcore.components.webassembly.http.webassemblyhttprequestmessageextensions.setbrowserresponsestreamingenabled?view=aspnetcore-6.0
        // https://github.com/dotnet/aspnetcore/blob/0ee742c53f2669fd7233df6da89db5e8ab944585/src/Components/WebAssembly/WebAssembly/src/Http/WebAssemblyHttpRequestMessageExtensions.cs
        requestMessage.Options.Set(WebAssemblyEnableStreamingResponseKey, true);

        return requestMessage;
    }


    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient?.Dispose();
    }

}

/// <summary>
/// Provides methods to interact with domain.
/// </summary>
public interface IDomainClient
{
    /// <summary>
    /// 
    /// </summary>
    string GetDomain();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<string> GetDomainAsync(CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class DomainClient : IDomainClient
{
    private HsdsClient ___client;
    
    internal DomainClient(HsdsClient client)
    {
        ___client = client;
    }

    /// <inheritdoc />
    public string GetDomain()
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/domain");

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<string>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<string> GetDomainAsync(CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/domain");

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<string>("GET", __url, "application/json", default, default, cancellationToken);
    }

}




/// <summary>
/// A HsdsException.
/// </summary>
public class HsdsException : Exception
{
    internal HsdsException(string statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    internal HsdsException(string statusCode, string message, Exception innerException) : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    /// <summary>
    /// The exception status code.
    /// </summary>
    public string StatusCode { get; }
}




internal static class Utilities
{
    internal static JsonSerializerOptions JsonOptions { get; }

    static Utilities()
    {
        JsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }
}

