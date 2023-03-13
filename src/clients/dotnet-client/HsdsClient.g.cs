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
    IReadOnlyDictionary<string, MVNJVIVUZX> Domain(JsonElement body, string? domain = default, double? folder = default);

    /// <summary>
    /// Create a new Domain on the service.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="folder">If present and `1`, creates a Folder instead of a Domain.</param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, ZYTGNJLFPX>> DomainAsync(JsonElement body, string? domain = default, double? folder = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about the requested domain.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, OMQRULCSXY> Domain(string? domain = default);

    /// <summary>
    /// Get information about the requested domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, ZEWOMGHBAB>> DomainAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete the specified Domain or Folder.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, IQVDMTGFVV> Domain(string? domain = default);

    /// <summary>
    /// Delete the specified Domain or Folder.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, UCXCZLMOHN>> DomainAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body"></param>
    IReadOnlyDictionary<string, CQTYYWTBJL> Group(JsonElement body, string? domain = default);

    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PPTJMOQJXZ>> GroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, FLDTSXZGSA> Groups(string? domain = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, RQGGQPNJEF>> GroupsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Dataset's properties.
</param>
    IReadOnlyDictionary<string, HYYWAALCDX> Dataset(JsonElement body, string? domain = default);

    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Dataset's properties.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, LHFQHDBLPM>> DatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GBGNJPCZDL> Datasets(string? domain = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, RBCTIUHYOQ>> DatasetsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">Definition of Datatype to commit.
</param>
    IReadOnlyDictionary<string, UJJABVWYLI> DataType(JsonElement body, string? domain = default);

    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">Definition of Datatype to commit.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, IXEUUFBDKR>> DataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GXFUHNFSOA> AccessLists(string? domain = default);

    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, MAWGTREJXC>> AccessListsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="user">User identifier/name.</param>
    IReadOnlyDictionary<string, WLFHVSWNNB> UserAccess(string user, string? domain = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="user">User identifier/name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JBHWXJVUEU>> UserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.
</param>
    IReadOnlyDictionary<string, DOERPKDADO> UserAccess(string user, JsonElement body, string? domain = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, LPDWGUUGVG>> UserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, IRXXPOVZPR> Domain(JsonElement body, string? domain = default, double? folder = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["folder"] = Uri.EscapeDataString(Convert.ToString(folder, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, IRXXPOVZPR>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, YSMKJKZLOA>> DomainAsync(JsonElement body, string? domain = default, double? folder = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["folder"] = Uri.EscapeDataString(Convert.ToString(folder, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, YSMKJKZLOA>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, OYLJODVQGL> Domain(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, OYLJODVQGL>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, NLIRKHPVLG>> DomainAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, NLIRKHPVLG>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DRUOUZWBWQ> Domain(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, DRUOUZWBWQ>>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, VKZGMEMZUO>> DomainAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, VKZGMEMZUO>>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, FFHXPFXDNS> Group(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, FFHXPFXDNS>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, CBAIPCUXPQ>> GroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, CBAIPCUXPQ>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, YCDAWBWCUQ> Groups(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, YCDAWBWCUQ>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, DLCSVGGIOF>> GroupsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, DLCSVGGIOF>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, BWQVVCPJOW> Dataset(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, BWQVVCPJOW>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GXWNIKGQZN>> DatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GXWNIKGQZN>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, LKGEUGMHFE> Datasets(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, LKGEUGMHFE>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, EYHNSEGJTW>> DatasetsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, EYHNSEGJTW>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DVNWDAJYLU> DataType(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, DVNWDAJYLU>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, UJYKAWXDFX>> DataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, UJYKAWXDFX>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, RVTMMGFPVN> AccessLists(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, RVTMMGFPVN>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, OBIIPHKNCZ>> AccessListsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, OBIIPHKNCZ>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, WJBBKRYOLW> UserAccess(string user, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, WJBBKRYOLW>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, SNGSMUPVAM>> UserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, SNGSMUPVAM>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, EEOSWULATS> UserAccess(string user, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, EEOSWULATS>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PWFKFKSRGN>> UserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PWFKFKSRGN>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
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
    IReadOnlyDictionary<string, TTSLFRYKRD> Group(JsonElement body, string? domain = default);

    /// <summary>
    /// Create a new Group.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, YZPBWKVLWM>> GroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, LBOPERDNVU> Groups(string? domain = default);

    /// <summary>
    /// Get UUIDs for all non-root Groups in Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, KYHLTRYFDA>> GroupsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="getalias"></param>
    IReadOnlyDictionary<string, FNRGXCBZWG> Group(string id, string? domain = default, int? getalias = default);

    /// <summary>
    /// Get information about a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="getalias"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, LXLHJHLXTO>> GroupAsync(string id, string? domain = default, int? getalias = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, NCHTGGRAOH> Group(string id, string? domain = default);

    /// <summary>
    /// Delete a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, OSQTVEUTPE>> GroupAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.
Can be used with `Marker`.
</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.
</param>
    IReadOnlyDictionary<string, MMSICLCMTD> Attributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.
Can be used with `Marker`.
</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, XHIZYDAEWK>> AttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    IReadOnlyDictionary<string, JHDCRFWIWP> Attribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, UPLEDITJEV>> AttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    IReadOnlyDictionary<string, JCUBAUTCMO> Attribute(string collection, string obj_uuid, string attr, string? domain = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, VVCIHGUUMY>> AttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, HRPEOYTDYS> GroupAccessLists(string id, string? domain = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, SYDCDACZYU>> GroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, DBPNYJELES> GroupUserAccess(string id, string user, string? domain = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, LTQAZFCIHA>> GroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, VEPWPNQYKF> Group(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, VEPWPNQYKF>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, AQCJYTFMBT>> GroupAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, AQCJYTFMBT>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SOIAMDLACE> Groups(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, SOIAMDLACE>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, WIQNBMJKUE>> GroupsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, WIQNBMJKUE>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, OZSMUEFEVE> Group(string id, string? domain = default, int? getalias = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["getalias"] = Uri.EscapeDataString(Convert.ToString(getalias, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, OZSMUEFEVE>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, KMRRSGPELX>> GroupAsync(string id, string? domain = default, int? getalias = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["getalias"] = Uri.EscapeDataString(Convert.ToString(getalias, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, KMRRSGPELX>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ULWQNLJYPG> Group(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, ULWQNLJYPG>>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, YIPGGCMCSL>> GroupAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, YIPGGCMCSL>>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, RYEXTTJBNI> Attributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, RYEXTTJBNI>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PBFJKFCGXT>> AttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PBFJKFCGXT>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, WADKSJUBUK> Attribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, WADKSJUBUK>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, BNQXJWDYWG>> AttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, BNQXJWDYWG>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SELKKKVEIL> Attribute(string collection, string obj_uuid, string attr, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, SELKKKVEIL>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, RZBOKZREEI>> AttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, RZBOKZREEI>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, NFTPMVAHMS> GroupAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, NFTPMVAHMS>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, ROANSYAHJV>> GroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, ROANSYAHJV>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, LFMCQMPIJC> GroupUserAccess(string id, string user, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, LFMCQMPIJC>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, YZJXVGCPIY>> GroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, YZJXVGCPIY>>("GET", __url, "application/json", default, default, cancellationToken);
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
    /// <param name="Limit">Cap the number of Links returned in list.
Must be an integer `N >= 0`.
May be greater than or equal to the number of Links; has no effect in that case.
May be used in conjunction with query parameter `Marker`.
</param>
    /// <param name="Marker">Title of a Link; the first Link name to list.
If no Link exists with that title, causes an error.
May be used with query parameter `Limit`.
</param>
    IReadOnlyDictionary<string, CKYLCVFQGT> Links(string id, string? domain = default, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Links in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Links returned in list.
Must be an integer `N >= 0`.
May be greater than or equal to the number of Links; has no effect in that case.
May be used in conjunction with query parameter `Marker`.
</param>
    /// <param name="Marker">Title of a Link; the first Link name to list.
If no Link exists with that title, causes an error.
May be used with query parameter `Limit`.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, IFDYGPCQGO>> LinksAsync(string id, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new Link in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Link to create.
Requires at least one of `id` and `h5path`; if both supplied, `id` takes priority. `h5domain` applies only if `h5path` is present, providing the Domain for an external Link.
</param>
    IReadOnlyDictionary<string, DHKKNTBHBC> Link(string id, string linkname, JsonElement body, string? domain = default);

    /// <summary>
    /// Create a new Link in a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Link to create.
Requires at least one of `id` and `h5path`; if both supplied, `id` takes priority. `h5domain` applies only if `h5path` is present, providing the Domain for an external Link.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, FNRFFIAFQX>> LinkAsync(string id, string linkname, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get Link info.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, OAPWXHOJBH> Link(string id, string linkname, string? domain = default);

    /// <summary>
    /// Get Link info.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JIPAYJERQP>> LinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete Link.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, POITHILPNW> Link(string id, string linkname, string? domain = default);

    /// <summary>
    /// Delete Link.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="linkname"></param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, FPGIOTOZRJ>> LinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, ISDKRENGAW> Links(string id, string? domain = default, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, ISDKRENGAW>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, TCBITTDQFH>> LinksAsync(string id, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/links");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, TCBITTDQFH>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, BOKFHGHGGQ> Link(string id, string linkname, JsonElement body, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, BOKFHGHGGQ>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, VJDXATNOZI>> LinkAsync(string id, string linkname, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, VJDXATNOZI>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, FYPZYXUTTH> Link(string id, string linkname, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, FYPZYXUTTH>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PHXXGVKPGR>> LinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, PHXXGVKPGR>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, JDGKVTCFSJ> Link(string id, string linkname, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, JDGKVTCFSJ>>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, HDFJBYAHMV>> LinkAsync(string id, string linkname, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, HDFJBYAHMV>>("DELETE", __url, "application/json", default, default, cancellationToken);
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
    /// <param name="body">JSON object describing the Dataset's properties.
</param>
    IReadOnlyDictionary<string, TYXPHWRBLG> Dataset(JsonElement body, string? domain = default);

    /// <summary>
    /// Create a Dataset.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing the Dataset's properties.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GHOAFEHUJV>> DatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, VPVSXWWGKX> Datasets(string? domain = default);

    /// <summary>
    /// List Datasets.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, MFDQLQOLDY>> DatasetsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, ONNVDBSPIR> Dataset(string id, string? domain = default);

    /// <summary>
    /// Get information about a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JIRGXQWHYC>> DatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, JMDLEUCCSA> Dataset(string id, string? domain = default);

    /// <summary>
    /// Delete a Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, PYNXYQKRUU>> DatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Modify a Dataset's dimensions.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">Array of nonzero integers.
Length must equal Dataset's rank -- one value per dimension.
New extent must be greater than or equal to the current extent and less than or equal to maximum extent (from `maxdims`). For dimension index `i`, if `maxdims[i] == 0` then its maximum extent is unbounded.
</param>
    IReadOnlyDictionary<string, KIYKYZKUGX> Shape(string id, JsonElement body, string? domain = default);

    /// <summary>
    /// Modify a Dataset's dimensions.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">Array of nonzero integers.
Length must equal Dataset's rank -- one value per dimension.
New extent must be greater than or equal to the current extent and less than or equal to maximum extent (from `maxdims`). For dimension index `i`, if `maxdims[i] == 0` then its maximum extent is unbounded.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, ASTRLTCBUR>> ShapeAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset's shape.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, JRWPJTXPNL> Shape(string id, string? domain = default);

    /// <summary>
    /// Get information about a Dataset's shape.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, DPVWNOXHKJ>> ShapeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a Dataset's type.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, XEYXXSFETW> DataType(string id, string? domain = default);

    /// <summary>
    /// Get information about a Dataset's type.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, HVBBSHUWCW>> DataTypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Write values to Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing what to write.
At least one of `points` OR `start, stop, step` must be provided.
`value` must be provided if not using `points`, else _either_ `value` or `value_bas64` must be provided.
</param>
    void Values(string id, JsonElement body, string? domain = default);

    /// <summary>
    /// Write values to Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object describing what to write.
At least one of `points` OR `start, stop, step` must be provided.
`value` must be provided if not using `points`, else _either_ `value` or `value_bas64` must be provided.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task ValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get values from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="select">URL-encoded string representing a selection array.
_Example_: `[3:9,0:5:2]` gets values from two-dimensional dataset: [[3,0], [4,0], ..., [8,0], [3,2], [4,2], ..., [8,4]] (18 data points total: `6*3`)
In EBNF plaintext:
`SELECT` := `'[' SLICE { ',' SLICE } ']'`
`SLICE` := `START ':' STOP [ ':' STEP ]`
`START` := non-negative integer less than the dimension's extent.
`STOP` := non-negaive integer greater than `START` and less than or equal to the dimension's extent. Is the first index _not_ included in the selection hyperslab.
`STEP` := non-negative integer greater than zero; is the increment of index in dimension between each value. If omitted, defaults to `1` (contiguous indices).
</param>
    /// <param name="query">URL-encoded string of conditional expression to filter selection.
E.g., the condition `(temp > 32.0) & (dir == 'N')` would return elements of the dataset where the `temp` field was greater than `32.0` _and_ the `dir` field was equal to `N`. TODO: query syntax description
_Must_ be URL-encoded.
Can be used in conjunction with `select` parameter to filter a hyberslab selection. Can be used in conjunction with `Limit` parameter to restrict number of values returned.
Only applicable to one-dimensional compound datasets. TODO: verify
</param>
    /// <param name="Limit">Integer greater than zero.
If present, specifies maximum number of values to return.
Applies only to the `query` parameter.
</param>
    HttpResponseMessage Values(string id, string? domain = default, string? select = default, string? query = default, double? Limit = default);

    /// <summary>
    /// Get values from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="select">URL-encoded string representing a selection array.
_Example_: `[3:9,0:5:2]` gets values from two-dimensional dataset: [[3,0], [4,0], ..., [8,0], [3,2], [4,2], ..., [8,4]] (18 data points total: `6*3`)
In EBNF plaintext:
`SELECT` := `'[' SLICE { ',' SLICE } ']'`
`SLICE` := `START ':' STOP [ ':' STEP ]`
`START` := non-negative integer less than the dimension's extent.
`STOP` := non-negaive integer greater than `START` and less than or equal to the dimension's extent. Is the first index _not_ included in the selection hyperslab.
`STEP` := non-negative integer greater than zero; is the increment of index in dimension between each value. If omitted, defaults to `1` (contiguous indices).
</param>
    /// <param name="query">URL-encoded string of conditional expression to filter selection.
E.g., the condition `(temp > 32.0) & (dir == 'N')` would return elements of the dataset where the `temp` field was greater than `32.0` _and_ the `dir` field was equal to `N`. TODO: query syntax description
_Must_ be URL-encoded.
Can be used in conjunction with `select` parameter to filter a hyberslab selection. Can be used in conjunction with `Limit` parameter to restrict number of values returned.
Only applicable to one-dimensional compound datasets. TODO: verify
</param>
    /// <param name="Limit">Integer greater than zero.
If present, specifies maximum number of values to return.
Applies only to the `query` parameter.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<HttpResponseMessage> ValuesAsync(string id, string? domain = default, string? select = default, string? query = default, double? Limit = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get specific data points from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON array of coordinates in the Dataset.</param>
    IReadOnlyDictionary<string, VLIPJJSHOA> Values(string id, JsonElement body, string? domain = default);

    /// <summary>
    /// Get specific data points from Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON array of coordinates in the Dataset.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GJTKJCGRWP>> ValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.
Can be used with `Marker`.
</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.
</param>
    IReadOnlyDictionary<string, FIXXSUJQLK> Attributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.
Can be used with `Marker`.
</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, EARLRQMRVS>> AttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    IReadOnlyDictionary<string, QFGWKODFLY> Attribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, NZSJQBHBQY>> AttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    IReadOnlyDictionary<string, KHXYZNKMRE> Attribute(string collection, string obj_uuid, string attr, string? domain = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, MPIQECJBFO>> AttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, PXGPRGJYAE> DatasetAccessLists(string id, string? domain = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, TRSGTLUTVW>> DatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, DQGFQVPSPG> Dataset(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, DQGFQVPSPG>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, YIRTLMMHEQ>> DatasetAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, YIRTLMMHEQ>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, KBERUCCSWO> Datasets(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, KBERUCCSWO>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, QRWGIQALQL>> DatasetsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, QRWGIQALQL>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, RRKMVDIKZF> Dataset(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, RRKMVDIKZF>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, ZALSNSTICH>> DatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, ZALSNSTICH>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, UQLEOGFMBZ> Dataset(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, UQLEOGFMBZ>>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, SUTFXKZRAX>> DatasetAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, SUTFXKZRAX>>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, NGONRYYXXW> Shape(string id, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, NGONRYYXXW>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, UQRNZUEONZ>> ShapeAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, UQRNZUEONZ>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, NXPZRVSEFE> Shape(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, NXPZRVSEFE>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, HGZPNZKYHZ>> ShapeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/shape");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, HGZPNZKYHZ>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, EWXLIZVQGR> DataType(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/type");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, EWXLIZVQGR>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, WPRBXVZTXX>> DataTypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/type");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, WPRBXVZTXX>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public void Values(string id, JsonElement body, string? domain = default)
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
    public Task ValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
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
    public HttpResponseMessage Values(string id, string? domain = default, string? select = default, string? query = default, double? Limit = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["select"] = Uri.EscapeDataString(select);

        __queryValues["query"] = Uri.EscapeDataString(query);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<HttpResponseMessage>("GET", __url, "application/octet-stream", default, default);
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> ValuesAsync(string id, string? domain = default, string? select = default, string? query = default, double? Limit = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["select"] = Uri.EscapeDataString(select);

        __queryValues["query"] = Uri.EscapeDataString(query);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<HttpResponseMessage>("GET", __url, "application/octet-stream", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ZJWYTWMGQP> Values(string id, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, ZJWYTWMGQP>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, SRXIBTQWDJ>> ValuesAsync(string id, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/value");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, SRXIBTQWDJ>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, LTILCWOSGJ> Attributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, LTILCWOSGJ>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, XIHPYLQWZC>> AttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, XIHPYLQWZC>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, WAFUPCNZMM> Attribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, WAFUPCNZMM>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, RCHLDYUDUG>> AttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, RCHLDYUDUG>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, JECZBJUKNJ> Attribute(string collection, string obj_uuid, string attr, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, JECZBJUKNJ>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, AUWRZCZEGE>> AttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, AUWRZCZEGE>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, IJULVXOQVT> DatasetAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, IJULVXOQVT>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, QIXFWUSQOA>> DatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, QIXFWUSQOA>>("GET", __url, "application/json", default, default, cancellationToken);
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
    /// <param name="body">Definition of Datatype to commit.
</param>
    IReadOnlyDictionary<string, NRTZSSNVMF> DataType(JsonElement body, string? domain = default);

    /// <summary>
    /// Commit a Datatype to the Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="body">Definition of Datatype to commit.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, WEKIIUKKUG>> DataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about a committed Datatype
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="id">UUID of the committed datatype.</param>
    IReadOnlyDictionary<string, CYDKSTGFGK> Datatype(string id, string? domain = default);

    /// <summary>
    /// Get information about a committed Datatype
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, TOMKNIIOBJ>> DatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a committed Datatype.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="id">UUID of the committed datatype.</param>
    IReadOnlyDictionary<string, IYLQIHHPJY> Datatype(string id, string? domain = default);

    /// <summary>
    /// Delete a committed Datatype.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, ZDKLMTILZM>> DatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.
Can be used with `Marker`.
</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.
</param>
    IReadOnlyDictionary<string, GBHFAYBETJ> Attributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.
Can be used with `Marker`.
</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, GJKQWCFYGA>> AttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    IReadOnlyDictionary<string, MTAKSDLEEV> Attribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, ZGNUKJZUZL>> AttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    IReadOnlyDictionary<string, KDFECTMLWK> Attribute(string collection, string obj_uuid, string attr, string? domain = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, WCORIBEZFJ>> AttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, WEXAGCHLWF> DataTypeAccessLists(string id, string? domain = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, WZYHJICUEG>> DataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, WBJQBCKPRR> DataType(JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, WBJQBCKPRR>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, SBJPVBCFLB>> DataTypeAsync(JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, SBJPVBCFLB>>("POST", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, YZQXPPIAXX> Datatype(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, YZQXPPIAXX>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GCZEEOJBYA>> DatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GCZEEOJBYA>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, EFNRUOJGAH> Datatype(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, EFNRUOJGAH>>("DELETE", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, CEITCOMKRU>> DatatypeAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, CEITCOMKRU>>("DELETE", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, FEMNFLFDWC> Attributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, FEMNFLFDWC>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, DEYMAAKNHW>> AttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, DEYMAAKNHW>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SHNIOEQJSB> Attribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, SHNIOEQJSB>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, KWOKZFDMJJ>> AttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, KWOKZFDMJJ>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, STAUFORIGU> Attribute(string collection, string obj_uuid, string attr, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, STAUFORIGU>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, NVUBFFCSHV>> AttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, NVUBFFCSHV>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, VGHSDXTAHC> DataTypeAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, VGHSDXTAHC>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, YMWXYBEGPS>> DataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, YMWXYBEGPS>>("GET", __url, "application/json", default, default, cancellationToken);
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
    /// <param name="Limit">Cap the number of Attributes listed.
Can be used with `Marker`.
</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.
</param>
    IReadOnlyDictionary<string, LTBYROJYDV> Attributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default);

    /// <summary>
    /// List all Attributes attached to the HDF5 object `obj_uuid`.
    /// </summary>
    /// <param name="collection">The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="domain"></param>
    /// <param name="Limit">Cap the number of Attributes listed.
Can be used with `Marker`.
</param>
    /// <param name="Marker">Start Attribute listing _after_ the given name.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, QWJNUVMAAK>> AttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    IReadOnlyDictionary<string, VINLPCNGPT> Attribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default);

    /// <summary>
    /// Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).</param>
    /// <param name="obj_uuid">HDF5 object's UUID.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="body">Information to create a new attribute of the HDF5 object `obj_uuid`.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, IYNHDFEMDP>> AttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    IReadOnlyDictionary<string, DETZJIGXAI> Attribute(string collection, string obj_uuid, string attr, string? domain = default);

    /// <summary>
    /// Get information about an Attribute.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="collection">Collection of object (Group, Dataset, or Datatype).</param>
    /// <param name="obj_uuid">UUID of object.</param>
    /// <param name="attr">Name of attribute.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, VSPLEVVKTG>> AttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, HPKRKTFWIW> Attributes(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, HPKRKTFWIW>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, KYUHABLTNV>> AttributesAsync(string collection, string obj_uuid, string? domain = default, double? Limit = default, string? Marker = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/{collection}/{obj_uuid}/attributes");
        __urlBuilder.Replace("{collection}", Uri.EscapeDataString(collection));
        __urlBuilder.Replace("{obj_uuid}", Uri.EscapeDataString(obj_uuid));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        __queryValues["Limit"] = Uri.EscapeDataString(Convert.ToString(Limit, CultureInfo.InvariantCulture)!);

        __queryValues["Marker"] = Uri.EscapeDataString(Marker);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, KYUHABLTNV>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, CUQFSBIQTK> Attribute(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, CUQFSBIQTK>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, LHNIFAAPVB>> AttributeAsync(string collection, string obj_uuid, string attr, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, LHNIFAAPVB>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, DGBUMXPURX> Attribute(string collection, string obj_uuid, string attr, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, DGBUMXPURX>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, UYLTQADIKR>> AttributeAsync(string collection, string obj_uuid, string attr, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, UYLTQADIKR>>("GET", __url, "application/json", default, default, cancellationToken);
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
    IReadOnlyDictionary<string, LYIPSNVOCE> AccessLists(string? domain = default);

    /// <summary>
    /// Get access lists on Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, KPSVLUCATA>> AccessListsAsync(string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="user">User identifier/name.</param>
    IReadOnlyDictionary<string, XLRTNQRIOE> UserAccess(string user, string? domain = default);

    /// <summary>
    /// Get users's access to a Domain.
    /// </summary>
    /// <param name="domain"></param>
    /// <param name="user">User identifier/name.</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, JALTAMQLUB>> UserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.
</param>
    IReadOnlyDictionary<string, PVPLFFKULC> UserAccess(string user, JsonElement body, string? domain = default);

    /// <summary>
    /// Set user's access to the Domain.
    /// </summary>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="body">JSON object with one or more keys from the set: 'create', 'read', 'update', 'delete', 'readACL', 'updateACL'.  Each key should have a boolean value.  Based on keys provided, the user's ACL will be  updated for those keys.  If no ACL exist for the given user, it will be created.
</param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, ZTQKKJSDRN>> UserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, GERACQMIGQ> GroupAccessLists(string id, string? domain = default);

    /// <summary>
    /// List access lists on Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, ALWLMBACNS>> GroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, AXUQNWUGQX> GroupUserAccess(string id, string user, string? domain = default);

    /// <summary>
    /// Get users's access to a Group.
    /// </summary>
    /// <param name="id">UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.</param>
    /// <param name="user">Identifier/name of a user.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, AOGQHSUSOQ>> GroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, MPOQEBVTVR> DatasetAccessLists(string id, string? domain = default);

    /// <summary>
    /// Get access lists on Dataset.
    /// </summary>
    /// <param name="id">UUID of the Dataset.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, IDEGENHEXB>> DatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain"></param>
    IReadOnlyDictionary<string, XGRXJKWOGE> DataTypeAccessLists(string id, string? domain = default);

    /// <summary>
    /// List access lists on Datatype.
    /// </summary>
    /// <param name="id">UUID of the committed datatype.</param>
    /// <param name="domain"></param>
    /// <param name="cancellationToken">The token to cancel the current operation.</param>
    Task<IReadOnlyDictionary<string, SLEMVOBDRY>> DataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default);

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
    public IReadOnlyDictionary<string, DYMHOBSUQK> AccessLists(string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, DYMHOBSUQK>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, ZDPAHSXPOF>> AccessListsAsync(string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls");

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, ZDPAHSXPOF>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, OQSWHLNSJK> UserAccess(string user, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, OQSWHLNSJK>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, QVMIWOXAGK>> UserAccessAsync(string user, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, QVMIWOXAGK>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, SMZOPXPEOP> UserAccess(string user, JsonElement body, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, SMZOPXPEOP>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions));
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, GDWZEEBXQX>> UserAccessAsync(string user, JsonElement body, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/acls/{user}");
        __urlBuilder.Replace("{user}", Uri.EscapeDataString(user));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, GDWZEEBXQX>>("PUT", __url, "application/json", "application/json", JsonContent.Create(body, options: Utilities.JsonOptions), cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, QZHWBXFNVB> GroupAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, QZHWBXFNVB>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, QREXGSXIVO>> GroupAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/groups/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, QREXGSXIVO>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, AXDEBEGGKF> GroupUserAccess(string id, string user, string? domain = default)
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
        return ___client.Invoke<IReadOnlyDictionary<string, AXDEBEGGKF>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, VYDICRSQHU>> GroupUserAccessAsync(string id, string user, string? domain = default, CancellationToken cancellationToken = default)
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
        return ___client.InvokeAsync<IReadOnlyDictionary<string, VYDICRSQHU>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, RWTIYILCEN> DatasetAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, RWTIYILCEN>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, MNSNOKDWNJ>> DatasetAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datasets/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, MNSNOKDWNJ>>("GET", __url, "application/json", default, default, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, BMPJRRKKYP> DataTypeAccessLists(string id, string? domain = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.Invoke<IReadOnlyDictionary<string, BMPJRRKKYP>>("GET", __url, "application/json", default, default);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, RRNWQQUYGW>> DataTypeAccessListsAsync(string id, string? domain = default, CancellationToken cancellationToken = default)
    {
        var __urlBuilder = new StringBuilder();
        __urlBuilder.Append("/datatypes/{id}/acls");
        __urlBuilder.Replace("{id}", Uri.EscapeDataString(id));

        var __queryValues = new Dictionary<string, string>();

        __queryValues["domain"] = Uri.EscapeDataString(domain);

        var __query = "?" + string.Join('&', __queryValues.Select(entry => $"{entry.Key}={entry.Value}"));
        __urlBuilder.Append(__query);

        var __url = __urlBuilder.ToString();
        return ___client.InvokeAsync<IReadOnlyDictionary<string, RRNWQQUYGW>>("GET", __url, "application/json", default, default, cancellationToken);
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
public record ACL(IReadOnlyDictionary<string, TXSRBYCSLW> Username);

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
public record MVNJVIVUZX(ACLS Acls, double Created, double LastModified, string Owner, string Root);

/// <summary>
/// 
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Created">When domain was created.</param>
/// <param name="LastModified">When object was last modified.</param>
/// <param name="Owner">Name of owner.</param>
/// <param name="Root">ID of root group.</param>
public record ZYTGNJLFPX(ACLS Acls, double Created, double LastModified, string Owner, string Root);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this Domain.</param>
public record JURJWPKSLI(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Root">UUID of root Group. If Domain is of class 'folder', this entry is not present.
</param>
/// <param name="Owner"></param>
/// <param name="Class">Category of Domain. If 'folder' no root group is included in response.
</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Hrefs">Array of url references and their relation to this Domain. Should include entries for: `acls`, `database` (if not class is not `folder`), `groupbase` (if not class is not `folder`), `parent`, `root` (if not class is not `folder`), `self`, `typebase` (if not class is not `folder`).
</param>
public record OMQRULCSXY(string Root, string Owner, string Class, double Created, double LastModified, IReadOnlyList<IReadOnlyDictionary<string, JURJWPKSLI>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this Domain.</param>
public record PMSSGXOKKI(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Root">UUID of root Group. If Domain is of class 'folder', this entry is not present.
</param>
/// <param name="Owner"></param>
/// <param name="Class">Category of Domain. If 'folder' no root group is included in response.
</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Hrefs">Array of url references and their relation to this Domain. Should include entries for: `acls`, `database` (if not class is not `folder`), `groupbase` (if not class is not `folder`), `parent`, `root` (if not class is not `folder`), `self`, `typebase` (if not class is not `folder`).
</param>
public record ZEWOMGHBAB(string Root, string Owner, string Class, double Created, double LastModified, IReadOnlyList<IReadOnlyDictionary<string, PMSSGXOKKI>> Hrefs);

/// <summary>
/// The Domain or Folder which was deleted.
/// </summary>
/// <param name="Domain">domain path</param>
public record IQVDMTGFVV(string Domain);

/// <summary>
/// The Domain or Folder which was deleted.
/// </summary>
/// <param name="Domain">domain path</param>
public record UCXCZLMOHN(string Domain);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of new Group.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
public record CQTYYWTBJL(string Id, string Root, double LastModified, double Created, double AttributeCount, double LinkCount);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of new Group.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
public record PPTJMOQJXZ(string Id, string Root, double LastModified, double Created, double AttributeCount, double LinkCount);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record AZGEMZQUVP(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Groups"></param>
/// <param name="Hrefs"></param>
public record FLDTSXZGSA(IReadOnlyList<string> Groups, IReadOnlyList<IReadOnlyDictionary<string, AZGEMZQUVP>> Hrefs);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record NECDOGICOZ(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Groups"></param>
/// <param name="Hrefs"></param>
public record RQGGQPNJEF(IReadOnlyList<string> Groups, IReadOnlyList<IReadOnlyDictionary<string, NECDOGICOZ>> Hrefs);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record FYYCBATEER();

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record LRXWWZAGJH();

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
public record HYYWAALCDX(string Id, string Root, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, FYYCBATEER> Type, IReadOnlyDictionary<string, LRXWWZAGJH> Shape);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record ATEYNLNLLT();

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record EIJRXFEJME();

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
public record LHFQHDBLPM(string Id, string Root, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, ATEYNLNLLT> Type, IReadOnlyDictionary<string, EIJRXFEJME> Shape);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record MGWWUBADSP(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Datasets"></param>
/// <param name="Hrefs">List of references to other objects.
Should contain references for: `attributes`, `data`, `home`, `root`, `self`
</param>
public record GBGNJPCZDL(IReadOnlyList<string> Datasets, IReadOnlyList<IReadOnlyDictionary<string, MGWWUBADSP>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record HJVLKFLLYC(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Datasets"></param>
/// <param name="Hrefs">List of references to other objects.
Should contain references for: `attributes`, `data`, `home`, `root`, `self`
</param>
public record RBCTIUHYOQ(IReadOnlyList<string> Datasets, IReadOnlyList<IReadOnlyDictionary<string, HJVLKFLLYC>> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Id"></param>
public record UJJABVWYLI(double AttributeCount, string Id);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Id"></param>
public record IXEUUFBDKR(double AttributeCount, string Id);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record XPYLBAOBUM(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record GXFUHNFSOA(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, XPYLBAOBUM>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record BYETWGQOWW(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record MAWGTREJXC(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, BYETWGQOWW>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record XGYHZSHVEV(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record WLFHVSWNNB(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, XGYHZSHVEV>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record CNQNIIMCNQ(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record JBHWXJVUEU(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, CNQNIIMCNQ>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record TLQMDOIVCS(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record DOERPKDADO(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, TLQMDOIVCS>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record VJVOZEYNZB(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record LPDWGUUGVG(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, VJVOZEYNZB>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Created">When domain was created.</param>
/// <param name="LastModified">When object was last modified.</param>
/// <param name="Owner">Name of owner.</param>
/// <param name="Root">ID of root group.</param>
public record IRXXPOVZPR(ACLS Acls, double Created, double LastModified, string Owner, string Root);

/// <summary>
/// 
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Created">When domain was created.</param>
/// <param name="LastModified">When object was last modified.</param>
/// <param name="Owner">Name of owner.</param>
/// <param name="Root">ID of root group.</param>
public record YSMKJKZLOA(ACLS Acls, double Created, double LastModified, string Owner, string Root);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this Domain.</param>
public record MMEZTKLHNV(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Root">UUID of root Group. If Domain is of class 'folder', this entry is not present.
</param>
/// <param name="Owner"></param>
/// <param name="Class">Category of Domain. If 'folder' no root group is included in response.
</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Hrefs">Array of url references and their relation to this Domain. Should include entries for: `acls`, `database` (if not class is not `folder`), `groupbase` (if not class is not `folder`), `parent`, `root` (if not class is not `folder`), `self`, `typebase` (if not class is not `folder`).
</param>
public record OYLJODVQGL(string Root, string Owner, string Class, double Created, double LastModified, IReadOnlyList<IReadOnlyDictionary<string, MMEZTKLHNV>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this Domain.</param>
public record EXOMURSJXY(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Root">UUID of root Group. If Domain is of class 'folder', this entry is not present.
</param>
/// <param name="Owner"></param>
/// <param name="Class">Category of Domain. If 'folder' no root group is included in response.
</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Hrefs">Array of url references and their relation to this Domain. Should include entries for: `acls`, `database` (if not class is not `folder`), `groupbase` (if not class is not `folder`), `parent`, `root` (if not class is not `folder`), `self`, `typebase` (if not class is not `folder`).
</param>
public record NLIRKHPVLG(string Root, string Owner, string Class, double Created, double LastModified, IReadOnlyList<IReadOnlyDictionary<string, EXOMURSJXY>> Hrefs);

/// <summary>
/// The Domain or Folder which was deleted.
/// </summary>
/// <param name="Domain">domain path</param>
public record DRUOUZWBWQ(string Domain);

/// <summary>
/// The Domain or Folder which was deleted.
/// </summary>
/// <param name="Domain">domain path</param>
public record VKZGMEMZUO(string Domain);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of new Group.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
public record FFHXPFXDNS(string Id, string Root, double LastModified, double Created, double AttributeCount, double LinkCount);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of new Group.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
public record CBAIPCUXPQ(string Id, string Root, double LastModified, double Created, double AttributeCount, double LinkCount);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record SUOBNWOJOZ(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Groups"></param>
/// <param name="Hrefs"></param>
public record YCDAWBWCUQ(IReadOnlyList<string> Groups, IReadOnlyList<IReadOnlyDictionary<string, SUOBNWOJOZ>> Hrefs);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record DUHXURGXCZ(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Groups"></param>
/// <param name="Hrefs"></param>
public record DLCSVGGIOF(IReadOnlyList<string> Groups, IReadOnlyList<IReadOnlyDictionary<string, DUHXURGXCZ>> Hrefs);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record WHNEERZFGR();

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record JPPBGMCYFN();

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
public record BWQVVCPJOW(string Id, string Root, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, WHNEERZFGR> Type, IReadOnlyDictionary<string, JPPBGMCYFN> Shape);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record HGVSVSQTOW();

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record GRITUIUHRC();

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
public record GXWNIKGQZN(string Id, string Root, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, HGVSVSQTOW> Type, IReadOnlyDictionary<string, GRITUIUHRC> Shape);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record HLVZSGIBAQ(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Datasets"></param>
/// <param name="Hrefs">List of references to other objects.
Should contain references for: `attributes`, `data`, `home`, `root`, `self`
</param>
public record LKGEUGMHFE(IReadOnlyList<string> Datasets, IReadOnlyList<IReadOnlyDictionary<string, HLVZSGIBAQ>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record VFEQNJXJJH(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Datasets"></param>
/// <param name="Hrefs">List of references to other objects.
Should contain references for: `attributes`, `data`, `home`, `root`, `self`
</param>
public record EYHNSEGJTW(IReadOnlyList<string> Datasets, IReadOnlyList<IReadOnlyDictionary<string, VFEQNJXJJH>> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Id"></param>
public record DVNWDAJYLU(double AttributeCount, string Id);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Id"></param>
public record UJYKAWXDFX(double AttributeCount, string Id);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record KJUXEEPLAD(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record RVTMMGFPVN(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, KJUXEEPLAD>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record IJIAVGZOJS(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record OBIIPHKNCZ(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, IJIAVGZOJS>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record RSNKICYZZJ(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record WJBBKRYOLW(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, RSNKICYZZJ>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record RHAGHJJLHV(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record SNGSMUPVAM(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, RHAGHJJLHV>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record HAIKNTFZPN(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record EEOSWULATS(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, HAIKNTFZPN>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record MIUEZFZVTL(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record PWFKFKSRGN(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, MIUEZFZVTL>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of new Group.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
public record TTSLFRYKRD(string Id, string Root, double LastModified, double Created, double AttributeCount, double LinkCount);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of new Group.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
public record YZPBWKVLWM(string Id, string Root, double LastModified, double Created, double AttributeCount, double LinkCount);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record NQVFPDIBJY(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Groups"></param>
/// <param name="Hrefs"></param>
public record LBOPERDNVU(IReadOnlyList<string> Groups, IReadOnlyList<IReadOnlyDictionary<string, NQVFPDIBJY>> Hrefs);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record MBYIXADHCZ(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Groups"></param>
/// <param name="Hrefs"></param>
public record KYHLTRYFDA(IReadOnlyList<string> Groups, IReadOnlyList<IReadOnlyDictionary<string, MBYIXADHCZ>> Hrefs);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Rel">Relation to this object.</param>
/// <param name="Href">URL to reference.</param>
public record GDFHWOJOXF(string Rel, string Href);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of this Group.</param>
/// <param name="Root">UUID of root Group.</param>
/// <param name="Alias">List of aliases for the Group, as reached by _hard_ Links. If Group is unlinked, its alias list will be empty (`[]`).
Only present if `alias=1` is present as query parameter.
</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Domain"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
/// <param name="Hrefs">List of references to other objects.</param>
public record FNRGXCBZWG(string Id, string Root, IReadOnlyList<string> Alias, double Created, double LastModified, string Domain, double AttributeCount, double LinkCount, IReadOnlyList<IReadOnlyDictionary<string, GDFHWOJOXF>> Hrefs);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Rel">Relation to this object.</param>
/// <param name="Href">URL to reference.</param>
public record RPGBCGLXLJ(string Rel, string Href);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of this Group.</param>
/// <param name="Root">UUID of root Group.</param>
/// <param name="Alias">List of aliases for the Group, as reached by _hard_ Links. If Group is unlinked, its alias list will be empty (`[]`).
Only present if `alias=1` is present as query parameter.
</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Domain"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
/// <param name="Hrefs">List of references to other objects.</param>
public record LXLHJHLXTO(string Id, string Root, IReadOnlyList<string> Alias, double Created, double LastModified, string Domain, double AttributeCount, double LinkCount, IReadOnlyList<IReadOnlyDictionary<string, RPGBCGLXLJ>> Hrefs);

/// <summary>
/// 
/// </summary>
public record NCHTGGRAOH();

/// <summary>
/// 
/// </summary>
public record OSQTVEUTPE();

/// <summary>
/// 
/// </summary>
public record IGEANRKGPZ();

/// <summary>
/// 
/// </summary>
public record VJFWUKGEHI();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record VNYNMGWSSF(double Created, string Href, string Name, IReadOnlyDictionary<string, IGEANRKGPZ> Shape, IReadOnlyDictionary<string, VJFWUKGEHI> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record RCIIKGVGVB(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record MMSICLCMTD(IReadOnlyList<IReadOnlyDictionary<string, VNYNMGWSSF>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, RCIIKGVGVB>> Hrefs);

/// <summary>
/// 
/// </summary>
public record VGXELYNGWX();

/// <summary>
/// 
/// </summary>
public record NRZZJHQOFL();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record NUEUOQSIVK(double Created, string Href, string Name, IReadOnlyDictionary<string, VGXELYNGWX> Shape, IReadOnlyDictionary<string, NRZZJHQOFL> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record TXDKICUUUJ(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record XHIZYDAEWK(IReadOnlyList<IReadOnlyDictionary<string, NUEUOQSIVK>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, TXDKICUUUJ>> Hrefs);

/// <summary>
/// TODO
/// </summary>
public record JHDCRFWIWP();

/// <summary>
/// TODO
/// </summary>
public record UPLEDITJEV();

/// <summary>
/// 
/// </summary>
public record YVXWGYBHPQ();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record FVUHMDICXL(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record JCUBAUTCMO(double Created, double LastModified, string Name, IReadOnlyDictionary<string, YVXWGYBHPQ> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, FVUHMDICXL>> Hrefs);

/// <summary>
/// 
/// </summary>
public record PAKCYPUMAP();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record NFJSOLUNTI(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record VVCIHGUUMY(double Created, double LastModified, string Name, IReadOnlyDictionary<string, PAKCYPUMAP> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, NFJSOLUNTI>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record SAJAMMOWJV(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record HRPEOYTDYS(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, SAJAMMOWJV>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record IGDXSJKRPM(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record SYDCDACZYU(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, IGDXSJKRPM>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GYZGZWZCSI(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record DBPNYJELES(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, GYZGZWZCSI>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record XFQMNZCAOB(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record LTQAZFCIHA(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, XFQMNZCAOB>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of new Group.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
public record VEPWPNQYKF(string Id, string Root, double LastModified, double Created, double AttributeCount, double LinkCount);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of new Group.</param>
/// <param name="Root">UUID of root Group in Domain.</param>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
public record AQCJYTFMBT(string Id, string Root, double LastModified, double Created, double AttributeCount, double LinkCount);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record HOUXKSVIRR(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Groups"></param>
/// <param name="Hrefs"></param>
public record SOIAMDLACE(IReadOnlyList<string> Groups, IReadOnlyList<IReadOnlyDictionary<string, HOUXKSVIRR>> Hrefs);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record RSZVSBCTIJ(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Groups"></param>
/// <param name="Hrefs"></param>
public record WIQNBMJKUE(IReadOnlyList<string> Groups, IReadOnlyList<IReadOnlyDictionary<string, RSZVSBCTIJ>> Hrefs);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Rel">Relation to this object.</param>
/// <param name="Href">URL to reference.</param>
public record BJNEYKLUUL(string Rel, string Href);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of this Group.</param>
/// <param name="Root">UUID of root Group.</param>
/// <param name="Alias">List of aliases for the Group, as reached by _hard_ Links. If Group is unlinked, its alias list will be empty (`[]`).
Only present if `alias=1` is present as query parameter.
</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Domain"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
/// <param name="Hrefs">List of references to other objects.</param>
public record OZSMUEFEVE(string Id, string Root, IReadOnlyList<string> Alias, double Created, double LastModified, string Domain, double AttributeCount, double LinkCount, IReadOnlyList<IReadOnlyDictionary<string, BJNEYKLUUL>> Hrefs);

/// <summary>
/// References to other objects.
Must contain references for only: `attributes`, `home`, `links`, `root`, `self`.

/// </summary>
/// <param name="Rel">Relation to this object.</param>
/// <param name="Href">URL to reference.</param>
public record XVVWDVOADM(string Rel, string Href);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of this Group.</param>
/// <param name="Root">UUID of root Group.</param>
/// <param name="Alias">List of aliases for the Group, as reached by _hard_ Links. If Group is unlinked, its alias list will be empty (`[]`).
Only present if `alias=1` is present as query parameter.
</param>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Domain"></param>
/// <param name="AttributeCount"></param>
/// <param name="LinkCount"></param>
/// <param name="Hrefs">List of references to other objects.</param>
public record KMRRSGPELX(string Id, string Root, IReadOnlyList<string> Alias, double Created, double LastModified, string Domain, double AttributeCount, double LinkCount, IReadOnlyList<IReadOnlyDictionary<string, XVVWDVOADM>> Hrefs);

/// <summary>
/// 
/// </summary>
public record ULWQNLJYPG();

/// <summary>
/// 
/// </summary>
public record YIPGGCMCSL();

/// <summary>
/// 
/// </summary>
public record NNRSCFIGYL();

/// <summary>
/// 
/// </summary>
public record JWYDUYCUPF();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record BSNGBYWIPA(double Created, string Href, string Name, IReadOnlyDictionary<string, NNRSCFIGYL> Shape, IReadOnlyDictionary<string, JWYDUYCUPF> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record XNVUVHUAFF(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record RYEXTTJBNI(IReadOnlyList<IReadOnlyDictionary<string, BSNGBYWIPA>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, XNVUVHUAFF>> Hrefs);

/// <summary>
/// 
/// </summary>
public record NGVOTRTDBS();

/// <summary>
/// 
/// </summary>
public record IWBERCCNZN();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record JENOOGVYYX(double Created, string Href, string Name, IReadOnlyDictionary<string, NGVOTRTDBS> Shape, IReadOnlyDictionary<string, IWBERCCNZN> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record IQDUHAQINK(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record PBFJKFCGXT(IReadOnlyList<IReadOnlyDictionary<string, JENOOGVYYX>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, IQDUHAQINK>> Hrefs);

/// <summary>
/// TODO
/// </summary>
public record WADKSJUBUK();

/// <summary>
/// TODO
/// </summary>
public record BNQXJWDYWG();

/// <summary>
/// 
/// </summary>
public record SNUHGPAAUW();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record BGXBMDOKPC(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record SELKKKVEIL(double Created, double LastModified, string Name, IReadOnlyDictionary<string, SNUHGPAAUW> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, BGXBMDOKPC>> Hrefs);

/// <summary>
/// 
/// </summary>
public record HWFTNIZTRN();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record QHPUEUWMLH(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record RZBOKZREEI(double Created, double LastModified, string Name, IReadOnlyDictionary<string, HWFTNIZTRN> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, QHPUEUWMLH>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record EUSFPCERVV(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record NFTPMVAHMS(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, EUSFPCERVV>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GPZDLVPHZY(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record ROANSYAHJV(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, GPZDLVPHZY>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record HWVYOQVXBN(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record LFMCQMPIJC(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, HWVYOQVXBN>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record VQZIJXTMYW(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record YZJXVGCPIY(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, VQZIJXTMYW>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of Link target.</param>
/// <param name="Created"></param>
/// <param name="Class">Indicate whether this Link is hard, soft, or external.
</param>
/// <param name="Title">Name/label/title of the Link, as provided upon creation.
</param>
/// <param name="Target">URL of Link target.</param>
/// <param name="Href">URL to origin of Link.</param>
/// <param name="Collection">What kind of object is the target. (TODO)
</param>
public record MJONJSSCFA(string Id, double Created, string Class, string Title, string Target, string Href, string Collection);

/// <summary>
/// 
/// </summary>
/// <param name="Rel">Relation to this object.</param>
/// <param name="Href">URL to reference.</param>
public record GARQVZXMCY(string Rel, string Href);

/// <summary>
/// 
/// </summary>
/// <param name="Links"></param>
/// <param name="Hrefs">List of references to other entities.
Should contain references for: `home`, `owner`, `self`.
</param>
public record CKYLCVFQGT(IReadOnlyList<IReadOnlyDictionary<string, MJONJSSCFA>> Links, IReadOnlyList<IReadOnlyDictionary<string, GARQVZXMCY>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of Link target.</param>
/// <param name="Created"></param>
/// <param name="Class">Indicate whether this Link is hard, soft, or external.
</param>
/// <param name="Title">Name/label/title of the Link, as provided upon creation.
</param>
/// <param name="Target">URL of Link target.</param>
/// <param name="Href">URL to origin of Link.</param>
/// <param name="Collection">What kind of object is the target. (TODO)
</param>
public record RBUKFQUWSS(string Id, double Created, string Class, string Title, string Target, string Href, string Collection);

/// <summary>
/// 
/// </summary>
/// <param name="Rel">Relation to this object.</param>
/// <param name="Href">URL to reference.</param>
public record BRGYPDQDQA(string Rel, string Href);

/// <summary>
/// 
/// </summary>
/// <param name="Links"></param>
/// <param name="Hrefs">List of references to other entities.
Should contain references for: `home`, `owner`, `self`.
</param>
public record IFDYGPCQGO(IReadOnlyList<IReadOnlyDictionary<string, RBUKFQUWSS>> Links, IReadOnlyList<IReadOnlyDictionary<string, BRGYPDQDQA>> Hrefs);

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record DHKKNTBHBC();

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record FNRFFIAFQX();

/// <summary>
/// 
/// </summary>
/// <param name="Id"></param>
/// <param name="Title"></param>
/// <param name="Collection"></param>
/// <param name="Class"></param>
public record WCERGMMRVI(string Id, string Title, string Collection, string Class);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record GAPDYVQTZH(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="Link"></param>
/// <param name="Hrefs">List of references to other entities.
Should contain references for: `home`, `owner`, `self`, `target`,
</param>
public record OAPWXHOJBH(double LastModified, double Created, IReadOnlyDictionary<string, WCERGMMRVI> Link, IReadOnlyList<IReadOnlyDictionary<string, GAPDYVQTZH>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Id"></param>
/// <param name="Title"></param>
/// <param name="Collection"></param>
/// <param name="Class"></param>
public record LWGPBECFTY(string Id, string Title, string Collection, string Class);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record GFIYTHHFCU(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="Link"></param>
/// <param name="Hrefs">List of references to other entities.
Should contain references for: `home`, `owner`, `self`, `target`,
</param>
public record JIPAYJERQP(double LastModified, double Created, IReadOnlyDictionary<string, LWGPBECFTY> Link, IReadOnlyList<IReadOnlyDictionary<string, GFIYTHHFCU>> Hrefs);

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record POITHILPNW();

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record FPGIOTOZRJ();

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of Link target.</param>
/// <param name="Created"></param>
/// <param name="Class">Indicate whether this Link is hard, soft, or external.
</param>
/// <param name="Title">Name/label/title of the Link, as provided upon creation.
</param>
/// <param name="Target">URL of Link target.</param>
/// <param name="Href">URL to origin of Link.</param>
/// <param name="Collection">What kind of object is the target. (TODO)
</param>
public record OKRDGJFEQJ(string Id, double Created, string Class, string Title, string Target, string Href, string Collection);

/// <summary>
/// 
/// </summary>
/// <param name="Rel">Relation to this object.</param>
/// <param name="Href">URL to reference.</param>
public record AUPZFJBQBV(string Rel, string Href);

/// <summary>
/// 
/// </summary>
/// <param name="Links"></param>
/// <param name="Hrefs">List of references to other entities.
Should contain references for: `home`, `owner`, `self`.
</param>
public record ISDKRENGAW(IReadOnlyList<IReadOnlyDictionary<string, OKRDGJFEQJ>> Links, IReadOnlyList<IReadOnlyDictionary<string, AUPZFJBQBV>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Id">UUID of Link target.</param>
/// <param name="Created"></param>
/// <param name="Class">Indicate whether this Link is hard, soft, or external.
</param>
/// <param name="Title">Name/label/title of the Link, as provided upon creation.
</param>
/// <param name="Target">URL of Link target.</param>
/// <param name="Href">URL to origin of Link.</param>
/// <param name="Collection">What kind of object is the target. (TODO)
</param>
public record FAKSUGUENT(string Id, double Created, string Class, string Title, string Target, string Href, string Collection);

/// <summary>
/// 
/// </summary>
/// <param name="Rel">Relation to this object.</param>
/// <param name="Href">URL to reference.</param>
public record MJDAGMXTWX(string Rel, string Href);

/// <summary>
/// 
/// </summary>
/// <param name="Links"></param>
/// <param name="Hrefs">List of references to other entities.
Should contain references for: `home`, `owner`, `self`.
</param>
public record TCBITTDQFH(IReadOnlyList<IReadOnlyDictionary<string, FAKSUGUENT>> Links, IReadOnlyList<IReadOnlyDictionary<string, MJDAGMXTWX>> Hrefs);

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record BOKFHGHGGQ();

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record VJDXATNOZI();

/// <summary>
/// 
/// </summary>
/// <param name="Id"></param>
/// <param name="Title"></param>
/// <param name="Collection"></param>
/// <param name="Class"></param>
public record KBDSLWRCXV(string Id, string Title, string Collection, string Class);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record DVWJSDMUZI(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="Link"></param>
/// <param name="Hrefs">List of references to other entities.
Should contain references for: `home`, `owner`, `self`, `target`,
</param>
public record FYPZYXUTTH(double LastModified, double Created, IReadOnlyDictionary<string, KBDSLWRCXV> Link, IReadOnlyList<IReadOnlyDictionary<string, DVWJSDMUZI>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Id"></param>
/// <param name="Title"></param>
/// <param name="Collection"></param>
/// <param name="Class"></param>
public record LJNOOABOCT(string Id, string Title, string Collection, string Class);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record REBVBETSXC(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="LastModified"></param>
/// <param name="Created"></param>
/// <param name="Link"></param>
/// <param name="Hrefs">List of references to other entities.
Should contain references for: `home`, `owner`, `self`, `target`,
</param>
public record PHXXGVKPGR(double LastModified, double Created, IReadOnlyDictionary<string, LJNOOABOCT> Link, IReadOnlyList<IReadOnlyDictionary<string, REBVBETSXC>> Hrefs);

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record JDGKVTCFSJ();

/// <summary>
/// Always returns `{"hrefs": []}`.
/// </summary>
public record HDFJBYAHMV();

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record PIBNGFALXP();

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record EHFECGANJE();

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
public record TYXPHWRBLG(string Id, string Root, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, PIBNGFALXP> Type, IReadOnlyDictionary<string, EHFECGANJE> Shape);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record YUAPPQHCMO();

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record XOOORDWKUL();

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
public record GHOAFEHUJV(string Id, string Root, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, YUAPPQHCMO> Type, IReadOnlyDictionary<string, XOOORDWKUL> Shape);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record ISHRBDJKJB(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Datasets"></param>
/// <param name="Hrefs">List of references to other objects.
Should contain references for: `attributes`, `data`, `home`, `root`, `self`
</param>
public record VPVSXWWGKX(IReadOnlyList<string> Datasets, IReadOnlyList<IReadOnlyDictionary<string, ISHRBDJKJB>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record ZIITTYMUJX(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Datasets"></param>
/// <param name="Hrefs">List of references to other objects.
Should contain references for: `attributes`, `data`, `home`, `root`, `self`
</param>
public record MFDQLQOLDY(IReadOnlyList<string> Datasets, IReadOnlyList<IReadOnlyDictionary<string, ZIITTYMUJX>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Name">Descriptive or identifying name. Must be unique in the fields list.
</param>
/// <param name="Type">Enum of pre-defined type, UUID of committed type, or type definition. (TODO: see `POST Dataset`?)
</param>
public record HJYNTNUHKC(string Name, string Type);

/// <summary>
/// TODO
/// </summary>
/// <param name="Class">TODO
</param>
/// <param name="Base">TODO
Only present if class is not `H5T_COMPUND`.
</param>
/// <param name="Fields">List of fields in a compound dataset.
Only present if `class` is `H5T_COMPOUND`.
</param>
public record ZGHGPCCVGB(string Class, string Base, IReadOnlyList<IReadOnlyDictionary<string, HJYNTNUHKC>> Fields);

/// <summary>
/// TODO
/// </summary>
/// <param name="Class">String enum indicating expected structure.
+ H5S_NULL -- Dataset has no data and no shape.
+ H5S_SCALAR -- Single entity as the Datast.
+ H5S_SIMPLE -- Dataset has hyperrectangular shape of
  one or more dimensions.
</param>
/// <param name="Dims">Extent of each dimension in Dataset.
Only present if `class` is `H5S_SIMPLE`.
</param>
/// <param name="Maxdims">Maximum possible extent for each dimension.
Value of `0` in array indicates that the dimension has unlimited maximum extent.
Only present if `class` is `H5S_SIMPLE`, and `maxdims` was included upon Dataset creation.
</param>
public record XOXLLNFVAJ(string Class, IReadOnlyList<double> Dims, IReadOnlyList<double> Maxdims);

/// <summary>
/// TODO
/// </summary>
public record WEVHPYRWWU();

/// <summary>
/// Dataset creation properties as provided upon creation.

/// </summary>
public record VTGEYUPBUO();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record DCDBJCBXIE(string Href, string Rel);

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
/// <param name="CreationProperties">Dataset creation properties as provided upon creation.
</param>
/// <param name="Hrefs">List of references to other objects.
Must include references to only: `attributes`, `data` (shape class `H5S_NULL` must _not_ include `data`), `root`, `self`.
</param>
public record ONNVDBSPIR(string Id, string Root, string Domain, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, ZGHGPCCVGB> Type, IReadOnlyDictionary<string, XOXLLNFVAJ> Shape, IReadOnlyDictionary<string, WEVHPYRWWU> Layout, IReadOnlyDictionary<string, VTGEYUPBUO> CreationProperties, IReadOnlyList<IReadOnlyDictionary<string, DCDBJCBXIE>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Name">Descriptive or identifying name. Must be unique in the fields list.
</param>
/// <param name="Type">Enum of pre-defined type, UUID of committed type, or type definition. (TODO: see `POST Dataset`?)
</param>
public record INIPQIIGHN(string Name, string Type);

/// <summary>
/// TODO
/// </summary>
/// <param name="Class">TODO
</param>
/// <param name="Base">TODO
Only present if class is not `H5T_COMPUND`.
</param>
/// <param name="Fields">List of fields in a compound dataset.
Only present if `class` is `H5T_COMPOUND`.
</param>
public record GTVHLSBXJL(string Class, string Base, IReadOnlyList<IReadOnlyDictionary<string, INIPQIIGHN>> Fields);

/// <summary>
/// TODO
/// </summary>
/// <param name="Class">String enum indicating expected structure.
+ H5S_NULL -- Dataset has no data and no shape.
+ H5S_SCALAR -- Single entity as the Datast.
+ H5S_SIMPLE -- Dataset has hyperrectangular shape of
  one or more dimensions.
</param>
/// <param name="Dims">Extent of each dimension in Dataset.
Only present if `class` is `H5S_SIMPLE`.
</param>
/// <param name="Maxdims">Maximum possible extent for each dimension.
Value of `0` in array indicates that the dimension has unlimited maximum extent.
Only present if `class` is `H5S_SIMPLE`, and `maxdims` was included upon Dataset creation.
</param>
public record DRFGIOJXSN(string Class, IReadOnlyList<double> Dims, IReadOnlyList<double> Maxdims);

/// <summary>
/// TODO
/// </summary>
public record RKHWMHEYUC();

/// <summary>
/// Dataset creation properties as provided upon creation.

/// </summary>
public record OSJAXVDEEC();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record CPCIEIHKJL(string Href, string Rel);

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
/// <param name="CreationProperties">Dataset creation properties as provided upon creation.
</param>
/// <param name="Hrefs">List of references to other objects.
Must include references to only: `attributes`, `data` (shape class `H5S_NULL` must _not_ include `data`), `root`, `self`.
</param>
public record JIRGXQWHYC(string Id, string Root, string Domain, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, GTVHLSBXJL> Type, IReadOnlyDictionary<string, DRFGIOJXSN> Shape, IReadOnlyDictionary<string, RKHWMHEYUC> Layout, IReadOnlyDictionary<string, OSJAXVDEEC> CreationProperties, IReadOnlyList<IReadOnlyDictionary<string, CPCIEIHKJL>> Hrefs);

/// <summary>
/// 
/// </summary>
public record JMDLEUCCSA();

/// <summary>
/// 
/// </summary>
public record PYNXYQKRUU();

/// <summary>
/// 
/// </summary>
/// <param name="Hrefs"></param>
public record KIYKYZKUGX(IReadOnlyList<string> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Hrefs"></param>
public record ASTRLTCBUR(IReadOnlyList<string> Hrefs);

/// <summary>
/// 
/// </summary>
public record HNVITXKAFZ();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record ULYBQZCOBA(string Href, string Rel);

/// <summary>
/// (See `GET /datasets/{id}`)

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Shape"></param>
/// <param name="Hrefs">Must include references to only: `owner`, `root`, `self`.
</param>
public record JRWPJTXPNL(double Created, double LastModified, IReadOnlyDictionary<string, HNVITXKAFZ> Shape, IReadOnlyList<IReadOnlyDictionary<string, ULYBQZCOBA>> Hrefs);

/// <summary>
/// 
/// </summary>
public record JTSDZNBIPU();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record LDEHDYCOMN(string Href, string Rel);

/// <summary>
/// (See `GET /datasets/{id}`)

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Shape"></param>
/// <param name="Hrefs">Must include references to only: `owner`, `root`, `self`.
</param>
public record DPVWNOXHKJ(double Created, double LastModified, IReadOnlyDictionary<string, JTSDZNBIPU> Shape, IReadOnlyList<IReadOnlyDictionary<string, LDEHDYCOMN>> Hrefs);

/// <summary>
/// 
/// </summary>
public record VEGOHRVUGF();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record LWDXLVBSRO(string Href, string Rel);

/// <summary>
/// (See `GET /datasets/{id}`)

/// </summary>
/// <param name="Type"></param>
/// <param name="Hrefs"></param>
public record XEYXXSFETW(IReadOnlyDictionary<string, VEGOHRVUGF> Type, IReadOnlyList<IReadOnlyDictionary<string, LWDXLVBSRO>> Hrefs);

/// <summary>
/// 
/// </summary>
public record HSGIPHJWKF();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record PXSSNUCWUS(string Href, string Rel);

/// <summary>
/// (See `GET /datasets/{id}`)

/// </summary>
/// <param name="Type"></param>
/// <param name="Hrefs"></param>
public record HVBBSHUWCW(IReadOnlyDictionary<string, HSGIPHJWKF> Type, IReadOnlyList<IReadOnlyDictionary<string, PXSSNUCWUS>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Value"></param>
public record VLIPJJSHOA(IReadOnlyList<JsonElement> Value);

/// <summary>
/// 
/// </summary>
/// <param name="Value"></param>
public record GJTKJCGRWP(IReadOnlyList<JsonElement> Value);

/// <summary>
/// 
/// </summary>
public record KKUMISUHVZ();

/// <summary>
/// 
/// </summary>
public record HXLYGNKOWJ();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record OQSLTBPWRF(double Created, string Href, string Name, IReadOnlyDictionary<string, KKUMISUHVZ> Shape, IReadOnlyDictionary<string, HXLYGNKOWJ> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record CGMAHKKELG(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record FIXXSUJQLK(IReadOnlyList<IReadOnlyDictionary<string, OQSLTBPWRF>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, CGMAHKKELG>> Hrefs);

/// <summary>
/// 
/// </summary>
public record WLCKKARJHL();

/// <summary>
/// 
/// </summary>
public record YEXNQOLAHU();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record BIPDVBIEKE(double Created, string Href, string Name, IReadOnlyDictionary<string, WLCKKARJHL> Shape, IReadOnlyDictionary<string, YEXNQOLAHU> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record FSEMPSSJWV(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record EARLRQMRVS(IReadOnlyList<IReadOnlyDictionary<string, BIPDVBIEKE>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, FSEMPSSJWV>> Hrefs);

/// <summary>
/// TODO
/// </summary>
public record QFGWKODFLY();

/// <summary>
/// TODO
/// </summary>
public record NZSJQBHBQY();

/// <summary>
/// 
/// </summary>
public record LWGNYMEIJF();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record KODCPQGMAB(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record KHXYZNKMRE(double Created, double LastModified, string Name, IReadOnlyDictionary<string, LWGNYMEIJF> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, KODCPQGMAB>> Hrefs);

/// <summary>
/// 
/// </summary>
public record FXVSHQJIDJ();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record QVGLHRYYAK(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record MPIQECJBFO(double Created, double LastModified, string Name, IReadOnlyDictionary<string, FXVSHQJIDJ> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, QVGLHRYYAK>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record WKAIMIOFPB(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record PXGPRGJYAE(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, WKAIMIOFPB>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record PPQOUNNVUO(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record TRSGTLUTVW(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, PPQOUNNVUO>> Hrefs);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record DMKKVQRIVH();

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record JVZLBAOCBY();

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
public record DQGFQVPSPG(string Id, string Root, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, DMKKVQRIVH> Type, IReadOnlyDictionary<string, JVZLBAOCBY> Shape);

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record MDLMBXYSVY();

/// <summary>
/// (See `GET /datasets/{id}`)
/// </summary>
public record KVYGIMOFZF();

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
public record YIRTLMMHEQ(string Id, string Root, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, MDLMBXYSVY> Type, IReadOnlyDictionary<string, KVYGIMOFZF> Shape);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record CJCTVOZZXO(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Datasets"></param>
/// <param name="Hrefs">List of references to other objects.
Should contain references for: `attributes`, `data`, `home`, `root`, `self`
</param>
public record KBERUCCSWO(IReadOnlyList<string> Datasets, IReadOnlyList<IReadOnlyDictionary<string, CJCTVOZZXO>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record UKQPOJZNJN(string Href, string Rel);

/// <summary>
/// 
/// </summary>
/// <param name="Datasets"></param>
/// <param name="Hrefs">List of references to other objects.
Should contain references for: `attributes`, `data`, `home`, `root`, `self`
</param>
public record QRWGIQALQL(IReadOnlyList<string> Datasets, IReadOnlyList<IReadOnlyDictionary<string, UKQPOJZNJN>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Name">Descriptive or identifying name. Must be unique in the fields list.
</param>
/// <param name="Type">Enum of pre-defined type, UUID of committed type, or type definition. (TODO: see `POST Dataset`?)
</param>
public record PGJAXBWTJX(string Name, string Type);

/// <summary>
/// TODO
/// </summary>
/// <param name="Class">TODO
</param>
/// <param name="Base">TODO
Only present if class is not `H5T_COMPUND`.
</param>
/// <param name="Fields">List of fields in a compound dataset.
Only present if `class` is `H5T_COMPOUND`.
</param>
public record VNTWBSLQXI(string Class, string Base, IReadOnlyList<IReadOnlyDictionary<string, PGJAXBWTJX>> Fields);

/// <summary>
/// TODO
/// </summary>
/// <param name="Class">String enum indicating expected structure.
+ H5S_NULL -- Dataset has no data and no shape.
+ H5S_SCALAR -- Single entity as the Datast.
+ H5S_SIMPLE -- Dataset has hyperrectangular shape of
  one or more dimensions.
</param>
/// <param name="Dims">Extent of each dimension in Dataset.
Only present if `class` is `H5S_SIMPLE`.
</param>
/// <param name="Maxdims">Maximum possible extent for each dimension.
Value of `0` in array indicates that the dimension has unlimited maximum extent.
Only present if `class` is `H5S_SIMPLE`, and `maxdims` was included upon Dataset creation.
</param>
public record ATHQHJJOOU(string Class, IReadOnlyList<double> Dims, IReadOnlyList<double> Maxdims);

/// <summary>
/// TODO
/// </summary>
public record PJEIPSFTHN();

/// <summary>
/// Dataset creation properties as provided upon creation.

/// </summary>
public record MWMJWCZJCO();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record RQOHHTVFVL(string Href, string Rel);

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
/// <param name="CreationProperties">Dataset creation properties as provided upon creation.
</param>
/// <param name="Hrefs">List of references to other objects.
Must include references to only: `attributes`, `data` (shape class `H5S_NULL` must _not_ include `data`), `root`, `self`.
</param>
public record RRKMVDIKZF(string Id, string Root, string Domain, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, VNTWBSLQXI> Type, IReadOnlyDictionary<string, ATHQHJJOOU> Shape, IReadOnlyDictionary<string, PJEIPSFTHN> Layout, IReadOnlyDictionary<string, MWMJWCZJCO> CreationProperties, IReadOnlyList<IReadOnlyDictionary<string, RQOHHTVFVL>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Name">Descriptive or identifying name. Must be unique in the fields list.
</param>
/// <param name="Type">Enum of pre-defined type, UUID of committed type, or type definition. (TODO: see `POST Dataset`?)
</param>
public record SVCDHCGNOZ(string Name, string Type);

/// <summary>
/// TODO
/// </summary>
/// <param name="Class">TODO
</param>
/// <param name="Base">TODO
Only present if class is not `H5T_COMPUND`.
</param>
/// <param name="Fields">List of fields in a compound dataset.
Only present if `class` is `H5T_COMPOUND`.
</param>
public record SRUVPNISDS(string Class, string Base, IReadOnlyList<IReadOnlyDictionary<string, SVCDHCGNOZ>> Fields);

/// <summary>
/// TODO
/// </summary>
/// <param name="Class">String enum indicating expected structure.
+ H5S_NULL -- Dataset has no data and no shape.
+ H5S_SCALAR -- Single entity as the Datast.
+ H5S_SIMPLE -- Dataset has hyperrectangular shape of
  one or more dimensions.
</param>
/// <param name="Dims">Extent of each dimension in Dataset.
Only present if `class` is `H5S_SIMPLE`.
</param>
/// <param name="Maxdims">Maximum possible extent for each dimension.
Value of `0` in array indicates that the dimension has unlimited maximum extent.
Only present if `class` is `H5S_SIMPLE`, and `maxdims` was included upon Dataset creation.
</param>
public record ERCFDDPHDX(string Class, IReadOnlyList<double> Dims, IReadOnlyList<double> Maxdims);

/// <summary>
/// TODO
/// </summary>
public record PAXRXGDLWJ();

/// <summary>
/// Dataset creation properties as provided upon creation.

/// </summary>
public record IETHMGPQXF();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL to reference.</param>
/// <param name="Rel">Relation to this object.</param>
public record OUGSHYCREL(string Href, string Rel);

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
/// <param name="CreationProperties">Dataset creation properties as provided upon creation.
</param>
/// <param name="Hrefs">List of references to other objects.
Must include references to only: `attributes`, `data` (shape class `H5S_NULL` must _not_ include `data`), `root`, `self`.
</param>
public record ZALSNSTICH(string Id, string Root, string Domain, double Created, double LastModified, double AttributeCount, IReadOnlyDictionary<string, SRUVPNISDS> Type, IReadOnlyDictionary<string, ERCFDDPHDX> Shape, IReadOnlyDictionary<string, PAXRXGDLWJ> Layout, IReadOnlyDictionary<string, IETHMGPQXF> CreationProperties, IReadOnlyList<IReadOnlyDictionary<string, OUGSHYCREL>> Hrefs);

/// <summary>
/// 
/// </summary>
public record UQLEOGFMBZ();

/// <summary>
/// 
/// </summary>
public record SUTFXKZRAX();

/// <summary>
/// 
/// </summary>
/// <param name="Hrefs"></param>
public record NGONRYYXXW(IReadOnlyList<string> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Hrefs"></param>
public record UQRNZUEONZ(IReadOnlyList<string> Hrefs);

/// <summary>
/// 
/// </summary>
public record KGTVLSVGDG();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GIRZAAADRB(string Href, string Rel);

/// <summary>
/// (See `GET /datasets/{id}`)

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Shape"></param>
/// <param name="Hrefs">Must include references to only: `owner`, `root`, `self`.
</param>
public record NXPZRVSEFE(double Created, double LastModified, IReadOnlyDictionary<string, KGTVLSVGDG> Shape, IReadOnlyList<IReadOnlyDictionary<string, GIRZAAADRB>> Hrefs);

/// <summary>
/// 
/// </summary>
public record DXGXUNGPIT();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record UUBXZCBYPC(string Href, string Rel);

/// <summary>
/// (See `GET /datasets/{id}`)

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Shape"></param>
/// <param name="Hrefs">Must include references to only: `owner`, `root`, `self`.
</param>
public record HGZPNZKYHZ(double Created, double LastModified, IReadOnlyDictionary<string, DXGXUNGPIT> Shape, IReadOnlyList<IReadOnlyDictionary<string, UUBXZCBYPC>> Hrefs);

/// <summary>
/// 
/// </summary>
public record RVCAHTAWQB();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record NMPEATJTGG(string Href, string Rel);

/// <summary>
/// (See `GET /datasets/{id}`)

/// </summary>
/// <param name="Type"></param>
/// <param name="Hrefs"></param>
public record EWXLIZVQGR(IReadOnlyDictionary<string, RVCAHTAWQB> Type, IReadOnlyList<IReadOnlyDictionary<string, NMPEATJTGG>> Hrefs);

/// <summary>
/// 
/// </summary>
public record TFGWLHCYAL();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record FPDFONAKNK(string Href, string Rel);

/// <summary>
/// (See `GET /datasets/{id}`)

/// </summary>
/// <param name="Type"></param>
/// <param name="Hrefs"></param>
public record WPRBXVZTXX(IReadOnlyDictionary<string, TFGWLHCYAL> Type, IReadOnlyList<IReadOnlyDictionary<string, FPDFONAKNK>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Value"></param>
public record ZJWYTWMGQP(IReadOnlyList<JsonElement> Value);

/// <summary>
/// 
/// </summary>
/// <param name="Value"></param>
public record SRXIBTQWDJ(IReadOnlyList<JsonElement> Value);

/// <summary>
/// 
/// </summary>
public record TMLXNAXWLT();

/// <summary>
/// 
/// </summary>
public record BBWYCTVUPR();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record GDXGKPHINA(double Created, string Href, string Name, IReadOnlyDictionary<string, TMLXNAXWLT> Shape, IReadOnlyDictionary<string, BBWYCTVUPR> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record SOZGWXPMZQ(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record LTILCWOSGJ(IReadOnlyList<IReadOnlyDictionary<string, GDXGKPHINA>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, SOZGWXPMZQ>> Hrefs);

/// <summary>
/// 
/// </summary>
public record AQROQABTVG();

/// <summary>
/// 
/// </summary>
public record KHOZFBBZZA();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record JKHXJFGRTG(double Created, string Href, string Name, IReadOnlyDictionary<string, AQROQABTVG> Shape, IReadOnlyDictionary<string, KHOZFBBZZA> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record LVXUNZDNRT(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record XIHPYLQWZC(IReadOnlyList<IReadOnlyDictionary<string, JKHXJFGRTG>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, LVXUNZDNRT>> Hrefs);

/// <summary>
/// TODO
/// </summary>
public record WAFUPCNZMM();

/// <summary>
/// TODO
/// </summary>
public record RCHLDYUDUG();

/// <summary>
/// 
/// </summary>
public record LBISPZZQHJ();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record QHHWYRHRFE(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record JECZBJUKNJ(double Created, double LastModified, string Name, IReadOnlyDictionary<string, LBISPZZQHJ> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, QHHWYRHRFE>> Hrefs);

/// <summary>
/// 
/// </summary>
public record MDCMSVXPWV();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record KJTZWOTJDB(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record AUWRZCZEGE(double Created, double LastModified, string Name, IReadOnlyDictionary<string, MDCMSVXPWV> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, KJTZWOTJDB>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record ROQKYWRYXX(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record IJULVXOQVT(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, ROQKYWRYXX>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record WGQMJELIHZ(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record QIXFWUSQOA(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, WGQMJELIHZ>> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Id"></param>
public record NRTZSSNVMF(double AttributeCount, string Id);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Id"></param>
public record WEKIIUKKUG(double AttributeCount, string Id);

/// <summary>
/// 
/// </summary>
public record RFTAKSWAOB();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GNZAWLCJOI(string Href, string Rel);

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
public record CYDKSTGFGK(double AttributeCount, double Created, string Id, double LastModified, string Root, IReadOnlyDictionary<string, RFTAKSWAOB> Type, IReadOnlyList<IReadOnlyDictionary<string, GNZAWLCJOI>> Hrefs);

/// <summary>
/// 
/// </summary>
public record FOEFBICBUG();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record YZYAPUFTTY(string Href, string Rel);

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
public record TOMKNIIOBJ(double AttributeCount, double Created, string Id, double LastModified, string Root, IReadOnlyDictionary<string, FOEFBICBUG> Type, IReadOnlyList<IReadOnlyDictionary<string, YZYAPUFTTY>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record FXKUTHITRW(string Href, string Rel);

/// <summary>
/// Always returns `{"hrefs": []}` (TODO confirm)
/// </summary>
/// <param name="Hrefs"></param>
public record IYLQIHHPJY(IReadOnlyList<IReadOnlyDictionary<string, FXKUTHITRW>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record UGDBWXRLJK(string Href, string Rel);

/// <summary>
/// Always returns `{"hrefs": []}` (TODO confirm)
/// </summary>
/// <param name="Hrefs"></param>
public record ZDKLMTILZM(IReadOnlyList<IReadOnlyDictionary<string, UGDBWXRLJK>> Hrefs);

/// <summary>
/// 
/// </summary>
public record VGSSECBEWL();

/// <summary>
/// 
/// </summary>
public record PMLMXPBMKA();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record VOLAKQQRUS(double Created, string Href, string Name, IReadOnlyDictionary<string, VGSSECBEWL> Shape, IReadOnlyDictionary<string, PMLMXPBMKA> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record HIJBBYBLCM(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record GBHFAYBETJ(IReadOnlyList<IReadOnlyDictionary<string, VOLAKQQRUS>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, HIJBBYBLCM>> Hrefs);

/// <summary>
/// 
/// </summary>
public record JIGBRAYUGP();

/// <summary>
/// 
/// </summary>
public record QIFNNSHNJP();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record MYBMTQOGYO(double Created, string Href, string Name, IReadOnlyDictionary<string, JIGBRAYUGP> Shape, IReadOnlyDictionary<string, QIFNNSHNJP> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record CLOKRTPAKY(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record GJKQWCFYGA(IReadOnlyList<IReadOnlyDictionary<string, MYBMTQOGYO>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, CLOKRTPAKY>> Hrefs);

/// <summary>
/// TODO
/// </summary>
public record MTAKSDLEEV();

/// <summary>
/// TODO
/// </summary>
public record ZGNUKJZUZL();

/// <summary>
/// 
/// </summary>
public record YBERVXTFKQ();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GMDGYVPKOD(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record KDFECTMLWK(double Created, double LastModified, string Name, IReadOnlyDictionary<string, YBERVXTFKQ> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, GMDGYVPKOD>> Hrefs);

/// <summary>
/// 
/// </summary>
public record ASDRIVWIJI();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record VFNNRKYHAE(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record WCORIBEZFJ(double Created, double LastModified, string Name, IReadOnlyDictionary<string, ASDRIVWIJI> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, VFNNRKYHAE>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">Relation to `href`.</param>
public record UQSRLFQPDS(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record WEXAGCHLWF(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, UQSRLFQPDS>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">Relation to `href`.</param>
public record HGFYJZTSKR(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record WZYHJICUEG(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, HGFYJZTSKR>> Hrefs);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Id"></param>
public record WBJQBCKPRR(double AttributeCount, string Id);

/// <summary>
/// TODO
/// </summary>
/// <param name="AttributeCount"></param>
/// <param name="Id"></param>
public record SBJPVBCFLB(double AttributeCount, string Id);

/// <summary>
/// 
/// </summary>
public record LSVCRMFNJI();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record VFXZLNMBWB(string Href, string Rel);

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
public record YZQXPPIAXX(double AttributeCount, double Created, string Id, double LastModified, string Root, IReadOnlyDictionary<string, LSVCRMFNJI> Type, IReadOnlyList<IReadOnlyDictionary<string, VFXZLNMBWB>> Hrefs);

/// <summary>
/// 
/// </summary>
public record SSQTNWXWTS();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record IZEFXHOOVO(string Href, string Rel);

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
public record GCZEEOJBYA(double AttributeCount, double Created, string Id, double LastModified, string Root, IReadOnlyDictionary<string, SSQTNWXWTS> Type, IReadOnlyList<IReadOnlyDictionary<string, IZEFXHOOVO>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record PDYHJXBYGR(string Href, string Rel);

/// <summary>
/// Always returns `{"hrefs": []}` (TODO confirm)
/// </summary>
/// <param name="Hrefs"></param>
public record EFNRUOJGAH(IReadOnlyList<IReadOnlyDictionary<string, PDYHJXBYGR>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record BLFWNXMDLS(string Href, string Rel);

/// <summary>
/// Always returns `{"hrefs": []}` (TODO confirm)
/// </summary>
/// <param name="Hrefs"></param>
public record CEITCOMKRU(IReadOnlyList<IReadOnlyDictionary<string, BLFWNXMDLS>> Hrefs);

/// <summary>
/// 
/// </summary>
public record XSZECIAZYC();

/// <summary>
/// 
/// </summary>
public record CJDEXYEQLV();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record HIHWSHPRWI(double Created, string Href, string Name, IReadOnlyDictionary<string, XSZECIAZYC> Shape, IReadOnlyDictionary<string, CJDEXYEQLV> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record KBYXAKXKBD(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record FEMNFLFDWC(IReadOnlyList<IReadOnlyDictionary<string, HIHWSHPRWI>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, KBYXAKXKBD>> Hrefs);

/// <summary>
/// 
/// </summary>
public record ECBGUUPWZJ();

/// <summary>
/// 
/// </summary>
public record YXNVUFRTVD();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record QDHURMMLSM(double Created, string Href, string Name, IReadOnlyDictionary<string, ECBGUUPWZJ> Shape, IReadOnlyDictionary<string, YXNVUFRTVD> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record RBGTKYYKPQ(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record DEYMAAKNHW(IReadOnlyList<IReadOnlyDictionary<string, QDHURMMLSM>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, RBGTKYYKPQ>> Hrefs);

/// <summary>
/// TODO
/// </summary>
public record SHNIOEQJSB();

/// <summary>
/// TODO
/// </summary>
public record KWOKZFDMJJ();

/// <summary>
/// 
/// </summary>
public record BBEXRMJXRW();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record DGLJPQHDXV(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record STAUFORIGU(double Created, double LastModified, string Name, IReadOnlyDictionary<string, BBEXRMJXRW> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, DGLJPQHDXV>> Hrefs);

/// <summary>
/// 
/// </summary>
public record SANLDFGDSF();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record JYMCGRIEBQ(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record NVUBFFCSHV(double Created, double LastModified, string Name, IReadOnlyDictionary<string, SANLDFGDSF> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, JYMCGRIEBQ>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">Relation to `href`.</param>
public record AIHCZWSMWW(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record VGHSDXTAHC(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, AIHCZWSMWW>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">Relation to `href`.</param>
public record VDANMURIPF(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record YMWXYBEGPS(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, VDANMURIPF>> Hrefs);

/// <summary>
/// 
/// </summary>
public record CHLFGQQZFB();

/// <summary>
/// 
/// </summary>
public record AYHKTJXGNH();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record GLLCVFPNTH(double Created, string Href, string Name, IReadOnlyDictionary<string, CHLFGQQZFB> Shape, IReadOnlyDictionary<string, AYHKTJXGNH> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record RVZJEOMESW(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record LTBYROJYDV(IReadOnlyList<IReadOnlyDictionary<string, GLLCVFPNTH>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, RVZJEOMESW>> Hrefs);

/// <summary>
/// 
/// </summary>
public record LKTOOIMKTH();

/// <summary>
/// 
/// </summary>
public record QNFAGSUUEM();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record JZPQDVHBBU(double Created, string Href, string Name, IReadOnlyDictionary<string, LKTOOIMKTH> Shape, IReadOnlyDictionary<string, QNFAGSUUEM> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record TKDJEWJMNZ(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record QWJNUVMAAK(IReadOnlyList<IReadOnlyDictionary<string, JZPQDVHBBU>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, TKDJEWJMNZ>> Hrefs);

/// <summary>
/// TODO
/// </summary>
public record VINLPCNGPT();

/// <summary>
/// TODO
/// </summary>
public record IYNHDFEMDP();

/// <summary>
/// 
/// </summary>
public record LVQGXQZNAF();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record BSYYCIDMLC(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record DETZJIGXAI(double Created, double LastModified, string Name, IReadOnlyDictionary<string, LVQGXQZNAF> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, BSYYCIDMLC>> Hrefs);

/// <summary>
/// 
/// </summary>
public record EASMPGUANP();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record WFWSEKGUZN(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record VSPLEVVKTG(double Created, double LastModified, string Name, IReadOnlyDictionary<string, EASMPGUANP> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, WFWSEKGUZN>> Hrefs);

/// <summary>
/// 
/// </summary>
public record IKTLCDOJSD();

/// <summary>
/// 
/// </summary>
public record DLDMWHODEQ();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record SROCDCGNUW(double Created, string Href, string Name, IReadOnlyDictionary<string, IKTLCDOJSD> Shape, IReadOnlyDictionary<string, DLDMWHODEQ> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record PFRSRTCHHI(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record HPKRKTFWIW(IReadOnlyList<IReadOnlyDictionary<string, SROCDCGNUW>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, PFRSRTCHHI>> Hrefs);

/// <summary>
/// 
/// </summary>
public record YFIXQZVIHE();

/// <summary>
/// 
/// </summary>
public record EKMYXWAZQR();

/// <summary>
/// 
/// </summary>
/// <param name="Created"></param>
/// <param name="Href"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Type"></param>
/// <param name="Value"></param>
public record TJFKXNTJAT(double Created, string Href, string Name, IReadOnlyDictionary<string, YFIXQZVIHE> Shape, IReadOnlyDictionary<string, EKMYXWAZQR> Type, string Value);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record BWEKPFPQSN(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Attributes"></param>
/// <param name="Hrefs"></param>
public record KYUHABLTNV(IReadOnlyList<IReadOnlyDictionary<string, TJFKXNTJAT>> Attributes, IReadOnlyList<IReadOnlyDictionary<string, BWEKPFPQSN>> Hrefs);

/// <summary>
/// TODO
/// </summary>
public record CUQFSBIQTK();

/// <summary>
/// TODO
/// </summary>
public record LHNIFAAPVB();

/// <summary>
/// 
/// </summary>
public record SDTOCOWVCY();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record SKZALJOXKB(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record DGBUMXPURX(double Created, double LastModified, string Name, IReadOnlyDictionary<string, SDTOCOWVCY> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, SKZALJOXKB>> Hrefs);

/// <summary>
/// 
/// </summary>
public record OMADPFLDUV();

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record YGYUFDXKLD(string Href, string Rel);

/// <summary>
/// TODO

/// </summary>
/// <param name="Created"></param>
/// <param name="LastModified"></param>
/// <param name="Name"></param>
/// <param name="Shape"></param>
/// <param name="Value"></param>
/// <param name="Hrefs"></param>
public record UYLTQADIKR(double Created, double LastModified, string Name, IReadOnlyDictionary<string, OMADPFLDUV> Shape, string Value, IReadOnlyList<IReadOnlyDictionary<string, YGYUFDXKLD>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record RFNUJHXKQC(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record LYIPSNVOCE(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, RFNUJHXKQC>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record XZPPFERLKV(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record KPSVLUCATA(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, XZPPFERLKV>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record EWTRXNJBKB(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record XLRTNQRIOE(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, EWTRXNJBKB>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record CWVUAWXSXX(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record JALTAMQLUB(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, CWVUAWXSXX>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GCAADNBNRD(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record PVPLFFKULC(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, GCAADNBNRD>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record IWHEHDUWZN(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record ZTQKKJSDRN(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, IWHEHDUWZN>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record QMZURNXSUZ(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record GERACQMIGQ(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, QMZURNXSUZ>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record JXCOYFKKXH(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record ALWLMBACNS(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, JXCOYFKKXH>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record WYAJCLXRNG(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record AXUQNWUGQX(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, WYAJCLXRNG>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record KQBBCKFHMF(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record AOGQHSUSOQ(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, KQBBCKFHMF>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record SJWATKLKWA(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record MPOQEBVTVR(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, SJWATKLKWA>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record VVVYEHRAQQ(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record IDEGENHEXB(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, VVVYEHRAQQ>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">Relation to `href`.</param>
public record NWCXPMAQPH(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record XGRXJKWOGE(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, NWCXPMAQPH>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">Relation to `href`.</param>
public record ULZBHNANRN(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record SLEMVOBDRY(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, ULZBHNANRN>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record KCKCRIZDFA(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record DYMHOBSUQK(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, KCKCRIZDFA>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record RHPZWVJQRQ(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record ZDPAHSXPOF(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, RHPZWVJQRQ>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record PKFOTYIQEB(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record OQSWHLNSJK(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, PKFOTYIQEB>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record TKBPSVLCCT(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record QVMIWOXAGK(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, TKBPSVLCCT>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record VSKVPRFIHL(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record SMZOPXPEOP(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, VSKVPRFIHL>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record AYEKYQFKFK(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record GDWZEEBXQX(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, AYEKYQFKFK>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record EVZIEIJJBB(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record QZHWBXFNVB(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, EVZIEIJJBB>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record VDZAZXBKSD(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record QREXGSXIVO(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, VDZAZXBKSD>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record GQKUSYHAMB(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record AXDEBEGGKF(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, GQKUSYHAMB>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record YURXJLDHKL(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acl">Access Control List for a single user.</param>
/// <param name="Hrefs"></param>
public record VYDICRSQHU(ACL Acl, IReadOnlyList<IReadOnlyDictionary<string, YURXJLDHKL>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record LTEUPLFXMF(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record RWTIYILCEN(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, LTEUPLFXMF>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">relation to this object</param>
public record TJIXJPEDXR(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record MNSNOKDWNJ(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, TJIXJPEDXR>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">Relation to `href`.</param>
public record ZVKRRPCNJY(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record BMPJRRKKYP(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, ZVKRRPCNJY>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Href">URL of resource</param>
/// <param name="Rel">Relation to `href`.</param>
public record KUEELZHOAT(string Href, string Rel);

/// <summary>
/// TODO
/// </summary>
/// <param name="Acls">Access Control Lists for users.</param>
/// <param name="Hrefs"></param>
public record RRNWQQUYGW(ACLS Acls, IReadOnlyList<IReadOnlyDictionary<string, KUEELZHOAT>> Hrefs);

/// <summary>
/// 
/// </summary>
/// <param name="Create"></param>
/// <param name="Update"></param>
/// <param name="Delete"></param>
/// <param name="UpdateACL"></param>
/// <param name="Read"></param>
/// <param name="ReadACL"></param>
public record TXSRBYCSLW(bool Create, bool Update, bool Delete, bool UpdateACL, bool Read, bool ReadACL);



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

