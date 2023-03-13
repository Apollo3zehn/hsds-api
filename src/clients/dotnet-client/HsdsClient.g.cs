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
public class HsdsClient : IHsdsClient, IDisposable
{
    private HttpClient _httpClient;

    private DomainClient _domain;
    private GroupClient _group;
    private LinkClient _link;
    private DatasetClient _dataset;
    private DatatypeClient _datatype;
    private AttributeClient _attribute;
    private ACLSClient _aCLS;

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
        _group = new GroupClient(this);
        _link = new LinkClient(this);
        _dataset = new DatasetClient(this);
        _datatype = new DatatypeClient(this);
        _attribute = new AttributeClient(this);
        _aCLS = new ACLSClient(this);

    }


    /// <inheritdoc />
    public IDomainClient Domain => _domain;

    /// <inheritdoc />
    public IGroupClient Group => _group;

    /// <inheritdoc />
    public ILinkClient Link => _link;

    /// <inheritdoc />
    public IDatasetClient Dataset => _dataset;

    /// <inheritdoc />
    public IDatatypeClient Datatype => _datatype;

    /// <inheritdoc />
    public IAttributeClient Attribute => _attribute;

    /// <inheritdoc />
    public IACLSClient ACLS => _aCLS;





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
    /// Create a new Domain on the service.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="folder">If present and `1`, creates a Folder instead of a Domain.</param>
    /// <param name="body"></param>
    IReadOnlyDictionary<string, PutDomainResponse> PutDomain(JsonElement body, string? domain = default, double? folder = default);

    /// <summary>
    /// Create a new Domain on the service.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="folder">If present and `1`, creates a Folder instead of a Domain.</param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PutDomainResponse>> PutDomainAsync(JsonElement body, string? domain = default, double? folder = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about the requested domain.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetDomainResponse> GetDomain(string? domain = default);

    /// <summary>
    /// Get information about the requested domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetDomainResponse>> GetDomainAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete the specified Domain or Folder.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, DeleteDomainResponse> DeleteDomain(string? domain = default);

    /// <summary>
    /// Delete the specified Domain or Folder.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, DeleteDomainResponse>> DeleteDomainAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body"></param>
    IReadOnlyDictionary<string, PostGroupResponse> PostGroup(JsonElement body, string? domain = default);

    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PostGroupResponse>> PostGroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetGroupsResponse> GetGroups(string? domain = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetGroupsResponse>> GetGroupsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Dataset's properties.</param>
    IReadOnlyDictionary<string, PostDatasetResponse> PostDataset(JsonElement body, string? domain = default);

    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Dataset's properties.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PostDatasetResponse>> PostDatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetDatasetsResponse> GetDatasets(string? domain = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetDatasetsResponse>> GetDatasetsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">Definition of Datatype to commit.</param>
    IReadOnlyDictionary<string, PostDataTypeResponse> PostDataType(JsonElement body, string? domain = default);

    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">Definition of Datatype to commit.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PostDataTypeResponse>> PostDataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetAccessListsResponse> GetAccessLists(string? domain = default);

    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetAccessListsResponse>> GetAccessListsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="user">User identifier/name.</param>
    IReadOnlyDictionary<string, GetUserAccessResponse> GetUserAccess(string user, string? domain = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="user">User identifier/name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetUserAccessResponse>> GetUserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.</param>
    IReadOnlyDictionary<string, PutUserAccessResponse> PutUserAccess(string user, JsonElement body, string? domain = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PutUserAccessResponse>> PutUserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, PutDomainResponse> PutDomain(JsonElement body, string? domain = default, double? folder = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (folder is not null)
            __queryValues["folder"] = Uri.EscapeDataString(Convert.ToString(folder, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PutDomainResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PutDomainResponse>> PutDomainAsync(JsonElement body, string? domain = default, double? folder = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (folder is not null)
            __queryValues["folder"] = Uri.EscapeDataString(Convert.ToString(folder, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PutDomainResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetDomainResponse> GetDomain(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetDomainResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetDomainResponse>> GetDomainAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetDomainResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DeleteDomainResponse> DeleteDomain(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, DeleteDomainResponse>>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, DeleteDomainResponse>> DeleteDomainAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, DeleteDomainResponse>>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PostGroupResponse> PostGroup(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PostGroupResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PostGroupResponse>> PostGroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PostGroupResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetGroupsResponse> GetGroups(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetGroupsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetGroupsResponse>> GetGroupsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetGroupsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PostDatasetResponse> PostDataset(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PostDatasetResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PostDatasetResponse>> PostDatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PostDatasetResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetDatasetsResponse> GetDatasets(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetDatasetsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetDatasetsResponse>> GetDatasetsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetDatasetsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PostDataTypeResponse> PostDataType(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PostDataTypeResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PostDataTypeResponse>> PostDataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PostDataTypeResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetAccessListsResponse> GetAccessLists(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetAccessListsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetAccessListsResponse>> GetAccessListsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetAccessListsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetUserAccessResponse> GetUserAccess(string user, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetUserAccessResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetUserAccessResponse>> GetUserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetUserAccessResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PutUserAccessResponse> PutUserAccess(string user, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PutUserAccessResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PutUserAccessResponse>> PutUserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PutUserAccessResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
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
    /// <param name="domain"></param>
    /// <param name="body"></param>
    IReadOnlyDictionary<string, PostGroupResponse> PostGroup(JsonElement body, string? domain = default);

    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PostGroupResponse>> PostGroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetGroupsResponse> GetGroups(string? domain = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetGroupsResponse>> GetGroupsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="getalias"></param>
    IReadOnlyDictionary<string, GetGroupResponse> GetGroup(string id, string? domain = default, int? getalias = default);

    /// <summary>
    /// Get information about a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="getalias"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetGroupResponse>> GetGroupAsync(string id, string? domain = default, int? getalias = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, DeleteGroupResponse> DeleteGroup(string id, string? domain = default);

    /// <summary>
    /// Delete a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, DeleteGroupResponse>> DeleteGroupAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    IReadOnlyDictionary<string, GetAttributesResponse> GetAttributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetAttributesResponse>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    IReadOnlyDictionary<string, PutAttributeResponse> PutAttribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PutAttributeResponse>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    IReadOnlyDictionary<string, GetAttributeResponse> GetAttribute(string collection, string obj_uuid, string attr, string? domain = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetAttributeResponse>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetGroupAccessListsResponse> GetGroupAccessLists(string id, string? domain = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetGroupAccessListsResponse>> GetGroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetGroupUserAccessResponse> GetGroupUserAccess(string id, string user, string? domain = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetGroupUserAccessResponse>> GetGroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, PostGroupResponse> PostGroup(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PostGroupResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PostGroupResponse>> PostGroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PostGroupResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetGroupsResponse> GetGroups(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetGroupsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetGroupsResponse>> GetGroupsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetGroupsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetGroupResponse> GetGroup(string id, string? domain = default, int? getalias = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (getalias is not null)
            __queryValues["getalias"] = Uri.EscapeDataString(Convert.ToString(getalias, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetGroupResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetGroupResponse>> GetGroupAsync(string id, string? domain = default, int? getalias = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (getalias is not null)
            __queryValues["getalias"] = Uri.EscapeDataString(Convert.ToString(getalias, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetGroupResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DeleteGroupResponse> DeleteGroup(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, DeleteGroupResponse>>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, DeleteGroupResponse>> DeleteGroupAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, DeleteGroupResponse>>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetAttributesResponse> GetAttributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetAttributesResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetAttributesResponse>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetAttributesResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PutAttributeResponse> PutAttribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PutAttributeResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PutAttributeResponse>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PutAttributeResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetAttributeResponse> GetAttribute(string collection, string obj_uuid, string attr, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetAttributeResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetAttributeResponse>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetAttributeResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetGroupAccessListsResponse> GetGroupAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetGroupAccessListsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetGroupAccessListsResponse>> GetGroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetGroupAccessListsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetGroupUserAccessResponse> GetGroupUserAccess(string id, string user, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls/{user}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetGroupUserAccessResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetGroupUserAccessResponse>> GetGroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls/{user}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetGroupUserAccessResponse>>("GET", __url, "application/json", default, default, cancellationToken);
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
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Links returned in list.</param>
    /// <param name="Marker">Title of a Link; the first Link name to list.</param>
    IReadOnlyDictionary<string, GetLinksResponse> GetLinks(string id, string? domain = default, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Links in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Links returned in list.</param>
    /// <param name="Marker">Title of a Link; the first Link name to list.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetLinksResponse>> GetLinksAsync(string id, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new Link in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Link to create.</param>
    IReadOnlyDictionary<string, PutLinkResponse> PutLink(string id, string linkname, JsonElement body, string? domain = default);

    /// <summary>
    /// Create a new Link in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Link to create.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PutLinkResponse>> PutLinkAsync(string id, string linkname, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Link info.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetLinkResponse> GetLink(string id, string linkname, string? domain = default);

    /// <summary>
    /// Get Link info.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetLinkResponse>> GetLinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete Link.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, DeleteLinkResponse> DeleteLink(string id, string linkname, string? domain = default);

    /// <summary>
    /// Delete Link.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, DeleteLinkResponse>> DeleteLinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, GetLinksResponse> GetLinks(string id, string? domain = default, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetLinksResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetLinksResponse>> GetLinksAsync(string id, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetLinksResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PutLinkResponse> PutLink(string id, string linkname, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PutLinkResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PutLinkResponse>> PutLinkAsync(string id, string linkname, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PutLinkResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetLinkResponse> GetLink(string id, string linkname, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetLinkResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetLinkResponse>> GetLinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetLinkResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DeleteLinkResponse> DeleteLink(string id, string linkname, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, DeleteLinkResponse>>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, DeleteLinkResponse>> DeleteLinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links/{linkname}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{linkname}", Uri.EscapeDataString(linkname));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, DeleteLinkResponse>>("DELETE", __url, "application/json", default, default, cancellationToken);
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
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Dataset's properties.</param>
    IReadOnlyDictionary<string, PostDatasetResponse> PostDataset(JsonElement body, string? domain = default);

    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Dataset's properties.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PostDatasetResponse>> PostDatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetDatasetsResponse> GetDatasets(string? domain = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetDatasetsResponse>> GetDatasetsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetDatasetResponse> GetDataset(string id, string? domain = default);

    /// <summary>
    /// Get information about a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetDatasetResponse>> GetDatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, DeleteDatasetResponse> DeleteDataset(string id, string? domain = default);

    /// <summary>
    /// Delete a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, DeleteDatasetResponse>> DeleteDatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modify a Dataset's dimensions.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">Array of nonzero integers.</param>
    IReadOnlyDictionary<string, PutShapeResponse> PutShape(string id, JsonElement body, string? domain = default);

    /// <summary>
    /// Modify a Dataset's dimensions.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">Array of nonzero integers.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PutShapeResponse>> PutShapeAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset's shape.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetShapeResponse> GetShape(string id, string? domain = default);

    /// <summary>
    /// Get information about a Dataset's shape.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetShapeResponse>> GetShapeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset's type.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetDataTypeResponse> GetDataType(string id, string? domain = default);

    /// <summary>
    /// Get information about a Dataset's type.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetDataTypeResponse>> GetDataTypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Write values to Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing what to write.</param>
    void PutValues(string id, JsonElement body, string? domain = default);

    /// <summary>
    /// Write values to Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing what to write.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task PutValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get values from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="select">URL-encoded string representing a selection array.</param>
    /// <param name="query">URL-encoded string of conditional expression to filter selection.</param>
    /// <param name="Limit">Integer greater than zero.</param>
    HttpResponseMessage GetValues(string id, string? domain = default, string? select = default, string? query = default, double? Limit = default);

    /// <summary>
    /// Get values from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="select">URL-encoded string representing a selection array.</param>
    /// <param name="query">URL-encoded string of conditional expression to filter selection.</param>
    /// <param name="Limit">Integer greater than zero.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<HttpResponseMessage> GetValuesAsync(string id, string? domain = default, string? select = default, string? query = default, double? Limit = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get specific data points from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON array of coordinates in the Dataset.</param>
    IReadOnlyDictionary<string, PostValuesResponse> PostValues(string id, JsonElement body, string? domain = default);

    /// <summary>
    /// Get specific data points from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON array of coordinates in the Dataset.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PostValuesResponse>> PostValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    IReadOnlyDictionary<string, GetAttributesResponse> GetAttributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetAttributesResponse>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    IReadOnlyDictionary<string, PutAttributeResponse> PutAttribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PutAttributeResponse>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    IReadOnlyDictionary<string, GetAttributeResponse> GetAttribute(string collection, string obj_uuid, string attr, string? domain = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetAttributeResponse>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetDatasetAccessListsResponse> GetDatasetAccessLists(string id, string? domain = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetDatasetAccessListsResponse>> GetDatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, PostDatasetResponse> PostDataset(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PostDatasetResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PostDatasetResponse>> PostDatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PostDatasetResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetDatasetsResponse> GetDatasets(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetDatasetsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetDatasetsResponse>> GetDatasetsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetDatasetsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetDatasetResponse> GetDataset(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetDatasetResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetDatasetResponse>> GetDatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetDatasetResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DeleteDatasetResponse> DeleteDataset(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, DeleteDatasetResponse>>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, DeleteDatasetResponse>> DeleteDatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, DeleteDatasetResponse>>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PutShapeResponse> PutShape(string id, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PutShapeResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PutShapeResponse>> PutShapeAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PutShapeResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetShapeResponse> GetShape(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetShapeResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetShapeResponse>> GetShapeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetShapeResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetDataTypeResponse> GetDataType(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/type");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetDataTypeResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetDataTypeResponse>> GetDataTypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/type");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetDataTypeResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public void PutValues(string id, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        ___client.Invoke<object>("PUT", __url, default, "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task PutValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<object>("PUT", __url, default, "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public HttpResponseMessage GetValues(string id, string? domain = default, string? select = default, string? query = default, double? Limit = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
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
    public Task<HttpResponseMessage> GetValuesAsync(string id, string? domain = default, string? select = default, string? query = default, double? Limit = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
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
    public IReadOnlyDictionary<string, PostValuesResponse> PostValues(string id, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PostValuesResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PostValuesResponse>> PostValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PostValuesResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetAttributesResponse> GetAttributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetAttributesResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetAttributesResponse>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetAttributesResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PutAttributeResponse> PutAttribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PutAttributeResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PutAttributeResponse>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PutAttributeResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetAttributeResponse> GetAttribute(string collection, string obj_uuid, string attr, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetAttributeResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetAttributeResponse>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetAttributeResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetDatasetAccessListsResponse> GetDatasetAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetDatasetAccessListsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetDatasetAccessListsResponse>> GetDatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetDatasetAccessListsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
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
    /// <param name="domain"></param>
    /// <param name="body">Definition of Datatype to commit.</param>
    IReadOnlyDictionary<string, PostDataTypeResponse> PostDataType(JsonElement body, string? domain = default);

    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">Definition of Datatype to commit.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PostDataTypeResponse>> PostDataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a committed Datatype
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="id">UUID of the committed datatype.</param>
    IReadOnlyDictionary<string, GetDatatypeResponse> GetDatatype(string id, string? domain = default);

    /// <summary>
    /// Get information about a committed Datatype
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetDatatypeResponse>> GetDatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a committed Datatype.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="id">UUID of the committed datatype.</param>
    IReadOnlyDictionary<string, DeleteDatatypeResponse> DeleteDatatype(string id, string? domain = default);

    /// <summary>
    /// Delete a committed Datatype.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, DeleteDatatypeResponse>> DeleteDatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    IReadOnlyDictionary<string, GetAttributesResponse> GetAttributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetAttributesResponse>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    IReadOnlyDictionary<string, PutAttributeResponse> PutAttribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PutAttributeResponse>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    IReadOnlyDictionary<string, GetAttributeResponse> GetAttribute(string collection, string obj_uuid, string attr, string? domain = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetAttributeResponse>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetDataTypeAccessListsResponse> GetDataTypeAccessLists(string id, string? domain = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetDataTypeAccessListsResponse>> GetDataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, PostDataTypeResponse> PostDataType(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PostDataTypeResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PostDataTypeResponse>> PostDataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PostDataTypeResponse>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetDatatypeResponse> GetDatatype(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetDatatypeResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetDatatypeResponse>> GetDatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetDatatypeResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DeleteDatatypeResponse> DeleteDatatype(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, DeleteDatatypeResponse>>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, DeleteDatatypeResponse>> DeleteDatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, DeleteDatatypeResponse>>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetAttributesResponse> GetAttributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetAttributesResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetAttributesResponse>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetAttributesResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PutAttributeResponse> PutAttribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PutAttributeResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PutAttributeResponse>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PutAttributeResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetAttributeResponse> GetAttribute(string collection, string obj_uuid, string attr, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetAttributeResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetAttributeResponse>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetAttributeResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetDataTypeAccessListsResponse> GetDataTypeAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetDataTypeAccessListsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetDataTypeAccessListsResponse>> GetDataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetDataTypeAccessListsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
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
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    IReadOnlyDictionary<string, GetAttributesResponse> GetAttributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetAttributesResponse>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    IReadOnlyDictionary<string, PutAttributeResponse> PutAttribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PutAttributeResponse>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    IReadOnlyDictionary<string, GetAttributeResponse> GetAttribute(string collection, string obj_uuid, string attr, string? domain = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetAttributeResponse>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, GetAttributesResponse> GetAttributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetAttributesResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetAttributesResponse>> GetAttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        if (Limit is not null)
            __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        if (Marker is not null)
            __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetAttributesResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PutAttributeResponse> PutAttribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PutAttributeResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PutAttributeResponse>> PutAttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PutAttributeResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetAttributeResponse> GetAttribute(string collection, string obj_uuid, string attr, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetAttributeResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetAttributeResponse>> GetAttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes/{attr}");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));
        __urlBuilder.Replace("{attr}", Uri.EscapeDataString(attr));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetAttributeResponse>>("GET", __url, "application/json", default, default, cancellationToken);
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
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetAccessListsResponse> GetAccessLists(string? domain = default);

    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetAccessListsResponse>> GetAccessListsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="user">User identifier/name.</param>
    IReadOnlyDictionary<string, GetUserAccessResponse> GetUserAccess(string user, string? domain = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="user">User identifier/name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetUserAccessResponse>> GetUserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.</param>
    IReadOnlyDictionary<string, PutUserAccessResponse> PutUserAccess(string user, JsonElement body, string? domain = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PutUserAccessResponse>> PutUserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetGroupAccessListsResponse> GetGroupAccessLists(string id, string? domain = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetGroupAccessListsResponse>> GetGroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetGroupUserAccessResponse> GetGroupUserAccess(string id, string user, string? domain = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetGroupUserAccessResponse>> GetGroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetDatasetAccessListsResponse> GetDatasetAccessLists(string id, string? domain = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetDatasetAccessListsResponse>> GetDatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GetDataTypeAccessListsResponse> GetDataTypeAccessLists(string id, string? domain = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GetDataTypeAccessListsResponse>> GetDataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, GetAccessListsResponse> GetAccessLists(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetAccessListsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetAccessListsResponse>> GetAccessListsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetAccessListsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetUserAccessResponse> GetUserAccess(string user, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetUserAccessResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetUserAccessResponse>> GetUserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetUserAccessResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, PutUserAccessResponse> PutUserAccess(string user, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, PutUserAccessResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PutUserAccessResponse>> PutUserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PutUserAccessResponse>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetGroupAccessListsResponse> GetGroupAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetGroupAccessListsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetGroupAccessListsResponse>> GetGroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetGroupAccessListsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetGroupUserAccessResponse> GetGroupUserAccess(string id, string user, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls/{user}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetGroupUserAccessResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetGroupUserAccessResponse>> GetGroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls/{user}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetGroupUserAccessResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetDatasetAccessListsResponse> GetDatasetAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetDatasetAccessListsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetDatasetAccessListsResponse>> GetDatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetDatasetAccessListsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, GetDataTypeAccessListsResponse> GetDataTypeAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, GetDataTypeAccessListsResponse>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GetDataTypeAccessListsResponse>> GetDataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        if (domain is not null)
            __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GetDataTypeAccessListsResponse>>("GET", __url, "application/json", default, default, cancellationToken);
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


/// <summary>
/// Access Control List for a single user.
/// </summary>
/// <param name="Username"></param>
public record ACL(IReadOnlyDictionary<string, UsernameType> Username);

/// <summary>
/// Access Control Lists for users.
/// </summary>
/// <param name="ForWhom">Access Control List for a single user.</param>
public record ACLS(ACL ForWhom);

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
/// <param name="Href">URL of resource</param>
/// <param name="Rel">Relation to `href`.</param>
public record HrefsType(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Root">UUID of root Group. If Domain is of class 'folder', this entry is not present.</param>
/// <param name="Owner"></param>
/// <param name="Class">Category of Domain. If 'folder' no root group is included in response.</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Hrefs">Array of url references and their relation to this Domain. Should include entries for: `acls`, `database` (if not class is not `folder`), `groupbase` (if not class is not `folder`), `parent`, `root` (if not class is not `folder`), `self`, `typebase` (if not class is not `folder`).</param>
public record GetDomainResponse(string Root, string Owner, string Class, double Created, double LastModified, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

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
/// 
/// </summary>
/// <param name="Groups"></param>
/// <param name="Hrefs"></param>
public record GetGroupsResponse(IReadOnlyList<string> Groups, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// 
/// </summary>
public record TypeType();

/// <summary>
/// 
/// </summary>
public record ShapeType();

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
public record PostDatasetResponse(string Id, string Root, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, TypeType> Type, IReadOnlyDictionary<string, ShapeType> Shape);

/// <summary>
/// 
/// </summary>
/// <param name="Datasets"></param>
/// <param name="Hrefs">List of references to other objects.</param>
public record GetDatasetsResponse(IReadOnlyList<string> Datasets, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Id"></param>
public record PostDataTypeResponse(double AttributeCount, string Id);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record GetAccessListsResponse(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record GetUserAccessResponse(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record PutUserAccessResponse(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

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
public record GetGroupResponse(string Id, string Root, IReadOnlyList<string> Alias, double Created, double LastModified, string Domain, double AttributeCount, double LinkCount, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// 
/// </summary>
public record DeleteGroupResponse();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record AttributesType(double Created, string Href, string Name, IReadOnlyDictionary<string, ShapeType> Shape, IReadOnlyDictionary<string, TypeType> Type, string Value);

/// <summary>
/// TODO
/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record GetAttributesResponse(IReadOnlyList<IReadOnlyDictionary<string, AttributesType>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// TODO
/// </summary>
public record PutAttributeResponse();

/// <summary>
/// TODO
/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record GetAttributeResponse(double Created, double LastModified, string Name, IReadOnlyDictionary<string, ShapeType> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record GetGroupAccessListsResponse(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record GetGroupUserAccessResponse(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

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
public record LinksType(string Id, double Created, string Class, string Title, string Target, string Href, string Collection);

/// <summary>
/// 
/// </summary>
/// <param name="Links"></param>
/// <param name="Hrefs">List of references to other entities.</param>
public record GetLinksResponse(IReadOnlyList<IReadOnlyDictionary<string, LinksType>> Links, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

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
public record LinkType(string Id, string Title, string Collection, string Class);

/// <summary>
/// 
/// </summary>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="Link"></param>
/// <param name="Hrefs">List of references to other entities.</param>
public record GetLinkResponse(double LastModified, double Created, IReadOnlyDictionary<string, LinkType> Link, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record DeleteLinkResponse();

/// <summary>
/// 
/// </summary>
/// <param name="Name">Descriptive or identifying name. Must be unique in the fields list.</param>
/// <param name="Type">Enum of pre-defined type, UUID of committed type, or type definition. (TODO: see `POST Dataset`?)</param>
public record FieldsType(string Name, string Type);

/// <summary>
/// TODO
/// </summary>
public record LayoutType();

/// <summary>
/// Dataset creation properties as provided upon creation.
/// </summary>
public record CreationPropertiesType();

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of this Dataset.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="Domain"></param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="AttributeCount"></param>
/// <param name="Type">TODO</param>
/// <param name="Shape">TODO</param>
/// <param name="Layout">TODO</param>
/// <param name="CreationProperties">Dataset creation properties as provided upon creation.</param>
/// <param name="Hrefs">List of references to other objects.</param>
public record GetDatasetResponse(string Id, string Root, string Domain, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, TypeType> Type, IReadOnlyDictionary<string, ShapeType> Shape, IReadOnlyDictionary<string, LayoutType> Layout, IReadOnlyDictionary<string, CreationPropertiesType> CreationProperties, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

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
/// (See `GET /datasets/{id}`)
/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Shape"></param>
/// <param name="Hrefs">Must include references to only: `owner`, `root`, `self`.</param>
public record GetShapeResponse(double Created, double LastModified, IReadOnlyDictionary<string, ShapeType> Shape, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
/// <param name="Type"></param>
/// <param name="Hrefs"></param>
public record GetDataTypeResponse(IReadOnlyDictionary<string, TypeType> Type, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Value"></param>
public record PostValuesResponse(IReadOnlyList<JsonElement> Value);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record GetDatasetAccessListsResponse(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

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
public record GetDatatypeResponse(double AttributeCount, double Created, string Id, double LastModified, string Root, IReadOnlyDictionary<string, TypeType> Type, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// Always returns `{"hrefs": []}` (TODO confirm)
/// </summary>
/// <param name="Hrefs"></param>
public record DeleteDatatypeResponse(IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record GetDataTypeAccessListsResponse(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, HrefsType>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Create"></param>
/// <param name="Update"></param>
/// <param name="Delete"></param>
/// <param name="UpdateACL"></param>
/// <param name="Read"></param>
/// <param name="ReadACL"></param>
public record UsernameType(bool Create, bool Update, bool Delete, bool UpdateACL, bool Read, bool ReadACL);



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

