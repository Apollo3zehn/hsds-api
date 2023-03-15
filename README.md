# Hsds.Api

[![GitHub Actions](https://github.com/Apollo3zehn/apollo3zehn-openapi-client-generator/actions/workflows/build-and-publish.yml/badge.svg)](https://github.com/Apollo3zehn/hsds-api/actions) [![NuGet](https://img.shields.io/nuget/v/Hsds.Api?label=Nuget)](https://www.nuget.org/packages/Hsds.Api)

This project hosts auto-generated C# and (type-annotated) Python clients for the HDF5 Highly Scalable Data Service (HSDS).

You can use them like this:

## .NET 6+

`dotnet add package Hsds.Api --prerelease`

```cs
var url = new Uri("http://hsdshdflab.hdfgroup.org");
using var client = new HsdsClient(url);
var domainName = "/shared/tall.h5";

/* get domain */
var domain = client.Domain.GetDomain(domainName);
var rootGroupLinks = client.Link.GetLinks(domain.Root, domainName);

/* get group "g1" */
var g1_Link = rootGroupLinks.Links[0];
var g1_Name = g1_Link.Title;

/* get group "g1.1" */
var g1_Links = client.Link.GetLinks(g1_Link.Id, domainName);
var g1_1_Link = g1_Links.Links[0];
var g1_1_Name = g1_1_Link.Title;
var g1_1_Links = client.Link.GetLinks(g1_1_Link.Id, domainName);

/* get dataset "dset1.1.1" */
var dset_1_1_1_Link = g1_1_Links.Links[0];
var dset_1_1_1_Name = dset_1_1_1_Link.Title;

var dset_1_1_1_Type = client.Dataset
    .GetDataset(dset_1_1_1_Link.Id, domainName)
    .Type;

/* get data of dataset "dset1.1.1" as JSON */
var jsonResponse = client.Dataset.GetValuesAsJson(dset_1_1_1_Link.Id, domainName);

/* get data of dataset "dset1.1.1" as Stream */
var streamResponse = client.Dataset.GetValuesAsStream(dset_1_1_1_Link.Id, domainName);
var stream = streamResponse.Content.ReadAsStream();
```

### Async

All client methods have an async counterpart:

```cs
await client.Domain.GetDomainAsync(...)
```

### Authentication

HSDS supports basic authentication. This requires the user to set the `Authorization` header of the `HttpClient` like this:

```cs
var username = "...";
var password = "...";
var authenticationString = $"{username}:{password}";
var encodedAuthString = Convert.ToBase64String(Encoding.UTF8.GetBytes(authenticationString));
var url = new Uri("http://hsdshdflab.hdfgroup.org");
var httpClient = new HttpClient() { BaseAddress = url };
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("basic", encodedAuthString);

using var client = new HsdsClient(httpClient);
```

## Python

```python
url = "http://hsdshdflab.hdfgroup.org"

with HsdsClient.create(url) as client:

    domain_name = "/shared/tall.h5"

    # get domain
    domain = client.domain.get_domain(domain_name)
    root_group_links = client.link.get_links(domain.root, domain_name)

    # get group "g1"
    g1_link = root_group_links.links[0]
    g1_name = g1_link.title

    # get group "g1.1"
    g1_links = client.link.get_links(g1_link.id, domain_name)
    g1_1_link = g1_links.links[0]
    g1_1_name = g1_1_link.title
    g1_1_links = client.link.get_links(g1_1_link.id, domain_name)

    # get dataset "dset1.1.1"
    dset_1_1_1_link = g1_1_links.links[0]
    dset_1_1_1_name = dset_1_1_1_link.title

    dset_1_1_1_type = client.dataset \
        .get_dataset(dset_1_1_1_link.id, domain_name) \
        .type

    # get data of dataset "dset1.1.1" as JSON
    json_response = client.dataset.get_values_as_json(dset_1_1_1_link.id, domain_name)

    # get data of dataset "dset1.1.1" as stream
    stream_response = client.dataset.get_values_as_stream(dset_1_1_1_link.id, domain_name)
    data = stream_response.read()
```

### Async

The client has an async counterpart:

```python
from hsds_api import HsdsAsyncClient

...

async with HsdsAsyncClient.create(url) as client:
    await client.domain.get_domain(...)
    ...
```

### Authentication

HSDS supports basic authentication. This requires the user to pass they credentials like this:

```python
from hsds_api import HsdsClient
from httpx import Client

username = "..."
password = "..."
url = "http://hsdshdflab.hdfgroup.org"
http_client = Client(base_url=url, auth=(username, password))

with HsdsClient(http_client) as client:
    ...
```