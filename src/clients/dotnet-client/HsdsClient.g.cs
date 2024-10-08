#nullable enable

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hsds.Api
{
/// <summary>
/// A client for the Hsds system.
/// </summary>
public interface IHsdsClient
{
    /// <summary>
    /// Gets the V2_0 client.
    /// </summary>
    Hsds.Api.V2_0.IV2_0 V2_0 { get; }




}

/// <inheritdoc />
public class HsdsClient : IHsdsClient, IDisposable
{
    private HttpClient _httpClient;

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

        V2_0 = new Hsds.Api.V2_0.V2_0(this);

    }


    /// <inheritdoc />
    public Hsds.Api.V2_0.IV2_0 V2_0 { get; }





    internal T Invoke<T>(string method, string relativeUrl, string? acceptHeaderValue, string? contentTypeValue, HttpContent? content)
    {
        // prepare request
        using var request = BuildRequestMessage(method, relativeUrl, content, contentTypeValue, acceptHeaderValue);

        // send request
        var response = _httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);

        // process response
        if (!response.IsSuccessStatusCode)
        {
            var message = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
            var statusCode = $"00.{(int)response.StatusCode}";

            if (string.IsNullOrWhiteSpace(message))
                throw new HsdsException(statusCode, $"The HTTP request failed with status code {response.StatusCode}.");

            else
                throw new HsdsException(statusCode, $"The HTTP request failed with status code {response.StatusCode}. The response message is: {message}");
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
                    throw new HsdsException("01", "Response data could not be deserialized.", ex);
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
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        // process response
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var statusCode = $"00.{(int)response.StatusCode}";

            if (string.IsNullOrWhiteSpace(message))
                throw new HsdsException(statusCode, $"The HTTP request failed with status code {response.StatusCode}.");

            else
                throw new HsdsException(statusCode, $"The HTTP request failed with status code {response.StatusCode}. The response message is: {message}");
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
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    return (await JsonSerializer.DeserializeAsync<T>(stream, Utilities.JsonOptions).ConfigureAwait(false))!;
                }
                catch (Exception ex)
                {
                    throw new HsdsException("01", "Response data could not be deserialized.", ex);
                }
            }
        }
        finally
        {
            if (typeof(T) != typeof(HttpResponseMessage))
                response.Dispose();
        }
    }


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


        return requestMessage;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _httpClient?.Dispose();
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

}

namespace Hsds.Api.V2_0
{

/// <summary>
/// A client for version V2_0.
/// </summary>
public interface IV2_0
{
    /// <summary>
    /// Gets the <see cref="IDomainClient"/>.
    /// </summary>
    IDomainClient Domain { get; }

    /// <summary>
    /// Gets the <see cref="IGroupClient"/>.
    /// </summary>
    IGroupClient Group { get; }

    /// <summary>
    /// Gets the <see cref="ILinkClient"/>.
    /// </summary>
    ILinkClient Link { get; }

    /// <summary>
    /// Gets the <see cref="IDatasetClient"/>.
    /// </summary>
    IDatasetClient Dataset { get; }

    /// <summary>
    /// Gets the <see cref="IDatatypeClient"/>.
    /// </summary>
    IDatatypeClient Datatype { get; }

    /// <summary>
    /// Gets the <see cref="IAttributeClient"/>.
    /// </summary>
    IAttributeClient Attribute { get; }

    /// <summary>
    /// Gets the <see cref="IACLSClient"/>.
    /// </summary>
    IACLSClient ACLS { get; }


}

/// <inheritdoc />
public class V2_0 : IV2_0
{
    /// <summary>
    /// Initializes a new instance of the <see cref="V2_0"/>.
    /// </summary>
    /// <param name="client">The client to use.</param>
    public V2_0(HsdsClient client)
    {
        Domain = new DomainClient(client);
        Group = new GroupClient(client);
        Link = new LinkClient(client);
        Dataset = new DatasetClient(client);
        Datatype = new DatatypeClient(client);
        Attribute = new AttributeClient(client);
        ACLS = new ACLSClient(client);

    }

    /// <inheritdoc />
    public IDomainClient Domain { get; }

    /// <inheritdoc />
    public IGroupClient Group { get; }

    /// <inheritdoc />
    public ILinkClient Link { get; }

    /// <inheritdoc />
    public IDatasetClient Dataset { get; }

    /// <inheritdoc />
    public IDatatypeClient Datatype { get; }

    /// <inheritdoc />
    public IAttributeClient Attribute { get; }

    /// <inheritdoc />
    public IACLSClient ACLS { get; }


}

/// <summary>
/// Provides methods to interact with domain.
/// </summary>
public interface IDomainClient
{
    /// <summary>
    /// Create a new Domain on the service.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="folder">If present and `1`, creates a Folder instead of a Domain.</param>
    /// <param name="body"></param>
    PutDomainResponse PutDomain(string domain, JsonElement? body, double? folder = default);

    /// <summary>
    /// Create a new Domain on the service.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="folder">If present and `1`, creates a Folder instead of a Domain.</param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PutDomainResponse> PutDomainAsync(string domain, JsonElement? body, double? folder = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about the requested domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetDomainResponse GetDomain(string domain);

    /// <summary>
    /// Get information about the requested domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetDomainResponse> GetDomainAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete the specified Domain or Folder.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    DeleteDomainResponse DeleteDomain(string domain);

    /// <summary>
    /// Delete the specified Domain or Folder.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<DeleteDomainResponse> DeleteDomainAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body"></param>
    PostGroupResponse PostGroup(string domain, JsonElement? body);

    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PostGroupResponse> PostGroupAsync(string domain, JsonElement? body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetGroupsResponse GetGroups(string domain);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetGroupsResponse> GetGroupsAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object describing the Dataset's properties.</param>
    PostDatasetResponse PostDataset(string domain, JsonElement body);

    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object describing the Dataset's properties.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PostDatasetResponse> PostDatasetAsync(string domain, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetDatasetsResponse GetDatasets(string domain);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetDatasetsResponse> GetDatasetsAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">Definition of Datatype to commit.</param>
    PostDataTypeResponse PostDataType(string domain, JsonElement body);

    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">Definition of Datatype to commit.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PostDataTypeResponse> PostDataTypeAsync(string domain, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetAccessListsResponse GetAccessLists(string domain);

    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetAccessListsResponse> GetAccessListsAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="user">User identifier/name.</param>
    GetUserAccessResponse GetUserAccess(string domain, string user);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="user">User identifier/name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetUserAccessResponse> GetUserAccessAsync(string domain, string user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.</param>
    PutUserAccessResponse PutUserAccess(string user, string domain, JsonElement body);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PutUserAccessResponse> PutUserAccessAsync(string user, string domain, JsonElement body, CancellationToken cancellationToken = default);

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
    public PutDomainResponse PutDomain(string domain, JsonElement? body, double? folder = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (folder is not null)
            __queryValues["folder"] = Uri.EscapeDataString(Convert.ToString(folder, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PutDomainResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PutDomainResponse> PutDomainAsync(string domain, JsonElement? body, double? folder = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (folder is not null)
            __queryValues["folder"] = Uri.EscapeDataString(Convert.ToString(folder, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PutDomainResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetDomainResponse GetDomain(string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetDomainResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetDomainResponse> GetDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetDomainResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public DeleteDomainResponse DeleteDomain(string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<DeleteDomainResponse>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<DeleteDomainResponse> DeleteDomainAsync(string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<DeleteDomainResponse>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PostGroupResponse PostGroup(string domain, JsonElement? body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PostGroupResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PostGroupResponse> PostGroupAsync(string domain, JsonElement? body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PostGroupResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetGroupsResponse GetGroups(string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetGroupsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetGroupsResponse> GetGroupsAsync(string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetGroupsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PostDatasetResponse PostDataset(string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PostDatasetResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PostDatasetResponse> PostDatasetAsync(string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PostDatasetResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetDatasetsResponse GetDatasets(string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetDatasetsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetDatasetsResponse> GetDatasetsAsync(string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetDatasetsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PostDataTypeResponse PostDataType(string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PostDataTypeResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PostDataTypeResponse> PostDataTypeAsync(string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PostDataTypeResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetAccessListsResponse GetAccessLists(string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetAccessListsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetAccessListsResponse> GetAccessListsAsync(string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetAccessListsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetUserAccessResponse GetUserAccess(string domain, string user)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetUserAccessResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetUserAccessResponse> GetUserAccessAsync(string domain, string user, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetUserAccessResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PutUserAccessResponse PutUserAccess(string user, string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PutUserAccessResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PutUserAccessResponse> PutUserAccessAsync(string user, string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PutUserAccessResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with group.
/// </summary>
public interface IGroupClient
{
    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body"></param>
    PostGroupResponse PostGroup(string domain, JsonElement? body);

    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PostGroupResponse> PostGroupAsync(string domain, JsonElement? body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetGroupsResponse GetGroups(string domain);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetGroupsResponse> GetGroupsAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="getalias">Optional body content, gets the alias (path name(s) from root) of the group as part of the response. Only includes paths as reached via _hard_ Links.</param>
    GetGroupResponse GetGroup(string id, string domain, int? getalias = default);

    /// <summary>
    /// Get information about a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="getalias">Optional body content, gets the alias (path name(s) from root) of the group as part of the response. Only includes paths as reached via _hard_ Links.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetGroupResponse> GetGroupAsync(string id, string domain, int? getalias = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    DeleteGroupResponse DeleteGroup(string id, string domain);

    /// <summary>
    /// Delete a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<DeleteGroupResponse> DeleteGroupAsync(string id, string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    GetAttributesResponse GetAttributes(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetAttributesResponse> GetAttributesAsync(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    PutAttributeResponse PutAttribute(string domain, string collection, string obj_uuid, string attr, JsonElement body);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PutAttributeResponse> PutAttributeAsync(string domain, string collection, string obj_uuid, string attr, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    AttributeType GetAttribute(string domain, string collection, string obj_uuid, string attr);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<AttributeType> GetAttributeAsync(string domain, string collection, string obj_uuid, string attr, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetGroupAccessListsResponse GetGroupAccessLists(string id, string domain);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetGroupAccessListsResponse> GetGroupAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetGroupUserAccessResponse GetGroupUserAccess(string id, string user, string domain);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetGroupUserAccessResponse> GetGroupUserAccessAsync(string id, string user, string domain, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class GroupClient : IGroupClient
{
    private HsdsClient ___client;
    
    internal GroupClient(HsdsClient client)
    {
        ___client = client;
    }

    /// <inheritdoc />
    public PostGroupResponse PostGroup(string domain, JsonElement? body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PostGroupResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PostGroupResponse> PostGroupAsync(string domain, JsonElement? body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PostGroupResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetGroupsResponse GetGroups(string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetGroupsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetGroupsResponse> GetGroupsAsync(string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetGroupsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetGroupResponse GetGroup(string id, string domain, int? getalias = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (getalias is not null)
            __queryValues["getalias"] = Uri.EscapeDataString(Convert.ToString(getalias, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetGroupResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetGroupResponse> GetGroupAsync(string id, string domain, int? getalias = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (getalias is not null)
            __queryValues["getalias"] = Uri.EscapeDataString(Convert.ToString(getalias, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetGroupResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public DeleteGroupResponse DeleteGroup(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<DeleteGroupResponse>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<DeleteGroupResponse> DeleteGroupAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<DeleteGroupResponse>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetAttributesResponse GetAttributes(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetAttributesResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetAttributesResponse> GetAttributesAsync(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetAttributesResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PutAttributeResponse PutAttribute(string domain, string collection, string obj_uuid, string attr, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PutAttributeResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PutAttributeResponse> PutAttributeAsync(string domain, string collection, string obj_uuid, string attr, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PutAttributeResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public AttributeType GetAttribute(string domain, string collection, string obj_uuid, string attr)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<AttributeType>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<AttributeType> GetAttributeAsync(string domain, string collection, string obj_uuid, string attr, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<AttributeType>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetGroupAccessListsResponse GetGroupAccessLists(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetGroupAccessListsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetGroupAccessListsResponse> GetGroupAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetGroupAccessListsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetGroupUserAccessResponse GetGroupUserAccess(string id, string user, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls/{user}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetGroupUserAccessResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetGroupUserAccessResponse> GetGroupUserAccessAsync(string id, string user, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls/{user}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetGroupUserAccessResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with link.
/// </summary>
public interface ILinkClient
{
    /// <summary>
    /// List all Links in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="Limit">Cap the number of Links returned in list.</param>
    /// <param name="Marker">Title of a Link; the first Link name to list.</param>
    GetLinksResponse GetLinks(string id, string domain, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Links in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="Limit">Cap the number of Links returned in list.</param>
    /// <param name="Marker">Title of a Link; the first Link name to list.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetLinksResponse> GetLinksAsync(string id, string domain, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new Link in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname">URL-encoded name of the Link. Label/name/title of the Link, e.g., `dset1` or `group3`. `linkname` cannot contain slashes.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object describing the Link to create.</param>
    PutLinkResponse PutLink(string id, string linkname, string domain, JsonElement body);

    /// <summary>
    /// Create a new Link in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname">URL-encoded name of the Link. Label/name/title of the Link, e.g., `dset1` or `group3`. `linkname` cannot contain slashes.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object describing the Link to create.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PutLinkResponse> PutLinkAsync(string id, string linkname, string domain, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Link info.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname">URL-encoded name of the Link. Label/name/title of the Link, e.g., `dset1` or `group3`. `linkname` cannot contain slashes.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetLinkResponse GetLink(string id, string linkname, string domain);

    /// <summary>
    /// Get Link info.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname">URL-encoded name of the Link. Label/name/title of the Link, e.g., `dset1` or `group3`. `linkname` cannot contain slashes.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetLinkResponse> GetLinkAsync(string id, string linkname, string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete Link.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname">URL-encoded name of the Link. Label/name/title of the Link, e.g., `dset1` or `group3`. `linkname` cannot contain slashes.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    DeleteLinkResponse DeleteLink(string id, string linkname, string domain);

    /// <summary>
    /// Delete Link.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname">URL-encoded name of the Link. Label/name/title of the Link, e.g., `dset1` or `group3`. `linkname` cannot contain slashes.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<DeleteLinkResponse> DeleteLinkAsync(string id, string linkname, string domain, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class LinkClient : ILinkClient
{
    private HsdsClient ___client;
    
    internal LinkClient(HsdsClient client)
    {
        ___client = client;
    }

    /// <inheritdoc />
    public GetLinksResponse GetLinks(string id, string domain, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetLinksResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetLinksResponse> GetLinksAsync(string id, string domain, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetLinksResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PutLinkResponse PutLink(string id, string linkname, string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PutLinkResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PutLinkResponse> PutLinkAsync(string id, string linkname, string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PutLinkResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetLinkResponse GetLink(string id, string linkname, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetLinkResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetLinkResponse> GetLinkAsync(string id, string linkname, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetLinkResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public DeleteLinkResponse DeleteLink(string id, string linkname, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<DeleteLinkResponse>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<DeleteLinkResponse> DeleteLinkAsync(string id, string linkname, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<DeleteLinkResponse>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with dataset.
/// </summary>
public interface IDatasetClient
{
    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object describing the Dataset's properties.</param>
    PostDatasetResponse PostDataset(string domain, JsonElement body);

    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object describing the Dataset's properties.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PostDatasetResponse> PostDatasetAsync(string domain, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetDatasetsResponse GetDatasets(string domain);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetDatasetsResponse> GetDatasetsAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetDatasetResponse GetDataset(string id, string domain);

    /// <summary>
    /// Get information about a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetDatasetResponse> GetDatasetAsync(string id, string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    DeleteDatasetResponse DeleteDataset(string id, string domain);

    /// <summary>
    /// Delete a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<DeleteDatasetResponse> DeleteDatasetAsync(string id, string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modify a Dataset's dimensions.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">Array of nonzero integers.</param>
    PutShapeResponse PutShape(string id, string domain, JsonElement body);

    /// <summary>
    /// Modify a Dataset's dimensions.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">Array of nonzero integers.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PutShapeResponse> PutShapeAsync(string id, string domain, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset's shape.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetShapeResponse GetShape(string id, string domain);

    /// <summary>
    /// Get information about a Dataset's shape.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetShapeResponse> GetShapeAsync(string id, string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset's type.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetDataTypeResponse GetDataType(string id, string domain);

    /// <summary>
    /// Get information about a Dataset's type.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetDataTypeResponse> GetDataTypeAsync(string id, string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Write values to Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object describing what to write.</param>
    void PutValues(string id, string domain, JsonElement body);

    /// <summary>
    /// Write values to Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object describing what to write.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task PutValuesAsync(string id, string domain, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get values from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="select">URL-encoded string representing a selection array.</param>
    /// <param name="query">URL-encoded string of conditional expression to filter selection.</param>
    /// <param name="Limit">Integer greater than zero.</param>
    HttpResponseMessage GetValuesAsStream(string id, string domain, string? select = default, string? query = default, double? Limit = default);

    /// <summary>
    /// Get values from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="select">URL-encoded string representing a selection array.</param>
    /// <param name="query">URL-encoded string of conditional expression to filter selection.</param>
    /// <param name="Limit">Integer greater than zero.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<HttpResponseMessage> GetValuesAsStreamAsync(string id, string domain, string? select = default, string? query = default, double? Limit = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get values from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="select">URL-encoded string representing a selection array.</param>
    /// <param name="query">URL-encoded string of conditional expression to filter selection.</param>
    /// <param name="Limit">Integer greater than zero.</param>
    GetValuesAsJsonResponse GetValuesAsJson(string id, string domain, string? select = default, string? query = default, double? Limit = default);

    /// <summary>
    /// Get values from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="select">URL-encoded string representing a selection array.</param>
    /// <param name="query">URL-encoded string of conditional expression to filter selection.</param>
    /// <param name="Limit">Integer greater than zero.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetValuesAsJsonResponse> GetValuesAsJsonAsync(string id, string domain, string? select = default, string? query = default, double? Limit = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get specific data points from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON array of coordinates in the Dataset.</param>
    PostValuesAsJsonResponse PostValuesAsJson(string id, string domain, JsonElement body);

    /// <summary>
    /// Get specific data points from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON array of coordinates in the Dataset.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PostValuesAsJsonResponse> PostValuesAsJsonAsync(string id, string domain, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get specific data points from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON array of coordinates in the Dataset.</param>
    HttpResponseMessage PostValuesAsStream(string id, string domain, JsonElement body);

    /// <summary>
    /// Get specific data points from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON array of coordinates in the Dataset.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<HttpResponseMessage> PostValuesAsStreamAsync(string id, string domain, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    GetAttributesResponse GetAttributes(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetAttributesResponse> GetAttributesAsync(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    PutAttributeResponse PutAttribute(string domain, string collection, string obj_uuid, string attr, JsonElement body);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PutAttributeResponse> PutAttributeAsync(string domain, string collection, string obj_uuid, string attr, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    AttributeType GetAttribute(string domain, string collection, string obj_uuid, string attr);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<AttributeType> GetAttributeAsync(string domain, string collection, string obj_uuid, string attr, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetDatasetAccessListsResponse GetDatasetAccessLists(string id, string domain);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetDatasetAccessListsResponse> GetDatasetAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class DatasetClient : IDatasetClient
{
    private HsdsClient ___client;
    
    internal DatasetClient(HsdsClient client)
    {
        ___client = client;
    }

    /// <inheritdoc />
    public PostDatasetResponse PostDataset(string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PostDatasetResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PostDatasetResponse> PostDatasetAsync(string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PostDatasetResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetDatasetsResponse GetDatasets(string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetDatasetsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetDatasetsResponse> GetDatasetsAsync(string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetDatasetsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetDatasetResponse GetDataset(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetDatasetResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetDatasetResponse> GetDatasetAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetDatasetResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public DeleteDatasetResponse DeleteDataset(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<DeleteDatasetResponse>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<DeleteDatasetResponse> DeleteDatasetAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<DeleteDatasetResponse>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PutShapeResponse PutShape(string id, string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PutShapeResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PutShapeResponse> PutShapeAsync(string id, string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PutShapeResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetShapeResponse GetShape(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetShapeResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetShapeResponse> GetShapeAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetShapeResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetDataTypeResponse GetDataType(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/type");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetDataTypeResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetDataTypeResponse> GetDataTypeAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/type");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetDataTypeResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public void PutValues(string id, string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        ___client.Invoke<object>("PUT", __url, default, "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task PutValuesAsync(string id, string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<object>("PUT", __url, default, "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public HttpResponseMessage GetValuesAsStream(string id, string domain, string? select = default, string? query = default, double? Limit = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (select is not null)
            __queryValues["select"] = Uri.EscapeDataString(select);

        if (query is not null)
            __queryValues["query"] = Uri.EscapeDataString(query);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<HttpResponseMessage>("GET", __url, "application/octet-stream", default, default);
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetValuesAsStreamAsync(string id, string domain, string? select = default, string? query = default, double? Limit = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (select is not null)
            __queryValues["select"] = Uri.EscapeDataString(select);

        if (query is not null)
            __queryValues["query"] = Uri.EscapeDataString(query);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<HttpResponseMessage>("GET", __url, "application/octet-stream", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetValuesAsJsonResponse GetValuesAsJson(string id, string domain, string? select = default, string? query = default, double? Limit = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (select is not null)
            __queryValues["select"] = Uri.EscapeDataString(select);

        if (query is not null)
            __queryValues["query"] = Uri.EscapeDataString(query);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetValuesAsJsonResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetValuesAsJsonResponse> GetValuesAsJsonAsync(string id, string domain, string? select = default, string? query = default, double? Limit = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (select is not null)
            __queryValues["select"] = Uri.EscapeDataString(select);

        if (query is not null)
            __queryValues["query"] = Uri.EscapeDataString(query);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetValuesAsJsonResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PostValuesAsJsonResponse PostValuesAsJson(string id, string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PostValuesAsJsonResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PostValuesAsJsonResponse> PostValuesAsJsonAsync(string id, string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PostValuesAsJsonResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public HttpResponseMessage PostValuesAsStream(string id, string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<HttpResponseMessage>("POST", __url, "application/octet-stream", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> PostValuesAsStreamAsync(string id, string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<HttpResponseMessage>("POST", __url, "application/octet-stream", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetAttributesResponse GetAttributes(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetAttributesResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetAttributesResponse> GetAttributesAsync(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetAttributesResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PutAttributeResponse PutAttribute(string domain, string collection, string obj_uuid, string attr, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PutAttributeResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PutAttributeResponse> PutAttributeAsync(string domain, string collection, string obj_uuid, string attr, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PutAttributeResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public AttributeType GetAttribute(string domain, string collection, string obj_uuid, string attr)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<AttributeType>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<AttributeType> GetAttributeAsync(string domain, string collection, string obj_uuid, string attr, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<AttributeType>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetDatasetAccessListsResponse GetDatasetAccessLists(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetDatasetAccessListsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetDatasetAccessListsResponse> GetDatasetAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetDatasetAccessListsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with datatype.
/// </summary>
public interface IDatatypeClient
{
    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">Definition of Datatype to commit.</param>
    PostDataTypeResponse PostDataType(string domain, JsonElement body);

    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">Definition of Datatype to commit.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PostDataTypeResponse> PostDataTypeAsync(string domain, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a committed Datatype
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="id">UUID of the committed datatype.</param>
    GetDatatypeResponse GetDatatype(string domain, string id);

    /// <summary>
    /// Get information about a committed Datatype
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetDatatypeResponse> GetDatatypeAsync(string domain, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a committed Datatype.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="id">UUID of the committed datatype.</param>
    DeleteDatatypeResponse DeleteDatatype(string domain, string id);

    /// <summary>
    /// Delete a committed Datatype.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<DeleteDatatypeResponse> DeleteDatatypeAsync(string domain, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    GetAttributesResponse GetAttributes(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetAttributesResponse> GetAttributesAsync(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    PutAttributeResponse PutAttribute(string domain, string collection, string obj_uuid, string attr, JsonElement body);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PutAttributeResponse> PutAttributeAsync(string domain, string collection, string obj_uuid, string attr, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    AttributeType GetAttribute(string domain, string collection, string obj_uuid, string attr);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<AttributeType> GetAttributeAsync(string domain, string collection, string obj_uuid, string attr, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetDataTypeAccessListsResponse GetDataTypeAccessLists(string id, string domain);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetDataTypeAccessListsResponse> GetDataTypeAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class DatatypeClient : IDatatypeClient
{
    private HsdsClient ___client;
    
    internal DatatypeClient(HsdsClient client)
    {
        ___client = client;
    }

    /// <inheritdoc />
    public PostDataTypeResponse PostDataType(string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PostDataTypeResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PostDataTypeResponse> PostDataTypeAsync(string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PostDataTypeResponse>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetDatatypeResponse GetDatatype(string domain, string id)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetDatatypeResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetDatatypeResponse> GetDatatypeAsync(string domain, string id, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetDatatypeResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public DeleteDatatypeResponse DeleteDatatype(string domain, string id)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<DeleteDatatypeResponse>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<DeleteDatatypeResponse> DeleteDatatypeAsync(string domain, string id, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<DeleteDatatypeResponse>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetAttributesResponse GetAttributes(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetAttributesResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetAttributesResponse> GetAttributesAsync(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetAttributesResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PutAttributeResponse PutAttribute(string domain, string collection, string obj_uuid, string attr, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PutAttributeResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PutAttributeResponse> PutAttributeAsync(string domain, string collection, string obj_uuid, string attr, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PutAttributeResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public AttributeType GetAttribute(string domain, string collection, string obj_uuid, string attr)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<AttributeType>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<AttributeType> GetAttributeAsync(string domain, string collection, string obj_uuid, string attr, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<AttributeType>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetDataTypeAccessListsResponse GetDataTypeAccessLists(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetDataTypeAccessListsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetDataTypeAccessListsResponse> GetDataTypeAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetDataTypeAccessListsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with attribute.
/// </summary>
public interface IAttributeClient
{
    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    GetAttributesResponse GetAttributes(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetAttributesResponse> GetAttributesAsync(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    PutAttributeResponse PutAttribute(string domain, string collection, string obj_uuid, string attr, JsonElement body);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PutAttributeResponse> PutAttributeAsync(string domain, string collection, string obj_uuid, string attr, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    AttributeType GetAttribute(string domain, string collection, string obj_uuid, string attr);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<AttributeType> GetAttributeAsync(string domain, string collection, string obj_uuid, string attr, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class AttributeClient : IAttributeClient
{
    private HsdsClient ___client;
    
    internal AttributeClient(HsdsClient client)
    {
        ___client = client;
    }

    /// <inheritdoc />
    public GetAttributesResponse GetAttributes(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetAttributesResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetAttributesResponse> GetAttributesAsync(string collection, string obj_uuid, string domain, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetAttributesResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PutAttributeResponse PutAttribute(string domain, string collection, string obj_uuid, string attr, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PutAttributeResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PutAttributeResponse> PutAttributeAsync(string domain, string collection, string obj_uuid, string attr, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PutAttributeResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public AttributeType GetAttribute(string domain, string collection, string obj_uuid, string attr)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<AttributeType>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<AttributeType> GetAttributeAsync(string domain, string collection, string obj_uuid, string attr, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<AttributeType>("GET", __url, "application/json", default, default, cancellationToken);
    }

}

/// <summary>
/// Provides methods to interact with acls.
/// </summary>
public interface IACLSClient
{
    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetAccessListsResponse GetAccessLists(string domain);

    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetAccessListsResponse> GetAccessListsAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="user">User identifier/name.</param>
    GetUserAccessResponse GetUserAccess(string domain, string user);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="user">User identifier/name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetUserAccessResponse> GetUserAccessAsync(string domain, string user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.</param>
    PutUserAccessResponse PutUserAccess(string user, string domain, JsonElement body);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<PutUserAccessResponse> PutUserAccessAsync(string user, string domain, JsonElement body, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetGroupAccessListsResponse GetGroupAccessLists(string id, string domain);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetGroupAccessListsResponse> GetGroupAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetGroupUserAccessResponse GetGroupUserAccess(string id, string user, string domain);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetGroupUserAccessResponse> GetGroupUserAccessAsync(string id, string user, string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetDatasetAccessListsResponse GetDatasetAccessLists(string id, string domain);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetDatasetAccessListsResponse> GetDatasetAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    GetDataTypeAccessListsResponse GetDataTypeAccessLists(string id, string domain);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain">Domain on service to access, e.g., `/home/user/someproject/somefile`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<GetDataTypeAccessListsResponse> GetDataTypeAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default);

}

/// <inheritdoc />
public class ACLSClient : IACLSClient
{
    private HsdsClient ___client;
    
    internal ACLSClient(HsdsClient client)
    {
        ___client = client;
    }

    /// <inheritdoc />
    public GetAccessListsResponse GetAccessLists(string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetAccessListsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetAccessListsResponse> GetAccessListsAsync(string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetAccessListsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetUserAccessResponse GetUserAccess(string domain, string user)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetUserAccessResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetUserAccessResponse> GetUserAccessAsync(string domain, string user, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetUserAccessResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public PutUserAccessResponse PutUserAccess(string user, string domain, JsonElement body)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<PutUserAccessResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<PutUserAccessResponse> PutUserAccessAsync(string user, string domain, JsonElement body, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<PutUserAccessResponse>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public GetGroupAccessListsResponse GetGroupAccessLists(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetGroupAccessListsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetGroupAccessListsResponse> GetGroupAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetGroupAccessListsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetGroupUserAccessResponse GetGroupUserAccess(string id, string user, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls/{user}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetGroupUserAccessResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetGroupUserAccessResponse> GetGroupUserAccessAsync(string id, string user, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls/{user}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetGroupUserAccessResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetDatasetAccessListsResponse GetDatasetAccessLists(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetDatasetAccessListsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetDatasetAccessListsResponse> GetDatasetAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetDatasetAccessListsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public GetDataTypeAccessListsResponse GetDataTypeAccessLists(string id, string domain)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<GetDataTypeAccessListsResponse>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<GetDataTypeAccessListsResponse> GetDataTypeAccessListsAsync(string id, string domain, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<GetDataTypeAccessListsResponse>("GET", __url, "application/json", default, default, cancellationToken);
    }

}



/// <summary>
/// Access Control List for a single user.
/// </summary>
/// <param name="Username"></param>
public record ACL(ACLUsernameType Username);

/// <summary>
/// Access Control Lists for users.
/// </summary>
/// <param name="ForWhom">Access Control List for a single user.</param>
public record ACLS(ACL ForWhom);

/// <summary>
/// A href.
/// </summary>
/// <param name="Href">URL of the resource.</param>
/// <param name="Rel">Relation to this object.</param>
public record HrefType(string Href, string Rel);

/// <summary>
/// A shape.
/// </summary>
/// <param name="Class">The shape class.</param>
/// <param name="Dims">The shape dimensions.</param>
/// <param name="Maxdims">The shape maximum dimensions.</param>
public record ShapeType(string Class, IReadOnlyList<int>? Dims, IReadOnlyList<double>? Maxdims);

/// <summary>
/// A type.
/// </summary>
/// <param name="Class">The type class.</param>
/// <param name="Base">The base type class.</param>
/// <param name="Fields">List of fields in a compound dataset.</param>
public record TypeType(string Class, string? Base, IReadOnlyList<TypeTypeFieldsType>? Fields);

/// <summary>
/// A layout.
/// </summary>
/// <param name="Class">The layout class.</param>
/// <param name="Dims">The chunk dimensions.</param>
public record LayoutType(string Class, IReadOnlyList<int>? Dims);

/// <summary>
/// An attribute.
/// </summary>
/// <param name="Created">The creation date.</param>
/// <param name="LastModified">The date of last modification.</param>
/// <param name="Name">The name.</param>
/// <param name="Shape">The shape.</param>
/// <param name="Type">The type.</param>
/// <param name="Value">The values.</param>
/// <param name="Href">Link to the attribute.</param>
/// <param name="Hrefs">A collection of relations.</param>
public record AttributeType(double Created, double? LastModified, string Name, ShapeType Shape, TypeType Type, JsonElement? Value, string? Href, IReadOnlyList<HrefType>? Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Created">When domain was created.</param>
/// <param name="LastModified">When object was last modified.</param>
/// <param name="Owner">Name of owner.</param>
/// <param name="Root">ID of root group.</param>
public record PutDomainResponse(ACLS Acls, double Created, double LastModified, string Owner, string Root);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this Domain.</param>
public record GetDomainResponseHrefsType(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Root">UUID of root Group. If Domain is of class 'folder', this entry is not present.</param>
/// <param name="Owner"></param>
/// <param name="Class">Category of Domain. If 'folder' no root group is included in response.</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Hrefs">Array of url references and their relation to this Domain. Should include entries for: `acls`, `database` (if not class is not `folder`), `groupbase` (if not class is not `folder`), `parent`, `root` (if not class is not `folder`), `self`, `typebase` (if not class is not `folder`).</param>
public record GetDomainResponse(string Root, string Owner, string Class, double Created, double LastModified, IReadOnlyList<GetDomainResponseHrefsType> Hrefs);

/// <summary>
/// The Domain or Folder which was deleted.
/// </summary>
/// <param name="Domain">domain path</param>
public record DeleteDomainResponse(string Domain);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of new Group.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
public record PostGroupResponse(string Id, string Root, double LastModified, double Created, double AttributeCount, double LinkCount);

/// <summary>
/// References to other objects.
/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record GetGroupsResponseHrefsType(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Groups"></param>
/// <param name="Hrefs"></param>
public record GetGroupsResponse(IReadOnlyList<string> Groups, IReadOnlyList<GetGroupsResponseHrefsType> Hrefs);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record PostDatasetResponseTypeType();

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record PostDatasetResponseShapeType();

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of this Dataset.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="AttributeCount"></param>
/// <param name="Type">(See `GET /datasets/{id}`)</param>
/// <param name="Shape">(See `GET /datasets/{id}`)</param>
public record PostDatasetResponse(string Id, string Root, double Created, double LastModified, double AttributeCount, PostDatasetResponseTypeType Type, PostDatasetResponseShapeType Shape);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record GetDatasetsResponseHrefsType(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Datasets"></param>
/// <param name="Hrefs">List of references to other objects.</param>
public record GetDatasetsResponse(IReadOnlyList<string> Datasets, IReadOnlyList<GetDatasetsResponseHrefsType> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Id"></param>
public record PostDataTypeResponse(double AttributeCount, string Id);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GetAccessListsResponseHrefsType(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record GetAccessListsResponse(ACLS Acls, IReadOnlyList<GetAccessListsResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GetUserAccessResponseHrefsType(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record GetUserAccessResponse(ACL Acl, IReadOnlyList<GetUserAccessResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record PutUserAccessResponseHrefsType(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record PutUserAccessResponse(ACL Acl, IReadOnlyList<PutUserAccessResponseHrefsType> Hrefs);

/// <summary>
/// References to other objects.
/// </summary>
/// <param name="Rel">Relation to this object.</param>
/// <param name="Href">URL to reference.</param>
public record GetGroupResponseHrefsType(string Rel, string Href);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of this Group.</param>
/// <param name="Root">UUID of root Group.</param>
/// <param name="Alias">List of aliases for the Group, as reached by _hard_ Links. If Group is unlinked, its alias list will be empty (`[]`).</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Domain"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
/// <param name="Hrefs">List of references to other objects.</param>
public record GetGroupResponse(string Id, string Root, IReadOnlyList<string> Alias, double Created, double LastModified, string Domain, double AttributeCount, double LinkCount, IReadOnlyList<GetGroupResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
public record DeleteGroupResponse();

/// <summary>
/// A list of attributes.
/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs">A collection of relations.</param>
public record GetAttributesResponse(IReadOnlyList<AttributeType> Attributes, IReadOnlyList<HrefType> Hrefs);

/// <summary>
/// TODO
/// </summary>
public record PutAttributeResponse();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GetGroupAccessListsResponseHrefsType(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record GetGroupAccessListsResponse(ACLS Acls, IReadOnlyList<GetGroupAccessListsResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GetGroupUserAccessResponseHrefsType(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record GetGroupUserAccessResponse(ACL Acl, IReadOnlyList<GetGroupUserAccessResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of Link target.</param>
/// <param name="Created"></param>
/// <param name="Class">Indicate whether this Link is hard, soft, or external.</param>
/// <param name="Title">Name/label/title of the Link, as provided upon creation.</param>
/// <param name="Target">URL of Link target.</param>
/// <param name="Href">URL to origin of Link.</param>
/// <param name="Collection">What kind of object is the target. (TODO)</param>
public record GetLinksResponseLinksType(string Id, double Created, string Class, string Title, string Target, string Href, string Collection);

/// <summary>
/// 
/// </summary>
/// <param name="Rel">Relation to this object.</param>
/// <param name="Href">URL to reference.</param>
public record GetLinksResponseHrefsType(string Rel, string Href);

/// <summary>
/// 
/// </summary>
/// <param name="Links"></param>
/// <param name="Hrefs">List of references to other entities.</param>
public record GetLinksResponse(IReadOnlyList<GetLinksResponseLinksType> Links, IReadOnlyList<GetLinksResponseHrefsType> Hrefs);

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record PutLinkResponse();

/// <summary>
/// 
/// </summary>
/// <param name="Id"></param>
/// <param name="Title"></param>
/// <param name="Collection"></param>
/// <param name="Class"></param>
public record GetLinkResponseLinkType(string Id, string Title, string Collection, string Class);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record GetLinkResponseHrefsType(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="Link"></param>
/// <param name="Hrefs">List of references to other entities.</param>
public record GetLinkResponse(double LastModified, double Created, GetLinkResponseLinkType Link, IReadOnlyList<GetLinkResponseHrefsType> Hrefs);

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record DeleteLinkResponse();

/// <summary>
/// Dataset creation properties as provided upon creation.
/// </summary>
public record GetDatasetResponseCreationPropertiesType();

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of this Dataset.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="Domain">The domain name.</param>
/// <param name="Created">The creation date.</param>
/// <param name="LastModified">The date of the last modification.</param>
/// <param name="AttributeCount">The number of attributes.</param>
/// <param name="Type">The type.</param>
/// <param name="Shape">The shape.</param>
/// <param name="Layout">The layout.</param>
/// <param name="CreationProperties">Dataset creation properties as provided upon creation.</param>
/// <param name="Hrefs">A collection of relations.</param>
public record GetDatasetResponse(string Id, string Root, string Domain, double Created, double LastModified, double AttributeCount, TypeType Type, ShapeType Shape, LayoutType Layout, GetDatasetResponseCreationPropertiesType CreationProperties, IReadOnlyList<HrefType> Hrefs);

/// <summary>
/// 
/// </summary>
public record DeleteDatasetResponse();

/// <summary>
/// 
/// </summary>
/// <param name="Hrefs"></param>
public record PutShapeResponse(IReadOnlyList<string> Hrefs);

/// <summary>
/// 
/// </summary>
public record GetShapeResponseShapeType();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GetShapeResponseHrefsType(string Href, string Rel);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Shape"></param>
/// <param name="Hrefs">Must include references to only: `owner`, `root`, `self`.</param>
public record GetShapeResponse(double Created, double LastModified, GetShapeResponseShapeType Shape, IReadOnlyList<GetShapeResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
public record GetDataTypeResponseTypeType();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GetDataTypeResponseHrefsType(string Href, string Rel);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
/// <param name="Type"></param>
/// <param name="Hrefs"></param>
public record GetDataTypeResponse(GetDataTypeResponseTypeType Type, IReadOnlyList<GetDataTypeResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Index">List of indices (TODO: coordinates?) corresponding with each value returned. i.e., `index[i]` is the coordinate of `value[i]`.</param>
/// <param name="Value"></param>
public record GetValuesAsJsonResponse(IReadOnlyList<string> Index, IReadOnlyList<JsonElement> Value);

/// <summary>
/// 
/// </summary>
/// <param name="Value"></param>
public record PostValuesAsJsonResponse(IReadOnlyList<JsonElement> Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GetDatasetAccessListsResponseHrefsType(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record GetDatasetAccessListsResponse(ACLS Acls, IReadOnlyList<GetDatasetAccessListsResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
public record GetDatatypeResponseTypeType();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GetDatatypeResponseHrefsType(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Created"></param>
/// <param name="Id"></param>
/// <param name="LastModified"></param>
/// <param name="Root"></param>
/// <param name="Type"></param>
/// <param name="Hrefs">TODO</param>
public record GetDatatypeResponse(double AttributeCount, double Created, string Id, double LastModified, string Root, GetDatatypeResponseTypeType Type, IReadOnlyList<GetDatatypeResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record DeleteDatatypeResponseHrefsType(string Href, string Rel);

/// <summary>
/// Always returns `{"hrefs": []}` (TODO confirm)
/// </summary>
/// <param name="Hrefs"></param>
public record DeleteDatatypeResponse(IReadOnlyList<DeleteDatatypeResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">Relation to `href`.</param>
public record GetDataTypeAccessListsResponseHrefsType(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record GetDataTypeAccessListsResponse(ACLS Acls, IReadOnlyList<GetDataTypeAccessListsResponseHrefsType> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Create"></param>
/// <param name="Update"></param>
/// <param name="Delete"></param>
/// <param name="UpdateACL"></param>
/// <param name="Read"></param>
/// <param name="ReadACL"></param>
public record ACLUsernameType(bool Create, bool Update, bool Delete, bool UpdateACL, bool Read, bool ReadACL);

/// <summary>
/// 
/// </summary>
/// <param name="Name">Descriptive or identifying name.</param>
/// <param name="Type">The type.</param>
public record TypeTypeFieldsType(string Name, TypeType Type);



}
