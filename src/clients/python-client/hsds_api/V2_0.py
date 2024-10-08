# Python <= 3.9
from __future__ import annotations

import json
from dataclasses import dataclass
from datetime import datetime
from typing import AsyncIterable, Awaitable, Iterable, Optional, TypeVar, Union
from urllib.parse import quote
from uuid import UUID

from httpx import Response

from . import JsonEncoder
from ._client import HsdsAsyncClient, HsdsClient
from ._shared import _json_encoder_options, _to_string

T = TypeVar("T")

class V2_0:
    """A client for version V2_0."""
    
    _domain: DomainClient
    _group: GroupClient
    _link: LinkClient
    _dataset: DatasetClient
    _datatype: DatatypeClient
    _attribute: AttributeClient
    _aCLS: ACLSClient


    def __init__(self, client: HsdsClient):
        """
        Initializes a new instance of V2_0
        
            Args:
                client: The client to use.
        """

        self._domain = DomainClient(client)
        self._group = GroupClient(client)
        self._link = LinkClient(client)
        self._dataset = DatasetClient(client)
        self._datatype = DatatypeClient(client)
        self._attribute = AttributeClient(client)
        self._aCLS = ACLSClient(client)


    @property
    def domain(self) -> DomainClient:
        """Gets the DomainClient."""
        return self._domain

    @property
    def group(self) -> GroupClient:
        """Gets the GroupClient."""
        return self._group

    @property
    def link(self) -> LinkClient:
        """Gets the LinkClient."""
        return self._link

    @property
    def dataset(self) -> DatasetClient:
        """Gets the DatasetClient."""
        return self._dataset

    @property
    def datatype(self) -> DatatypeClient:
        """Gets the DatatypeClient."""
        return self._datatype

    @property
    def attribute(self) -> AttributeClient:
        """Gets the AttributeClient."""
        return self._attribute

    @property
    def acls(self) -> ACLSClient:
        """Gets the ACLSClient."""
        return self._aCLS



class DomainClient:
    """Provides methods to interact with domain."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def put_domain(self, domain: str, body: Optional[object], folder: Optional[float] = None) -> PutDomainResponse:
        """
        Create a new Domain on the service.

        Args:
            domain: 
            folder: If present and `1`, creates a Folder instead of a Domain.
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if folder is not None:
            __query_values["folder"] = quote(_to_string(folder), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutDomainResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_domain(self, domain: str) -> GetDomainResponse:
        """
        Get information about the requested domain.

        Args:
            domain: 
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDomainResponse, "GET", __url, "application/json", None, None)

    def delete_domain(self, domain: str) -> DeleteDomainResponse:
        """
        Delete the specified Domain or Folder.

        Args:
            domain: 
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDomainResponse, "DELETE", __url, "application/json", None, None)

    def post_group(self, domain: str, body: Optional[object]) -> PostGroupResponse:
        """
        Create a new Group.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostGroupResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_groups(self, domain: str) -> GetGroupsResponse:
        """
        Get UUIDs for all non-root Groups in Domain.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupsResponse, "GET", __url, "application/json", None, None)

    def post_dataset(self, domain: str, body: object) -> PostDatasetResponse:
        """
        Create a Dataset.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDatasetResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datasets(self, domain: str) -> GetDatasetsResponse:
        """
        List Datasets.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetsResponse, "GET", __url, "application/json", None, None)

    def post_data_type(self, domain: str, body: object) -> PostDataTypeResponse:
        """
        Commit a Datatype to the Domain.

        Args:
            domain: 
        """

        __url = "/datatypes"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDataTypeResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_access_lists(self, domain: str) -> GetAccessListsResponse:
        """
        Get access lists on Domain.

        Args:
            domain: 
        """

        __url = "/acls"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_user_access(self, domain: str, user: str) -> GetUserAccessResponse:
        """
        Get users's access to a Domain.

        Args:
            domain: 
            user: User identifier/name.
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetUserAccessResponse, "GET", __url, "application/json", None, None)

    def put_user_access(self, user: str, domain: str, body: object) -> PutUserAccessResponse:
        """
        Set user's access to the Domain.

        Args:
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutUserAccessResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))


class GroupClient:
    """Provides methods to interact with group."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def post_group(self, domain: str, body: Optional[object]) -> PostGroupResponse:
        """
        Create a new Group.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostGroupResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_groups(self, domain: str) -> GetGroupsResponse:
        """
        Get UUIDs for all non-root Groups in Domain.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupsResponse, "GET", __url, "application/json", None, None)

    def get_group(self, id: str, domain: str, getalias: Optional[int] = None) -> GetGroupResponse:
        """
        Get information about a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
            getalias: 
        """

        __url = "/groups/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if getalias is not None:
            __query_values["getalias"] = quote(_to_string(getalias), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupResponse, "GET", __url, "application/json", None, None)

    def delete_group(self, id: str, domain: str) -> DeleteGroupResponse:
        """
        Delete a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteGroupResponse, "DELETE", __url, "application/json", None, None)

    def get_attributes(self, collection: str, obj_uuid: str, domain: str, limit: Optional[float] = None, marker: Optional[str] = None) -> GetAttributesResponse:
        """
        List all Attributes attached to the HDF5 object `obj_uuid`.

        Args:
            collection: The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).
            obj_uuid: UUID of object.
            domain: 
            limit: Cap the number of Attributes listed.
            marker: Start Attribute listing _after_ the given name.
        """

        __url = "/{collection}/{obj_uuid}/attributes"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str, body: object) -> PutAttributeResponse:
        """
        Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.

        Args:
            domain: 
            collection: The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).
            obj_uuid: HDF5 object's UUID.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str) -> AttributeType:
        """
        Get information about an Attribute.

        Args:
            domain: 
            collection: Collection of object (Group, Dataset, or Datatype).
            obj_uuid: UUID of object.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(AttributeType, "GET", __url, "application/json", None, None)

    def get_group_access_lists(self, id: str, domain: str) -> GetGroupAccessListsResponse:
        """
        List access lists on Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_group_user_access(self, id: str, user: str, domain: str) -> GetGroupUserAccessResponse:
        """
        Get users's access to a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/groups/{id}/acls/{user}"
        __url = __url.replace("{id}", quote(str(id), safe=""))
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupUserAccessResponse, "GET", __url, "application/json", None, None)


class LinkClient:
    """Provides methods to interact with link."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def get_links(self, id: str, domain: str, limit: Optional[float] = None, marker: Optional[str] = None) -> GetLinksResponse:
        """
        List all Links in a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
            limit: Cap the number of Links returned in list.
            marker: Title of a Link; the first Link name to list.
        """

        __url = "/groups/{id}/links"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetLinksResponse, "GET", __url, "application/json", None, None)

    def put_link(self, id: str, linkname: str, domain: str, body: object) -> PutLinkResponse:
        """
        Create a new Link in a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            linkname: 
            domain: 
        """

        __url = "/groups/{id}/links/{linkname}"
        __url = __url.replace("{id}", quote(str(id), safe=""))
        __url = __url.replace("{linkname}", quote(str(linkname), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutLinkResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_link(self, id: str, linkname: str, domain: str) -> GetLinkResponse:
        """
        Get Link info.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            linkname: 
            domain: 
        """

        __url = "/groups/{id}/links/{linkname}"
        __url = __url.replace("{id}", quote(str(id), safe=""))
        __url = __url.replace("{linkname}", quote(str(linkname), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetLinkResponse, "GET", __url, "application/json", None, None)

    def delete_link(self, id: str, linkname: str, domain: str) -> DeleteLinkResponse:
        """
        Delete Link.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            linkname: 
            domain: 
        """

        __url = "/groups/{id}/links/{linkname}"
        __url = __url.replace("{id}", quote(str(id), safe=""))
        __url = __url.replace("{linkname}", quote(str(linkname), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteLinkResponse, "DELETE", __url, "application/json", None, None)


class DatasetClient:
    """Provides methods to interact with dataset."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def post_dataset(self, domain: str, body: object) -> PostDatasetResponse:
        """
        Create a Dataset.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDatasetResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datasets(self, domain: str) -> GetDatasetsResponse:
        """
        List Datasets.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetsResponse, "GET", __url, "application/json", None, None)

    def get_dataset(self, id: str, domain: str) -> GetDatasetResponse:
        """
        Get information about a Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetResponse, "GET", __url, "application/json", None, None)

    def delete_dataset(self, id: str, domain: str) -> DeleteDatasetResponse:
        """
        Delete a Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDatasetResponse, "DELETE", __url, "application/json", None, None)

    def put_shape(self, id: str, domain: str, body: object) -> PutShapeResponse:
        """
        Modify a Dataset's dimensions.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/shape"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutShapeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_shape(self, id: str, domain: str) -> GetShapeResponse:
        """
        Get information about a Dataset's shape.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/shape"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetShapeResponse, "GET", __url, "application/json", None, None)

    def get_data_type(self, id: str, domain: str) -> GetDataTypeResponse:
        """
        Get information about a Dataset's type.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/type"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeResponse, "GET", __url, "application/json", None, None)

    def put_values(self, id: str, domain: str, body: object) -> None:
        """
        Write values to Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(type(None), "PUT", __url, None, "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_values_as_stream(self, id: str, domain: str, select: Optional[str] = None, query: Optional[str] = None, limit: Optional[float] = None) -> Response:
        """
        Get values from Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
            select: URL-encoded string representing a selection array.
            query: URL-encoded string of conditional expression to filter selection.
            limit: Integer greater than zero.
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if select is not None:
            __query_values["select"] = quote(_to_string(select), safe="")

        if query is not None:
            __query_values["query"] = quote(_to_string(query), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(Response, "GET", __url, "application/octet-stream", None, None)

    def get_values_as_json(self, id: str, domain: str, select: Optional[str] = None, query: Optional[str] = None, limit: Optional[float] = None) -> GetValuesAsJsonResponse:
        """
        Get values from Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
            select: URL-encoded string representing a selection array.
            query: URL-encoded string of conditional expression to filter selection.
            limit: Integer greater than zero.
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if select is not None:
            __query_values["select"] = quote(_to_string(select), safe="")

        if query is not None:
            __query_values["query"] = quote(_to_string(query), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetValuesAsJsonResponse, "GET", __url, "application/json", None, None)

    def post_values_as_json(self, id: str, domain: str, body: object) -> PostValuesAsJsonResponse:
        """
        Get specific data points from Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostValuesAsJsonResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def post_values_as_stream(self, id: str, domain: str, body: object) -> Response:
        """
        Get specific data points from Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(Response, "POST", __url, "application/octet-stream", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attributes(self, collection: str, obj_uuid: str, domain: str, limit: Optional[float] = None, marker: Optional[str] = None) -> GetAttributesResponse:
        """
        List all Attributes attached to the HDF5 object `obj_uuid`.

        Args:
            collection: The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).
            obj_uuid: UUID of object.
            domain: 
            limit: Cap the number of Attributes listed.
            marker: Start Attribute listing _after_ the given name.
        """

        __url = "/{collection}/{obj_uuid}/attributes"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str, body: object) -> PutAttributeResponse:
        """
        Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.

        Args:
            domain: 
            collection: The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).
            obj_uuid: HDF5 object's UUID.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str) -> AttributeType:
        """
        Get information about an Attribute.

        Args:
            domain: 
            collection: Collection of object (Group, Dataset, or Datatype).
            obj_uuid: UUID of object.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(AttributeType, "GET", __url, "application/json", None, None)

    def get_dataset_access_lists(self, id: str, domain: str) -> GetDatasetAccessListsResponse:
        """
        Get access lists on Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetAccessListsResponse, "GET", __url, "application/json", None, None)


class DatatypeClient:
    """Provides methods to interact with datatype."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def post_data_type(self, domain: str, body: object) -> PostDataTypeResponse:
        """
        Commit a Datatype to the Domain.

        Args:
            domain: 
        """

        __url = "/datatypes"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDataTypeResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datatype(self, domain: str, id: str) -> GetDatatypeResponse:
        """
        Get information about a committed Datatype

        Args:
            domain: 
            id: UUID of the committed datatype.
        """

        __url = "/datatypes/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatatypeResponse, "GET", __url, "application/json", None, None)

    def delete_datatype(self, domain: str, id: str) -> DeleteDatatypeResponse:
        """
        Delete a committed Datatype.

        Args:
            domain: 
            id: UUID of the committed datatype.
        """

        __url = "/datatypes/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDatatypeResponse, "DELETE", __url, "application/json", None, None)

    def get_attributes(self, collection: str, obj_uuid: str, domain: str, limit: Optional[float] = None, marker: Optional[str] = None) -> GetAttributesResponse:
        """
        List all Attributes attached to the HDF5 object `obj_uuid`.

        Args:
            collection: The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).
            obj_uuid: UUID of object.
            domain: 
            limit: Cap the number of Attributes listed.
            marker: Start Attribute listing _after_ the given name.
        """

        __url = "/{collection}/{obj_uuid}/attributes"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str, body: object) -> PutAttributeResponse:
        """
        Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.

        Args:
            domain: 
            collection: The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).
            obj_uuid: HDF5 object's UUID.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str) -> AttributeType:
        """
        Get information about an Attribute.

        Args:
            domain: 
            collection: Collection of object (Group, Dataset, or Datatype).
            obj_uuid: UUID of object.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(AttributeType, "GET", __url, "application/json", None, None)

    def get_data_type_access_lists(self, id: str, domain: str) -> GetDataTypeAccessListsResponse:
        """
        List access lists on Datatype.

        Args:
            id: UUID of the committed datatype.
            domain: 
        """

        __url = "/datatypes/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeAccessListsResponse, "GET", __url, "application/json", None, None)


class AttributeClient:
    """Provides methods to interact with attribute."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def get_attributes(self, collection: str, obj_uuid: str, domain: str, limit: Optional[float] = None, marker: Optional[str] = None) -> GetAttributesResponse:
        """
        List all Attributes attached to the HDF5 object `obj_uuid`.

        Args:
            collection: The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).
            obj_uuid: UUID of object.
            domain: 
            limit: Cap the number of Attributes listed.
            marker: Start Attribute listing _after_ the given name.
        """

        __url = "/{collection}/{obj_uuid}/attributes"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str, body: object) -> PutAttributeResponse:
        """
        Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.

        Args:
            domain: 
            collection: The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).
            obj_uuid: HDF5 object's UUID.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str) -> AttributeType:
        """
        Get information about an Attribute.

        Args:
            domain: 
            collection: Collection of object (Group, Dataset, or Datatype).
            obj_uuid: UUID of object.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(AttributeType, "GET", __url, "application/json", None, None)


class ACLSClient:
    """Provides methods to interact with acls."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def get_access_lists(self, domain: str) -> GetAccessListsResponse:
        """
        Get access lists on Domain.

        Args:
            domain: 
        """

        __url = "/acls"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_user_access(self, domain: str, user: str) -> GetUserAccessResponse:
        """
        Get users's access to a Domain.

        Args:
            domain: 
            user: User identifier/name.
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetUserAccessResponse, "GET", __url, "application/json", None, None)

    def put_user_access(self, user: str, domain: str, body: object) -> PutUserAccessResponse:
        """
        Set user's access to the Domain.

        Args:
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutUserAccessResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_group_access_lists(self, id: str, domain: str) -> GetGroupAccessListsResponse:
        """
        List access lists on Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_group_user_access(self, id: str, user: str, domain: str) -> GetGroupUserAccessResponse:
        """
        Get users's access to a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/groups/{id}/acls/{user}"
        __url = __url.replace("{id}", quote(str(id), safe=""))
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupUserAccessResponse, "GET", __url, "application/json", None, None)

    def get_dataset_access_lists(self, id: str, domain: str) -> GetDatasetAccessListsResponse:
        """
        Get access lists on Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_data_type_access_lists(self, id: str, domain: str) -> GetDataTypeAccessListsResponse:
        """
        List access lists on Datatype.

        Args:
            id: UUID of the committed datatype.
            domain: 
        """

        __url = "/datatypes/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeAccessListsResponse, "GET", __url, "application/json", None, None)



class V2_0Async:
    """A client for version V2_0."""
    
    _domain: DomainAsyncClient
    _group: GroupAsyncClient
    _link: LinkAsyncClient
    _dataset: DatasetAsyncClient
    _datatype: DatatypeAsyncClient
    _attribute: AttributeAsyncClient
    _aCLS: ACLSAsyncClient


    def __init__(self, client: HsdsAsyncClient):
        """
        Initializes a new instance of V2_0Async
        
            Args:
                client: The client to use.
        """

        self._domain = DomainAsyncClient(client)
        self._group = GroupAsyncClient(client)
        self._link = LinkAsyncClient(client)
        self._dataset = DatasetAsyncClient(client)
        self._datatype = DatatypeAsyncClient(client)
        self._attribute = AttributeAsyncClient(client)
        self._aCLS = ACLSAsyncClient(client)


    @property
    def domain(self) -> DomainAsyncClient:
        """Gets the DomainAsyncClient."""
        return self._domain

    @property
    def group(self) -> GroupAsyncClient:
        """Gets the GroupAsyncClient."""
        return self._group

    @property
    def link(self) -> LinkAsyncClient:
        """Gets the LinkAsyncClient."""
        return self._link

    @property
    def dataset(self) -> DatasetAsyncClient:
        """Gets the DatasetAsyncClient."""
        return self._dataset

    @property
    def datatype(self) -> DatatypeAsyncClient:
        """Gets the DatatypeAsyncClient."""
        return self._datatype

    @property
    def attribute(self) -> AttributeAsyncClient:
        """Gets the AttributeAsyncClient."""
        return self._attribute

    @property
    def acls(self) -> ACLSAsyncClient:
        """Gets the ACLSAsyncClient."""
        return self._aCLS



class DomainAsyncClient:
    """Provides methods to interact with domain."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def put_domain(self, domain: str, body: Optional[object], folder: Optional[float] = None) -> Awaitable[PutDomainResponse]:
        """
        Create a new Domain on the service.

        Args:
            domain: 
            folder: If present and `1`, creates a Folder instead of a Domain.
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if folder is not None:
            __query_values["folder"] = quote(_to_string(folder), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutDomainResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_domain(self, domain: str) -> Awaitable[GetDomainResponse]:
        """
        Get information about the requested domain.

        Args:
            domain: 
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDomainResponse, "GET", __url, "application/json", None, None)

    def delete_domain(self, domain: str) -> Awaitable[DeleteDomainResponse]:
        """
        Delete the specified Domain or Folder.

        Args:
            domain: 
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDomainResponse, "DELETE", __url, "application/json", None, None)

    def post_group(self, domain: str, body: Optional[object]) -> Awaitable[PostGroupResponse]:
        """
        Create a new Group.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostGroupResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_groups(self, domain: str) -> Awaitable[GetGroupsResponse]:
        """
        Get UUIDs for all non-root Groups in Domain.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupsResponse, "GET", __url, "application/json", None, None)

    def post_dataset(self, domain: str, body: object) -> Awaitable[PostDatasetResponse]:
        """
        Create a Dataset.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDatasetResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datasets(self, domain: str) -> Awaitable[GetDatasetsResponse]:
        """
        List Datasets.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetsResponse, "GET", __url, "application/json", None, None)

    def post_data_type(self, domain: str, body: object) -> Awaitable[PostDataTypeResponse]:
        """
        Commit a Datatype to the Domain.

        Args:
            domain: 
        """

        __url = "/datatypes"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDataTypeResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_access_lists(self, domain: str) -> Awaitable[GetAccessListsResponse]:
        """
        Get access lists on Domain.

        Args:
            domain: 
        """

        __url = "/acls"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_user_access(self, domain: str, user: str) -> Awaitable[GetUserAccessResponse]:
        """
        Get users's access to a Domain.

        Args:
            domain: 
            user: User identifier/name.
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetUserAccessResponse, "GET", __url, "application/json", None, None)

    def put_user_access(self, user: str, domain: str, body: object) -> Awaitable[PutUserAccessResponse]:
        """
        Set user's access to the Domain.

        Args:
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutUserAccessResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))


class GroupAsyncClient:
    """Provides methods to interact with group."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def post_group(self, domain: str, body: Optional[object]) -> Awaitable[PostGroupResponse]:
        """
        Create a new Group.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostGroupResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_groups(self, domain: str) -> Awaitable[GetGroupsResponse]:
        """
        Get UUIDs for all non-root Groups in Domain.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupsResponse, "GET", __url, "application/json", None, None)

    def get_group(self, id: str, domain: str, getalias: Optional[int] = None) -> Awaitable[GetGroupResponse]:
        """
        Get information about a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
            getalias: 
        """

        __url = "/groups/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if getalias is not None:
            __query_values["getalias"] = quote(_to_string(getalias), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupResponse, "GET", __url, "application/json", None, None)

    def delete_group(self, id: str, domain: str) -> Awaitable[DeleteGroupResponse]:
        """
        Delete a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteGroupResponse, "DELETE", __url, "application/json", None, None)

    def get_attributes(self, collection: str, obj_uuid: str, domain: str, limit: Optional[float] = None, marker: Optional[str] = None) -> Awaitable[GetAttributesResponse]:
        """
        List all Attributes attached to the HDF5 object `obj_uuid`.

        Args:
            collection: The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).
            obj_uuid: UUID of object.
            domain: 
            limit: Cap the number of Attributes listed.
            marker: Start Attribute listing _after_ the given name.
        """

        __url = "/{collection}/{obj_uuid}/attributes"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str, body: object) -> Awaitable[PutAttributeResponse]:
        """
        Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.

        Args:
            domain: 
            collection: The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).
            obj_uuid: HDF5 object's UUID.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str) -> Awaitable[AttributeType]:
        """
        Get information about an Attribute.

        Args:
            domain: 
            collection: Collection of object (Group, Dataset, or Datatype).
            obj_uuid: UUID of object.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(AttributeType, "GET", __url, "application/json", None, None)

    def get_group_access_lists(self, id: str, domain: str) -> Awaitable[GetGroupAccessListsResponse]:
        """
        List access lists on Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_group_user_access(self, id: str, user: str, domain: str) -> Awaitable[GetGroupUserAccessResponse]:
        """
        Get users's access to a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/groups/{id}/acls/{user}"
        __url = __url.replace("{id}", quote(str(id), safe=""))
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupUserAccessResponse, "GET", __url, "application/json", None, None)


class LinkAsyncClient:
    """Provides methods to interact with link."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def get_links(self, id: str, domain: str, limit: Optional[float] = None, marker: Optional[str] = None) -> Awaitable[GetLinksResponse]:
        """
        List all Links in a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
            limit: Cap the number of Links returned in list.
            marker: Title of a Link; the first Link name to list.
        """

        __url = "/groups/{id}/links"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetLinksResponse, "GET", __url, "application/json", None, None)

    def put_link(self, id: str, linkname: str, domain: str, body: object) -> Awaitable[PutLinkResponse]:
        """
        Create a new Link in a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            linkname: 
            domain: 
        """

        __url = "/groups/{id}/links/{linkname}"
        __url = __url.replace("{id}", quote(str(id), safe=""))
        __url = __url.replace("{linkname}", quote(str(linkname), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutLinkResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_link(self, id: str, linkname: str, domain: str) -> Awaitable[GetLinkResponse]:
        """
        Get Link info.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            linkname: 
            domain: 
        """

        __url = "/groups/{id}/links/{linkname}"
        __url = __url.replace("{id}", quote(str(id), safe=""))
        __url = __url.replace("{linkname}", quote(str(linkname), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetLinkResponse, "GET", __url, "application/json", None, None)

    def delete_link(self, id: str, linkname: str, domain: str) -> Awaitable[DeleteLinkResponse]:
        """
        Delete Link.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            linkname: 
            domain: 
        """

        __url = "/groups/{id}/links/{linkname}"
        __url = __url.replace("{id}", quote(str(id), safe=""))
        __url = __url.replace("{linkname}", quote(str(linkname), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteLinkResponse, "DELETE", __url, "application/json", None, None)


class DatasetAsyncClient:
    """Provides methods to interact with dataset."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def post_dataset(self, domain: str, body: object) -> Awaitable[PostDatasetResponse]:
        """
        Create a Dataset.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDatasetResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datasets(self, domain: str) -> Awaitable[GetDatasetsResponse]:
        """
        List Datasets.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetsResponse, "GET", __url, "application/json", None, None)

    def get_dataset(self, id: str, domain: str) -> Awaitable[GetDatasetResponse]:
        """
        Get information about a Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetResponse, "GET", __url, "application/json", None, None)

    def delete_dataset(self, id: str, domain: str) -> Awaitable[DeleteDatasetResponse]:
        """
        Delete a Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDatasetResponse, "DELETE", __url, "application/json", None, None)

    def put_shape(self, id: str, domain: str, body: object) -> Awaitable[PutShapeResponse]:
        """
        Modify a Dataset's dimensions.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/shape"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutShapeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_shape(self, id: str, domain: str) -> Awaitable[GetShapeResponse]:
        """
        Get information about a Dataset's shape.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/shape"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetShapeResponse, "GET", __url, "application/json", None, None)

    def get_data_type(self, id: str, domain: str) -> Awaitable[GetDataTypeResponse]:
        """
        Get information about a Dataset's type.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/type"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeResponse, "GET", __url, "application/json", None, None)

    def put_values(self, id: str, domain: str, body: object) -> Awaitable[None]:
        """
        Write values to Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(type(None), "PUT", __url, None, "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_values_as_stream(self, id: str, domain: str, select: Optional[str] = None, query: Optional[str] = None, limit: Optional[float] = None) -> Awaitable[Response]:
        """
        Get values from Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
            select: URL-encoded string representing a selection array.
            query: URL-encoded string of conditional expression to filter selection.
            limit: Integer greater than zero.
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if select is not None:
            __query_values["select"] = quote(_to_string(select), safe="")

        if query is not None:
            __query_values["query"] = quote(_to_string(query), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(Response, "GET", __url, "application/octet-stream", None, None)

    def get_values_as_json(self, id: str, domain: str, select: Optional[str] = None, query: Optional[str] = None, limit: Optional[float] = None) -> Awaitable[GetValuesAsJsonResponse]:
        """
        Get values from Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
            select: URL-encoded string representing a selection array.
            query: URL-encoded string of conditional expression to filter selection.
            limit: Integer greater than zero.
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if select is not None:
            __query_values["select"] = quote(_to_string(select), safe="")

        if query is not None:
            __query_values["query"] = quote(_to_string(query), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetValuesAsJsonResponse, "GET", __url, "application/json", None, None)

    def post_values_as_json(self, id: str, domain: str, body: object) -> Awaitable[PostValuesAsJsonResponse]:
        """
        Get specific data points from Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostValuesAsJsonResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def post_values_as_stream(self, id: str, domain: str, body: object) -> Awaitable[Response]:
        """
        Get specific data points from Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(Response, "POST", __url, "application/octet-stream", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attributes(self, collection: str, obj_uuid: str, domain: str, limit: Optional[float] = None, marker: Optional[str] = None) -> Awaitable[GetAttributesResponse]:
        """
        List all Attributes attached to the HDF5 object `obj_uuid`.

        Args:
            collection: The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).
            obj_uuid: UUID of object.
            domain: 
            limit: Cap the number of Attributes listed.
            marker: Start Attribute listing _after_ the given name.
        """

        __url = "/{collection}/{obj_uuid}/attributes"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str, body: object) -> Awaitable[PutAttributeResponse]:
        """
        Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.

        Args:
            domain: 
            collection: The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).
            obj_uuid: HDF5 object's UUID.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str) -> Awaitable[AttributeType]:
        """
        Get information about an Attribute.

        Args:
            domain: 
            collection: Collection of object (Group, Dataset, or Datatype).
            obj_uuid: UUID of object.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(AttributeType, "GET", __url, "application/json", None, None)

    def get_dataset_access_lists(self, id: str, domain: str) -> Awaitable[GetDatasetAccessListsResponse]:
        """
        Get access lists on Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetAccessListsResponse, "GET", __url, "application/json", None, None)


class DatatypeAsyncClient:
    """Provides methods to interact with datatype."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def post_data_type(self, domain: str, body: object) -> Awaitable[PostDataTypeResponse]:
        """
        Commit a Datatype to the Domain.

        Args:
            domain: 
        """

        __url = "/datatypes"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDataTypeResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datatype(self, domain: str, id: str) -> Awaitable[GetDatatypeResponse]:
        """
        Get information about a committed Datatype

        Args:
            domain: 
            id: UUID of the committed datatype.
        """

        __url = "/datatypes/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatatypeResponse, "GET", __url, "application/json", None, None)

    def delete_datatype(self, domain: str, id: str) -> Awaitable[DeleteDatatypeResponse]:
        """
        Delete a committed Datatype.

        Args:
            domain: 
            id: UUID of the committed datatype.
        """

        __url = "/datatypes/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDatatypeResponse, "DELETE", __url, "application/json", None, None)

    def get_attributes(self, collection: str, obj_uuid: str, domain: str, limit: Optional[float] = None, marker: Optional[str] = None) -> Awaitable[GetAttributesResponse]:
        """
        List all Attributes attached to the HDF5 object `obj_uuid`.

        Args:
            collection: The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).
            obj_uuid: UUID of object.
            domain: 
            limit: Cap the number of Attributes listed.
            marker: Start Attribute listing _after_ the given name.
        """

        __url = "/{collection}/{obj_uuid}/attributes"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str, body: object) -> Awaitable[PutAttributeResponse]:
        """
        Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.

        Args:
            domain: 
            collection: The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).
            obj_uuid: HDF5 object's UUID.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str) -> Awaitable[AttributeType]:
        """
        Get information about an Attribute.

        Args:
            domain: 
            collection: Collection of object (Group, Dataset, or Datatype).
            obj_uuid: UUID of object.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(AttributeType, "GET", __url, "application/json", None, None)

    def get_data_type_access_lists(self, id: str, domain: str) -> Awaitable[GetDataTypeAccessListsResponse]:
        """
        List access lists on Datatype.

        Args:
            id: UUID of the committed datatype.
            domain: 
        """

        __url = "/datatypes/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeAccessListsResponse, "GET", __url, "application/json", None, None)


class AttributeAsyncClient:
    """Provides methods to interact with attribute."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def get_attributes(self, collection: str, obj_uuid: str, domain: str, limit: Optional[float] = None, marker: Optional[str] = None) -> Awaitable[GetAttributesResponse]:
        """
        List all Attributes attached to the HDF5 object `obj_uuid`.

        Args:
            collection: The collection of the HDF5 object (one of: `groups`, `datasets`, or `datatypes`).
            obj_uuid: UUID of object.
            domain: 
            limit: Cap the number of Attributes listed.
            marker: Start Attribute listing _after_ the given name.
        """

        __url = "/{collection}/{obj_uuid}/attributes"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str, body: object) -> Awaitable[PutAttributeResponse]:
        """
        Create an attribute with name `attr` and assign it to HDF5 object `obj_uudi`.

        Args:
            domain: 
            collection: The collection of the HDF5 object (`groups`, `datasets`, or `datatypes`).
            obj_uuid: HDF5 object's UUID.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, domain: str, collection: str, obj_uuid: str, attr: str) -> Awaitable[AttributeType]:
        """
        Get information about an Attribute.

        Args:
            domain: 
            collection: Collection of object (Group, Dataset, or Datatype).
            obj_uuid: UUID of object.
            attr: Name of attribute.
        """

        __url = "/{collection}/{obj_uuid}/attributes/{attr}"
        __url = __url.replace("{collection}", quote(str(collection), safe=""))
        __url = __url.replace("{obj_uuid}", quote(str(obj_uuid), safe=""))
        __url = __url.replace("{attr}", quote(str(attr), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(AttributeType, "GET", __url, "application/json", None, None)


class ACLSAsyncClient:
    """Provides methods to interact with acls."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def get_access_lists(self, domain: str) -> Awaitable[GetAccessListsResponse]:
        """
        Get access lists on Domain.

        Args:
            domain: 
        """

        __url = "/acls"

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_user_access(self, domain: str, user: str) -> Awaitable[GetUserAccessResponse]:
        """
        Get users's access to a Domain.

        Args:
            domain: 
            user: User identifier/name.
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetUserAccessResponse, "GET", __url, "application/json", None, None)

    def put_user_access(self, user: str, domain: str, body: object) -> Awaitable[PutUserAccessResponse]:
        """
        Set user's access to the Domain.

        Args:
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutUserAccessResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_group_access_lists(self, id: str, domain: str) -> Awaitable[GetGroupAccessListsResponse]:
        """
        List access lists on Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_group_user_access(self, id: str, user: str, domain: str) -> Awaitable[GetGroupUserAccessResponse]:
        """
        Get users's access to a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/groups/{id}/acls/{user}"
        __url = __url.replace("{id}", quote(str(id), safe=""))
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupUserAccessResponse, "GET", __url, "application/json", None, None)

    def get_dataset_access_lists(self, id: str, domain: str) -> Awaitable[GetDatasetAccessListsResponse]:
        """
        Get access lists on Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_data_type_access_lists(self, id: str, domain: str) -> Awaitable[GetDataTypeAccessListsResponse]:
        """
        List access lists on Datatype.

        Args:
            id: UUID of the committed datatype.
            domain: 
        """

        __url = "/datatypes/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeAccessListsResponse, "GET", __url, "application/json", None, None)



@dataclass(frozen=True)
class ACL:
    """
    Access Control List for a single user.

    Args:
        username: 
    """

    username: ACLUsernameType
    """"""


@dataclass(frozen=True)
class ACLS:
    """
    Access Control Lists for users.

    Args:
        for_whom: Access Control List for a single user.
    """

    for_whom: ACL
    """Access Control List for a single user."""


@dataclass(frozen=True)
class HrefType:
    """
    A href.

    Args:
        href: URL of the resource.
        rel: Relation to this object.
    """

    href: str
    """URL of the resource."""

    rel: str
    """Relation to this object."""


@dataclass(frozen=True)
class ShapeType:
    """
    A shape.

    Args:
        class: The shape class.
        dims: The shape dimensions.
        maxdims: The shape maximum dimensions.
    """

    class_: str
    """The shape class."""

    dims: Optional[list[int]]
    """The shape dimensions."""

    maxdims: Optional[list[float]]
    """The shape maximum dimensions."""


@dataclass(frozen=True)
class TypeType:
    """
    A type.

    Args:
        class: The type class.
        base: The base type class.
        fields: List of fields in a compound dataset.
    """

    class_: str
    """The type class."""

    base: Optional[str]
    """The base type class."""

    fields: Optional[list[TypeTypeFieldsType]]
    """List of fields in a compound dataset."""


@dataclass(frozen=True)
class LayoutType:
    """
    A layout.

    Args:
        class: The layout class.
        dims: The chunk dimensions.
    """

    class_: str
    """The layout class."""

    dims: Optional[list[int]]
    """The chunk dimensions."""


@dataclass(frozen=True)
class AttributeType:
    """
    An attribute.

    Args:
        created: The creation date.
        last_modified: The date of last modification.
        name: The name.
        shape: The shape.
        type: The type.
        value: The values.
        href: Link to the attribute.
        hrefs: A collection of relations.
    """

    created: float
    """The creation date."""

    last_modified: Optional[float]
    """The date of last modification."""

    name: str
    """The name."""

    shape: ShapeType
    """The shape."""

    type: TypeType
    """The type."""

    value: Optional[object]
    """The values."""

    href: Optional[str]
    """Link to the attribute."""

    hrefs: Optional[list[HrefType]]
    """A collection of relations."""


@dataclass(frozen=True)
class PutDomainResponse:
    """
    

    Args:
        acls: Access Control Lists for users.
        created: When domain was created.
        last_modified: When object was last modified.
        owner: Name of owner.
        root: ID of root group.
    """

    acls: ACLS
    """Access Control Lists for users."""

    created: float
    """When domain was created."""

    last_modified: float
    """When object was last modified."""

    owner: str
    """Name of owner."""

    root: str
    """ID of root group."""


@dataclass(frozen=True)
class GetDomainResponseHrefsType:
    """
    

    Args:
        href: URL to reference.
        rel: Relation to this Domain.
    """

    href: str
    """URL to reference."""

    rel: str
    """Relation to this Domain."""


@dataclass(frozen=True)
class GetDomainResponse:
    """
    

    Args:
        root: UUID of root Group. If Domain is of class 'folder', this entry is not present.
        owner: 
        class: Category of Domain. If 'folder' no root group is included in response.
        created: 
        last_modified: 
        hrefs: Array of url references and their relation to this Domain. Should include entries for: `acls`, `database` (if not class is not `folder`), `groupbase` (if not class is not `folder`), `parent`, `root` (if not class is not `folder`), `self`, `typebase` (if not class is not `folder`).
    """

    root: str
    """UUID of root Group. If Domain is of class 'folder', this entry is not present.
"""

    owner: str
    """"""

    class_: str
    """Category of Domain. If 'folder' no root group is included in response.
"""

    created: float
    """"""

    last_modified: float
    """"""

    hrefs: list[GetDomainResponseHrefsType]
    """Array of url references and their relation to this Domain. Should include entries for: `acls`, `database` (if not class is not `folder`), `groupbase` (if not class is not `folder`), `parent`, `root` (if not class is not `folder`), `self`, `typebase` (if not class is not `folder`).
"""


@dataclass(frozen=True)
class DeleteDomainResponse:
    """
    The Domain or Folder which was deleted.

    Args:
        domain: domain path
    """

    domain: str
    """domain path"""


@dataclass(frozen=True)
class PostGroupResponse:
    """
    

    Args:
        id: UUID of new Group.
        root: UUID of root Group in Domain.
        last_modified: 
        created: 
        attribute_count: 
        link_count: 
    """

    id: str
    """UUID of new Group."""

    root: str
    """UUID of root Group in Domain."""

    last_modified: float
    """"""

    created: float
    """"""

    attribute_count: float
    """"""

    link_count: float
    """"""


@dataclass(frozen=True)
class GetGroupsResponseHrefsType:
    """
    References to other objects.

    Args:
        href: URL reference.
        rel: Relation to this object.
    """

    href: str
    """URL reference."""

    rel: str
    """Relation to this object."""


@dataclass(frozen=True)
class GetGroupsResponse:
    """
    

    Args:
        groups: 
        hrefs: 
    """

    groups: list[str]
    """"""

    hrefs: list[GetGroupsResponseHrefsType]
    """"""


@dataclass(frozen=True)
class PostDatasetResponseTypeType:
    """
    (See `GET /datasets/{id}`)

    Args:
    """


@dataclass(frozen=True)
class PostDatasetResponseShapeType:
    """
    (See `GET /datasets/{id}`)

    Args:
    """


@dataclass(frozen=True)
class PostDatasetResponse:
    """
    

    Args:
        id: UUID of this Dataset.
        root: UUID of root Group in Domain.
        created: 
        last_modified: 
        attribute_count: 
        type: (See `GET /datasets/{id}`)
        shape: (See `GET /datasets/{id}`)
    """

    id: str
    """UUID of this Dataset."""

    root: str
    """UUID of root Group in Domain."""

    created: float
    """"""

    last_modified: float
    """"""

    attribute_count: float
    """"""

    type: PostDatasetResponseTypeType
    """(See `GET /datasets/{id}`)"""

    shape: PostDatasetResponseShapeType
    """(See `GET /datasets/{id}`)"""


@dataclass(frozen=True)
class GetDatasetsResponseHrefsType:
    """
    

    Args:
        href: URL reference.
        rel: Relation to this object.
    """

    href: str
    """URL reference."""

    rel: str
    """Relation to this object."""


@dataclass(frozen=True)
class GetDatasetsResponse:
    """
    

    Args:
        datasets: 
        hrefs: List of references to other objects.
    """

    datasets: list[str]
    """"""

    hrefs: list[GetDatasetsResponseHrefsType]
    """List of references to other objects.
Should contain references for: `attributes`, `data`, `home`, `root`, `self`
"""


@dataclass(frozen=True)
class PostDataTypeResponse:
    """
    TODO

    Args:
        attribute_count: 
        id: 
    """

    attribute_count: float
    """"""

    id: str
    """"""


@dataclass(frozen=True)
class GetAccessListsResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: relation to this object
    """

    href: str
    """URL of resource"""

    rel: str
    """relation to this object"""


@dataclass(frozen=True)
class GetAccessListsResponse:
    """
    TODO

    Args:
        acls: Access Control Lists for users.
        hrefs: 
    """

    acls: ACLS
    """Access Control Lists for users."""

    hrefs: list[GetAccessListsResponseHrefsType]
    """"""


@dataclass(frozen=True)
class GetUserAccessResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: relation to this object
    """

    href: str
    """URL of resource"""

    rel: str
    """relation to this object"""


@dataclass(frozen=True)
class GetUserAccessResponse:
    """
    TODO

    Args:
        acl: Access Control List for a single user.
        hrefs: 
    """

    acl: ACL
    """Access Control List for a single user."""

    hrefs: list[GetUserAccessResponseHrefsType]
    """"""


@dataclass(frozen=True)
class PutUserAccessResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: relation to this object
    """

    href: str
    """URL of resource"""

    rel: str
    """relation to this object"""


@dataclass(frozen=True)
class PutUserAccessResponse:
    """
    TODO

    Args:
        acl: Access Control List for a single user.
        hrefs: 
    """

    acl: ACL
    """Access Control List for a single user."""

    hrefs: list[PutUserAccessResponseHrefsType]
    """"""


@dataclass(frozen=True)
class GetGroupResponseHrefsType:
    """
    References to other objects.

    Args:
        rel: Relation to this object.
        href: URL to reference.
    """

    rel: str
    """Relation to this object."""

    href: str
    """URL to reference."""


@dataclass(frozen=True)
class GetGroupResponse:
    """
    

    Args:
        id: UUID of this Group.
        root: UUID of root Group.
        alias: List of aliases for the Group, as reached by _hard_ Links. If Group is unlinked, its alias list will be empty (`[]`).
        created: 
        last_modified: 
        domain: 
        attribute_count: 
        link_count: 
        hrefs: List of references to other objects.
    """

    id: str
    """UUID of this Group."""

    root: str
    """UUID of root Group."""

    alias: list[str]
    """List of aliases for the Group, as reached by _hard_ Links. If Group is unlinked, its alias list will be empty (`[]`).
Only present if `alias=1` is present as query parameter.
"""

    created: float
    """"""

    last_modified: float
    """"""

    domain: str
    """"""

    attribute_count: float
    """"""

    link_count: float
    """"""

    hrefs: list[GetGroupResponseHrefsType]
    """List of references to other objects."""


@dataclass(frozen=True)
class DeleteGroupResponse:
    """
    

    Args:
    """


@dataclass(frozen=True)
class GetAttributesResponse:
    """
    A list of attributes.

    Args:
        attributes: 
        hrefs: A collection of relations.
    """

    attributes: list[AttributeType]
    """"""

    hrefs: list[HrefType]
    """A collection of relations."""


@dataclass(frozen=True)
class PutAttributeResponse:
    """
    TODO

    Args:
    """


@dataclass(frozen=True)
class GetGroupAccessListsResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: relation to this object
    """

    href: str
    """URL of resource"""

    rel: str
    """relation to this object"""


@dataclass(frozen=True)
class GetGroupAccessListsResponse:
    """
    TODO

    Args:
        acls: Access Control Lists for users.
        hrefs: 
    """

    acls: ACLS
    """Access Control Lists for users."""

    hrefs: list[GetGroupAccessListsResponseHrefsType]
    """"""


@dataclass(frozen=True)
class GetGroupUserAccessResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: relation to this object
    """

    href: str
    """URL of resource"""

    rel: str
    """relation to this object"""


@dataclass(frozen=True)
class GetGroupUserAccessResponse:
    """
    TODO

    Args:
        acl: Access Control List for a single user.
        hrefs: 
    """

    acl: ACL
    """Access Control List for a single user."""

    hrefs: list[GetGroupUserAccessResponseHrefsType]
    """"""


@dataclass(frozen=True)
class GetLinksResponseLinksType:
    """
    

    Args:
        id: UUID of Link target.
        created: 
        class: Indicate whether this Link is hard, soft, or external.
        title: Name/label/title of the Link, as provided upon creation.
        target: URL of Link target.
        href: URL to origin of Link.
        collection: What kind of object is the target. (TODO)
    """

    id: str
    """UUID of Link target."""

    created: float
    """"""

    class_: str
    """Indicate whether this Link is hard, soft, or external.
"""

    title: str
    """Name/label/title of the Link, as provided upon creation.
"""

    target: str
    """URL of Link target."""

    href: str
    """URL to origin of Link."""

    collection: str
    """What kind of object is the target. (TODO)
"""


@dataclass(frozen=True)
class GetLinksResponseHrefsType:
    """
    

    Args:
        rel: Relation to this object.
        href: URL to reference.
    """

    rel: str
    """Relation to this object."""

    href: str
    """URL to reference."""


@dataclass(frozen=True)
class GetLinksResponse:
    """
    

    Args:
        links: 
        hrefs: List of references to other entities.
    """

    links: list[GetLinksResponseLinksType]
    """"""

    hrefs: list[GetLinksResponseHrefsType]
    """List of references to other entities.
Should contain references for: `home`, `owner`, `self`.
"""


@dataclass(frozen=True)
class PutLinkResponse:
    """
    Always returns `{"hrefs": []}`.

    Args:
    """


@dataclass(frozen=True)
class GetLinkResponseLinkType:
    """
    

    Args:
        id: 
        title: 
        collection: 
        class: 
    """

    id: str
    """"""

    title: str
    """"""

    collection: str
    """"""

    class_: str
    """"""


@dataclass(frozen=True)
class GetLinkResponseHrefsType:
    """
    

    Args:
        href: URL to reference.
        rel: Relation to this object.
    """

    href: str
    """URL to reference."""

    rel: str
    """Relation to this object."""


@dataclass(frozen=True)
class GetLinkResponse:
    """
    

    Args:
        last_modified: 
        created: 
        link: 
        hrefs: List of references to other entities.
    """

    last_modified: float
    """"""

    created: float
    """"""

    link: GetLinkResponseLinkType
    """"""

    hrefs: list[GetLinkResponseHrefsType]
    """List of references to other entities.
Should contain references for: `home`, `owner`, `self`, `target`,
"""


@dataclass(frozen=True)
class DeleteLinkResponse:
    """
    Always returns `{"hrefs": []}`.

    Args:
    """


@dataclass(frozen=True)
class GetDatasetResponseCreationPropertiesType:
    """
    Dataset creation properties as provided upon creation.

    Args:
    """


@dataclass(frozen=True)
class GetDatasetResponse:
    """
    

    Args:
        id: UUID of this Dataset.
        root: UUID of root Group in Domain.
        domain: The domain name.
        created: The creation date.
        last_modified: The date of the last modification.
        attribute_count: The number of attributes.
        type: The type.
        shape: The shape.
        layout: The layout.
        creation_properties: Dataset creation properties as provided upon creation.
        hrefs: A collection of relations.
    """

    id: str
    """UUID of this Dataset."""

    root: str
    """UUID of root Group in Domain."""

    domain: str
    """The domain name."""

    created: float
    """The creation date."""

    last_modified: float
    """The date of the last modification."""

    attribute_count: float
    """The number of attributes."""

    type: TypeType
    """The type."""

    shape: ShapeType
    """The shape."""

    layout: LayoutType
    """The layout."""

    creation_properties: GetDatasetResponseCreationPropertiesType
    """Dataset creation properties as provided upon creation.
"""

    hrefs: list[HrefType]
    """A collection of relations."""


@dataclass(frozen=True)
class DeleteDatasetResponse:
    """
    

    Args:
    """


@dataclass(frozen=True)
class PutShapeResponse:
    """
    

    Args:
        hrefs: 
    """

    hrefs: list[str]
    """"""


@dataclass(frozen=True)
class GetShapeResponseShapeType:
    """
    

    Args:
    """


@dataclass(frozen=True)
class GetShapeResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: relation to this object
    """

    href: str
    """URL of resource"""

    rel: str
    """relation to this object"""


@dataclass(frozen=True)
class GetShapeResponse:
    """
    (See `GET /datasets/{id}`)

    Args:
        created: 
        last_modified: 
        shape: 
        hrefs: Must include references to only: `owner`, `root`, `self`.
    """

    created: float
    """"""

    last_modified: float
    """"""

    shape: GetShapeResponseShapeType
    """"""

    hrefs: list[GetShapeResponseHrefsType]
    """Must include references to only: `owner`, `root`, `self`.
"""


@dataclass(frozen=True)
class GetDataTypeResponseTypeType:
    """
    

    Args:
    """


@dataclass(frozen=True)
class GetDataTypeResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: relation to this object
    """

    href: str
    """URL of resource"""

    rel: str
    """relation to this object"""


@dataclass(frozen=True)
class GetDataTypeResponse:
    """
    (See `GET /datasets/{id}`)

    Args:
        type: 
        hrefs: 
    """

    type: GetDataTypeResponseTypeType
    """"""

    hrefs: list[GetDataTypeResponseHrefsType]
    """"""


@dataclass(frozen=True)
class GetValuesAsJsonResponse:
    """
    

    Args:
        index: List of indices (TODO: coordinates?) corresponding with each value returned. i.e., `index[i]` is the coordinate of `value[i]`.
        value: 
    """

    index: list[str]
    """List of indices (TODO: coordinates?) corresponding with each value returned. i.e., `index[i]` is the coordinate of `value[i]`.
Only present if `query` parameter is part of the request URI.
"""

    value: list[object]
    """"""


@dataclass(frozen=True)
class PostValuesAsJsonResponse:
    """
    

    Args:
        value: 
    """

    value: list[object]
    """"""


@dataclass(frozen=True)
class GetDatasetAccessListsResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: relation to this object
    """

    href: str
    """URL of resource"""

    rel: str
    """relation to this object"""


@dataclass(frozen=True)
class GetDatasetAccessListsResponse:
    """
    TODO

    Args:
        acls: Access Control Lists for users.
        hrefs: 
    """

    acls: ACLS
    """Access Control Lists for users."""

    hrefs: list[GetDatasetAccessListsResponseHrefsType]
    """"""


@dataclass(frozen=True)
class GetDatatypeResponseTypeType:
    """
    

    Args:
    """


@dataclass(frozen=True)
class GetDatatypeResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: relation to this object
    """

    href: str
    """URL of resource"""

    rel: str
    """relation to this object"""


@dataclass(frozen=True)
class GetDatatypeResponse:
    """
    TODO

    Args:
        attribute_count: 
        created: 
        id: 
        last_modified: 
        root: 
        type: 
        hrefs: TODO
    """

    attribute_count: float
    """"""

    created: float
    """"""

    id: str
    """"""

    last_modified: float
    """"""

    root: str
    """"""

    type: GetDatatypeResponseTypeType
    """"""

    hrefs: list[GetDatatypeResponseHrefsType]
    """TODO"""


@dataclass(frozen=True)
class DeleteDatatypeResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: relation to this object
    """

    href: str
    """URL of resource"""

    rel: str
    """relation to this object"""


@dataclass(frozen=True)
class DeleteDatatypeResponse:
    """
    Always returns `{"hrefs": []}` (TODO confirm)

    Args:
        hrefs: 
    """

    hrefs: list[DeleteDatatypeResponseHrefsType]
    """"""


@dataclass(frozen=True)
class GetDataTypeAccessListsResponseHrefsType:
    """
    

    Args:
        href: URL of resource
        rel: Relation to `href`.
    """

    href: str
    """URL of resource"""

    rel: str
    """Relation to `href`."""


@dataclass(frozen=True)
class GetDataTypeAccessListsResponse:
    """
    TODO

    Args:
        acls: Access Control Lists for users.
        hrefs: 
    """

    acls: ACLS
    """Access Control Lists for users."""

    hrefs: list[GetDataTypeAccessListsResponseHrefsType]
    """"""


@dataclass(frozen=True)
class ACLUsernameType:
    """
    

    Args:
        create: 
        update: 
        delete: 
        update_acl: 
        read: 
        read_acl: 
    """

    create: bool
    """"""

    update: bool
    """"""

    delete: bool
    """"""

    update_acl: bool
    """"""

    read: bool
    """"""

    read_acl: bool
    """"""


@dataclass(frozen=True)
class TypeTypeFieldsType:
    """
    

    Args:
        name: Descriptive or identifying name.
        type: The type.
    """

    name: str
    """Descriptive or identifying name."""

    type: TypeType
    """The type."""



