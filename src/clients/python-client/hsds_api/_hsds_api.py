# pyright: reportPrivateUsage=false

# Python <= 3.9
from __future__ import annotations

import dataclasses
import re
import typing
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from enum import Enum
from typing import (Any, Callable, ClassVar, Optional, Type, Union,
                    cast)
from uuid import UUID

from typing import TypeVar

T = TypeVar("T")

@dataclass(frozen=True)
class JsonEncoderOptions:

    property_name_encoder: Callable[[str], str] = lambda value: value
    property_name_decoder: Callable[[str], str] = lambda value: value

    encoders: dict[Type, Callable[[Any], Any]] = field(default_factory=lambda: {
        datetime:   lambda value: value.isoformat().replace("+00:00", "Z"),
        timedelta:  lambda value: _encode_timedelta(value),
        Enum:       lambda value: value.name,
        UUID:       lambda value: str(value)
    })

    decoders: dict[Type, Callable[[Type, Any], Any]] = field(default_factory=lambda: {
        datetime:   lambda       _, value: datetime.fromisoformat((value[0:26] + value[26 + 1:]).replace("Z", "+00:00")),
        timedelta:  lambda       _, value: _decode_timedelta(value),
        Enum:       lambda typeCls, value: cast(Type[Enum], typeCls)[value],
        UUID:       lambda       _, value: UUID(value)
    })

class JsonEncoder:

    @staticmethod
    def encode(value: Any, options: Optional[JsonEncoderOptions] = None) -> Any:
        options = options if options is not None else JsonEncoderOptions()
        value = JsonEncoder._try_encode(value, options)         

        return value

    @staticmethod
    def _try_encode(value: Any, options: JsonEncoderOptions) -> Any:

        # None
        if value is None:
            return typing.cast(T, None)

        # list/tuple
        elif isinstance(value, list) or isinstance(value, tuple):
            value = [JsonEncoder._try_encode(current, options) for current in value]
        
        # dict
        elif isinstance(value, dict):
            value = {key:JsonEncoder._try_encode(current_value, options) for key, current_value in value.items()}

        elif dataclasses.is_dataclass(value):
            # dataclasses.asdict(value) would be good choice here, but it also converts nested dataclasses into
            # dicts, which prevents us to distinct between dict and dataclasses (important for property_name_encoder)
            value = {options.property_name_encoder(key):JsonEncoder._try_encode(value, options) for key, value in value.__dict__.items()}

        # registered encoders
        else:
            for base in value.__class__.__mro__[:-1]:
                encoder = options.encoders.get(base)

                if encoder is not None:
                    return encoder(value)

        return value

    @staticmethod
    def decode(type: Type[T], data: Any, options: Optional[JsonEncoderOptions] = None) -> T:
        options = options if options is not None else JsonEncoderOptions()
        return JsonEncoder._decode(type, data, options)

    @staticmethod
    def _decode(typeCls: Type[T], data: Any, options: JsonEncoderOptions) -> T:
       
        if data is None:
            return typing.cast(T, None)

        if typeCls == Any:
            return data

        origin = typing.cast(Type, typing.get_origin(typeCls))
        args = typing.get_args(typeCls)

        if origin is not None:

            # Optional
            if origin is Union and type(None) in args:

                baseType = args[0]
                instance3 = JsonEncoder._decode(baseType, data, options)

                return typing.cast(T, instance3)

            # list
            elif issubclass(origin, list):

                listType = args[0]
                instance1: list = list()

                for value in data:
                    instance1.append(JsonEncoder._decode(listType, value, options))

                return typing.cast(T, instance1)
            
            # dict
            elif issubclass(origin, dict):

                # keyType = args[0]
                valueType = args[1]

                instance2: dict = dict()

                for key, value in data.items():
                    instance2[key] = JsonEncoder._decode(valueType, value, options)

                return typing.cast(T, instance2)

            # default
            else:
                raise Exception(f"Type {str(origin)} cannot be decoded.")
        
        # dataclass
        elif dataclasses.is_dataclass(typeCls):

            parameters = {}
            type_hints = typing.get_type_hints(typeCls)

            for key, value in data.items():

                key = options.property_name_decoder(key)
                parameter_type = typing.cast(Type, type_hints.get(key))
                
                if (parameter_type is not None):
                    value = JsonEncoder._decode(parameter_type, value, options)
                    parameters[key] = value

            # ensure default values if JSON does not serialize default fields
            for key, value in type_hints.items():
                if not key in parameters and not typing.get_origin(value) == ClassVar:
                    
                    if (value == int):
                        parameters[key] = 0

                    elif (value == float):
                        parameters[key] = 0.0

                    else:
                        parameters[key] = None
              
            instance = typing.cast(T, typeCls(**parameters))

            return instance

        # registered decoders
        for base in typeCls.__mro__[:-1]:
            decoder = options.decoders.get(base)

            if decoder is not None:
                return decoder(typeCls, data)

        # default
        return data

# timespan is always serialized with 7 subsecond digits (https://github.com/dotnet/runtime/blob/a6cb7705bd5317ab5e9f718b55a82444156fc0c8/src/libraries/System.Text.Json/tests/System.Text.Json.Tests/Serialization/Value.WriteTests.cs#L178-L189)
def _encode_timedelta(value: timedelta):
    hours, remainder = divmod(value.seconds, 3600)
    minutes, seconds = divmod(remainder, 60)
    result = f"{int(value.days)}.{int(hours):02}:{int(minutes):02}:{int(seconds):02}.{value.microseconds:06d}0"
    return result

timedelta_pattern = re.compile(r"^(?:([0-9]+)\.)?([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.([0-9]+))?$")

def _decode_timedelta(value: str):
    # 12:08:07
    # 12:08:07.1250000
    # 3000.00:08:07
    # 3000.00:08:07.1250000
    match = timedelta_pattern.match(value)

    if match:
        days = int(match.group(1)) if match.group(1) else 0
        hours = int(match.group(2))
        minutes = int(match.group(3))
        seconds = int(match.group(4))
        microseconds = int(match.group(5)) / 10.0 if match.group(5) else 0

        return typing.cast(T, timedelta(days=days, hours=hours, minutes=minutes, seconds=seconds, microseconds=microseconds))

    else:
        raise Exception(f"Unable to decode {value} into value of type timedelta.")

def to_camel_case(value: str) -> str:
    components = value.split("_")
    return components[0] + ''.join(x.title() for x in components[1:])

snake_case_pattern = re.compile('((?<=[a-z0-9])[A-Z]|(?!^)[A-Z](?=[a-z]))')

def to_snake_case(value: str) -> str:
    return snake_case_pattern.sub(r'_\1', value).lower()


import json
from dataclasses import dataclass
from datetime import datetime, timedelta
from enum import Enum
from typing import (Any, AsyncIterable, Awaitable, Callable, Iterable,
                    Optional, Type, Union, cast)
from urllib.parse import quote
from uuid import UUID

from httpx import AsyncClient, Client, Request, Response

def _to_string(value: Any) -> str:

    if type(value) is datetime:
        return value.isoformat()

    elif type(value) is str:
        return value

    else:
        return str(value)

_json_encoder_options: JsonEncoderOptions = JsonEncoderOptions(
    property_name_encoder=lambda value: to_camel_case(value) if value != "class_" else "class",
    property_name_decoder=lambda value: to_snake_case(value) if value != "class" else "class_"
)

_json_encoder_options.encoders[Enum] = lambda value: to_camel_case(value.name)
_json_encoder_options.decoders[Enum] = lambda typeCls, value: cast(Type[Enum], typeCls)[to_snake_case(value).upper()]

class HsdsException(Exception):
    """A HsdsException."""

    def __init__(self, status_code: str, message: str):
        self.status_code = status_code
        self.message = message

    status_code: str
    """The exception status code."""

    message: str
    """The exception message."""

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
class GetAttributesResponseAttributesTypeShapeType:
    """
    

    Args:
    """


@dataclass(frozen=True)
class GetAttributesResponseAttributesTypeTypeType:
    """
    

    Args:
    """


@dataclass(frozen=True)
class GetAttributesResponseAttributesType:
    """
    

    Args:
        created: 
        href: 
        name: 
        shape: 
        type: 
        value: 
    """

    created: float
    """"""

    href: str
    """"""

    name: str
    """"""

    shape: GetAttributesResponseAttributesTypeShapeType
    """"""

    type: GetAttributesResponseAttributesTypeTypeType
    """"""

    value: str
    """"""


@dataclass(frozen=True)
class GetAttributesResponseHrefsType:
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
class GetAttributesResponse:
    """
    TODO

    Args:
        attributes: 
        hrefs: 
    """

    attributes: list[GetAttributesResponseAttributesType]
    """"""

    hrefs: list[GetAttributesResponseHrefsType]
    """"""


@dataclass(frozen=True)
class PutAttributeResponse:
    """
    TODO

    Args:
    """


@dataclass(frozen=True)
class GetAttributeResponseShapeType:
    """
    

    Args:
    """


@dataclass(frozen=True)
class GetAttributeResponseHrefsType:
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
class GetAttributeResponse:
    """
    TODO

    Args:
        created: 
        last_modified: 
        name: 
        shape: 
        value: 
        hrefs: 
    """

    created: float
    """"""

    last_modified: float
    """"""

    name: str
    """"""

    shape: GetAttributeResponseShapeType
    """"""

    value: str
    """"""

    hrefs: list[GetAttributeResponseHrefsType]
    """"""


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
class GetDatasetResponseTypeTypeFieldsType:
    """
    

    Args:
        name: Descriptive or identifying name. Must be unique in the fields list.
        type: Enum of pre-defined type, UUID of committed type, or type definition. (TODO: see `POST Dataset`?)
    """

    name: str
    """Descriptive or identifying name. Must be unique in the fields list.
"""

    type: str
    """Enum of pre-defined type, UUID of committed type, or type definition. (TODO: see `POST Dataset`?)
"""


@dataclass(frozen=True)
class GetDatasetResponseTypeType:
    """
    TODO

    Args:
        class: TODO
        base: TODO
        fields: List of fields in a compound dataset.
    """

    class_: str
    """TODO
"""

    base: str
    """TODO
Only present if class is not `H5T_COMPUND`.
"""

    fields: list[GetDatasetResponseTypeTypeFieldsType]
    """List of fields in a compound dataset.
Only present if `class` is `H5T_COMPOUND`.
"""


@dataclass(frozen=True)
class GetDatasetResponseShapeType:
    """
    TODO

    Args:
        class: String enum indicating expected structure.
        dims: Extent of each dimension in Dataset.
        maxdims: Maximum possible extent for each dimension.
    """

    class_: str
    """String enum indicating expected structure.
+ H5S_NULL -- Dataset has no data and no shape.
+ H5S_SCALAR -- Single entity as the Datast.
+ H5S_SIMPLE -- Dataset has hyperrectangular shape of
  one or more dimensions.
"""

    dims: list[float]
    """Extent of each dimension in Dataset.
Only present if `class` is `H5S_SIMPLE`.
"""

    maxdims: list[float]
    """Maximum possible extent for each dimension.
Value of `0` in array indicates that the dimension has unlimited maximum extent.
Only present if `class` is `H5S_SIMPLE`, and `maxdims` was included upon Dataset creation.
"""


@dataclass(frozen=True)
class GetDatasetResponseLayoutType:
    """
    TODO

    Args:
    """


@dataclass(frozen=True)
class GetDatasetResponseCreationPropertiesType:
    """
    Dataset creation properties as provided upon creation.

    Args:
    """


@dataclass(frozen=True)
class GetDatasetResponseHrefsType:
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
class GetDatasetResponse:
    """
    

    Args:
        id: UUID of this Dataset.
        root: UUID of root Group in Domain.
        domain: 
        created: 
        last_modified: 
        attribute_count: 
        type: TODO
        shape: TODO
        layout: TODO
        creation_properties: Dataset creation properties as provided upon creation.
        hrefs: List of references to other objects.
    """

    id: str
    """UUID of this Dataset."""

    root: str
    """UUID of root Group in Domain."""

    domain: str
    """"""

    created: float
    """"""

    last_modified: float
    """"""

    attribute_count: float
    """"""

    type: GetDatasetResponseTypeType
    """TODO"""

    shape: GetDatasetResponseShapeType
    """TODO"""

    layout: GetDatasetResponseLayoutType
    """TODO"""

    creation_properties: GetDatasetResponseCreationPropertiesType
    """Dataset creation properties as provided upon creation.
"""

    hrefs: list[GetDatasetResponseHrefsType]
    """List of references to other objects.
Must include references to only: `attributes`, `data` (shape class `H5S_NULL` must _not_ include `data`), `root`, `self`.
"""


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
class PostValuesResponse:
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



class DomainAsyncClient:
    """Provides methods to interact with domain."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def put_domain(self, body: Optional[object], domain: Optional[str] = None, folder: Optional[float] = None) -> Awaitable[PutDomainResponse]:
        """
        Create a new Domain on the service.

        Args:
            domain: 
            folder: If present and `1`, creates a Folder instead of a Domain.
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if folder is not None:
            __query_values["folder"] = quote(_to_string(folder), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutDomainResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_domain(self, domain: Optional[str] = None) -> Awaitable[GetDomainResponse]:
        """
        Get information about the requested domain.

        Args:
            domain: 
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDomainResponse, "GET", __url, "application/json", None, None)

    def delete_domain(self, domain: Optional[str] = None) -> Awaitable[DeleteDomainResponse]:
        """
        Delete the specified Domain or Folder.

        Args:
            domain: 
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDomainResponse, "DELETE", __url, "application/json", None, None)

    def post_group(self, body: Optional[object], domain: Optional[str] = None) -> Awaitable[PostGroupResponse]:
        """
        Create a new Group.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostGroupResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_groups(self, domain: Optional[str] = None) -> Awaitable[GetGroupsResponse]:
        """
        Get UUIDs for all non-root Groups in Domain.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupsResponse, "GET", __url, "application/json", None, None)

    def post_dataset(self, body: object, domain: Optional[str] = None) -> Awaitable[PostDatasetResponse]:
        """
        Create a Dataset.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDatasetResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datasets(self, domain: Optional[str] = None) -> Awaitable[GetDatasetsResponse]:
        """
        List Datasets.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetsResponse, "GET", __url, "application/json", None, None)

    def post_data_type(self, body: object, domain: Optional[str] = None) -> Awaitable[PostDataTypeResponse]:
        """
        Commit a Datatype to the Domain.

        Args:
            domain: 
        """

        __url = "/datatypes"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDataTypeResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_access_lists(self, domain: Optional[str] = None) -> Awaitable[GetAccessListsResponse]:
        """
        Get access lists on Domain.

        Args:
            domain: 
        """

        __url = "/acls"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_user_access(self, user: str, domain: Optional[str] = None) -> Awaitable[GetUserAccessResponse]:
        """
        Get users's access to a Domain.

        Args:
            domain: 
            user: User identifier/name.
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetUserAccessResponse, "GET", __url, "application/json", None, None)

    def put_user_access(self, user: str, body: object, domain: Optional[str] = None) -> Awaitable[PutUserAccessResponse]:
        """
        Set user's access to the Domain.

        Args:
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutUserAccessResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))


class GroupAsyncClient:
    """Provides methods to interact with group."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def post_group(self, body: Optional[object], domain: Optional[str] = None) -> Awaitable[PostGroupResponse]:
        """
        Create a new Group.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostGroupResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_groups(self, domain: Optional[str] = None) -> Awaitable[GetGroupsResponse]:
        """
        Get UUIDs for all non-root Groups in Domain.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupsResponse, "GET", __url, "application/json", None, None)

    def get_group(self, id: str, domain: Optional[str] = None, getalias: Optional[int] = None) -> Awaitable[GetGroupResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if getalias is not None:
            __query_values["getalias"] = quote(_to_string(getalias), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupResponse, "GET", __url, "application/json", None, None)

    def delete_group(self, id: str, domain: Optional[str] = None) -> Awaitable[DeleteGroupResponse]:
        """
        Delete a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteGroupResponse, "DELETE", __url, "application/json", None, None)

    def get_attributes(self, collection: str, obj_uuid: str, domain: Optional[str] = None, limit: Optional[float] = None, marker: Optional[str] = None) -> Awaitable[GetAttributesResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, collection: str, obj_uuid: str, attr: str, body: object, domain: Optional[str] = None) -> Awaitable[PutAttributeResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, collection: str, obj_uuid: str, attr: str, domain: Optional[str] = None) -> Awaitable[GetAttributeResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributeResponse, "GET", __url, "application/json", None, None)

    def get_group_access_lists(self, id: str, domain: Optional[str] = None) -> Awaitable[GetGroupAccessListsResponse]:
        """
        List access lists on Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_group_user_access(self, id: str, user: str, domain: Optional[str] = None) -> Awaitable[GetGroupUserAccessResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupUserAccessResponse, "GET", __url, "application/json", None, None)


class LinkAsyncClient:
    """Provides methods to interact with link."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def get_links(self, id: str, domain: Optional[str] = None, limit: Optional[float] = None, marker: Optional[str] = None) -> Awaitable[GetLinksResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetLinksResponse, "GET", __url, "application/json", None, None)

    def put_link(self, id: str, linkname: str, body: object, domain: Optional[str] = None) -> Awaitable[PutLinkResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutLinkResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_link(self, id: str, linkname: str, domain: Optional[str] = None) -> Awaitable[GetLinkResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetLinkResponse, "GET", __url, "application/json", None, None)

    def delete_link(self, id: str, linkname: str, domain: Optional[str] = None) -> Awaitable[DeleteLinkResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteLinkResponse, "DELETE", __url, "application/json", None, None)


class DatasetAsyncClient:
    """Provides methods to interact with dataset."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def post_dataset(self, body: object, domain: Optional[str] = None) -> Awaitable[PostDatasetResponse]:
        """
        Create a Dataset.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDatasetResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datasets(self, domain: Optional[str] = None) -> Awaitable[GetDatasetsResponse]:
        """
        List Datasets.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetsResponse, "GET", __url, "application/json", None, None)

    def get_dataset(self, id: str, domain: Optional[str] = None) -> Awaitable[GetDatasetResponse]:
        """
        Get information about a Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetResponse, "GET", __url, "application/json", None, None)

    def delete_dataset(self, id: str, domain: Optional[str] = None) -> Awaitable[DeleteDatasetResponse]:
        """
        Delete a Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDatasetResponse, "DELETE", __url, "application/json", None, None)

    def put_shape(self, id: str, body: object, domain: Optional[str] = None) -> Awaitable[PutShapeResponse]:
        """
        Modify a Dataset's dimensions.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/shape"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutShapeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_shape(self, id: str, domain: Optional[str] = None) -> Awaitable[GetShapeResponse]:
        """
        Get information about a Dataset's shape.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/shape"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetShapeResponse, "GET", __url, "application/json", None, None)

    def get_data_type(self, id: str, domain: Optional[str] = None) -> Awaitable[GetDataTypeResponse]:
        """
        Get information about a Dataset's type.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/type"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeResponse, "GET", __url, "application/json", None, None)

    def put_values(self, id: str, body: object, domain: Optional[str] = None) -> Awaitable[None]:
        """
        Write values to Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(type(None), "PUT", __url, None, "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_values_as_stream(self, id: str, domain: Optional[str] = None, select: Optional[str] = None, query: Optional[str] = None, limit: Optional[float] = None) -> Awaitable[Response]:
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

        if domain is not None:
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

    def get_values_as_json(self, id: str, domain: Optional[str] = None, select: Optional[str] = None, query: Optional[str] = None, limit: Optional[float] = None) -> Awaitable[GetValuesAsJsonResponse]:
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

        if domain is not None:
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

    def post_values(self, id: str, body: object, domain: Optional[str] = None) -> Awaitable[PostValuesResponse]:
        """
        Get specific data points from Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostValuesResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attributes(self, collection: str, obj_uuid: str, domain: Optional[str] = None, limit: Optional[float] = None, marker: Optional[str] = None) -> Awaitable[GetAttributesResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, collection: str, obj_uuid: str, attr: str, body: object, domain: Optional[str] = None) -> Awaitable[PutAttributeResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, collection: str, obj_uuid: str, attr: str, domain: Optional[str] = None) -> Awaitable[GetAttributeResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributeResponse, "GET", __url, "application/json", None, None)

    def get_dataset_access_lists(self, id: str, domain: Optional[str] = None) -> Awaitable[GetDatasetAccessListsResponse]:
        """
        Get access lists on Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetAccessListsResponse, "GET", __url, "application/json", None, None)


class DatatypeAsyncClient:
    """Provides methods to interact with datatype."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def post_data_type(self, body: object, domain: Optional[str] = None) -> Awaitable[PostDataTypeResponse]:
        """
        Commit a Datatype to the Domain.

        Args:
            domain: 
        """

        __url = "/datatypes"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDataTypeResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datatype(self, id: str, domain: Optional[str] = None) -> Awaitable[GetDatatypeResponse]:
        """
        Get information about a committed Datatype

        Args:
            domain: 
            id: UUID of the committed datatype.
        """

        __url = "/datatypes/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatatypeResponse, "GET", __url, "application/json", None, None)

    def delete_datatype(self, id: str, domain: Optional[str] = None) -> Awaitable[DeleteDatatypeResponse]:
        """
        Delete a committed Datatype.

        Args:
            domain: 
            id: UUID of the committed datatype.
        """

        __url = "/datatypes/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDatatypeResponse, "DELETE", __url, "application/json", None, None)

    def get_attributes(self, collection: str, obj_uuid: str, domain: Optional[str] = None, limit: Optional[float] = None, marker: Optional[str] = None) -> Awaitable[GetAttributesResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, collection: str, obj_uuid: str, attr: str, body: object, domain: Optional[str] = None) -> Awaitable[PutAttributeResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, collection: str, obj_uuid: str, attr: str, domain: Optional[str] = None) -> Awaitable[GetAttributeResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributeResponse, "GET", __url, "application/json", None, None)

    def get_data_type_access_lists(self, id: str, domain: Optional[str] = None) -> Awaitable[GetDataTypeAccessListsResponse]:
        """
        List access lists on Datatype.

        Args:
            id: UUID of the committed datatype.
            domain: 
        """

        __url = "/datatypes/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeAccessListsResponse, "GET", __url, "application/json", None, None)


class AttributeAsyncClient:
    """Provides methods to interact with attribute."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def get_attributes(self, collection: str, obj_uuid: str, domain: Optional[str] = None, limit: Optional[float] = None, marker: Optional[str] = None) -> Awaitable[GetAttributesResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, collection: str, obj_uuid: str, attr: str, body: object, domain: Optional[str] = None) -> Awaitable[PutAttributeResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, collection: str, obj_uuid: str, attr: str, domain: Optional[str] = None) -> Awaitable[GetAttributeResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributeResponse, "GET", __url, "application/json", None, None)


class ACLSAsyncClient:
    """Provides methods to interact with acls."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def get_access_lists(self, domain: Optional[str] = None) -> Awaitable[GetAccessListsResponse]:
        """
        Get access lists on Domain.

        Args:
            domain: 
        """

        __url = "/acls"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_user_access(self, user: str, domain: Optional[str] = None) -> Awaitable[GetUserAccessResponse]:
        """
        Get users's access to a Domain.

        Args:
            domain: 
            user: User identifier/name.
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetUserAccessResponse, "GET", __url, "application/json", None, None)

    def put_user_access(self, user: str, body: object, domain: Optional[str] = None) -> Awaitable[PutUserAccessResponse]:
        """
        Set user's access to the Domain.

        Args:
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutUserAccessResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_group_access_lists(self, id: str, domain: Optional[str] = None) -> Awaitable[GetGroupAccessListsResponse]:
        """
        List access lists on Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_group_user_access(self, id: str, user: str, domain: Optional[str] = None) -> Awaitable[GetGroupUserAccessResponse]:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupUserAccessResponse, "GET", __url, "application/json", None, None)

    def get_dataset_access_lists(self, id: str, domain: Optional[str] = None) -> Awaitable[GetDatasetAccessListsResponse]:
        """
        Get access lists on Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_data_type_access_lists(self, id: str, domain: Optional[str] = None) -> Awaitable[GetDataTypeAccessListsResponse]:
        """
        List access lists on Datatype.

        Args:
            id: UUID of the committed datatype.
            domain: 
        """

        __url = "/datatypes/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeAccessListsResponse, "GET", __url, "application/json", None, None)



class DomainClient:
    """Provides methods to interact with domain."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def put_domain(self, body: Optional[object], domain: Optional[str] = None, folder: Optional[float] = None) -> PutDomainResponse:
        """
        Create a new Domain on the service.

        Args:
            domain: 
            folder: If present and `1`, creates a Folder instead of a Domain.
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if folder is not None:
            __query_values["folder"] = quote(_to_string(folder), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutDomainResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_domain(self, domain: Optional[str] = None) -> GetDomainResponse:
        """
        Get information about the requested domain.

        Args:
            domain: 
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDomainResponse, "GET", __url, "application/json", None, None)

    def delete_domain(self, domain: Optional[str] = None) -> DeleteDomainResponse:
        """
        Delete the specified Domain or Folder.

        Args:
            domain: 
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDomainResponse, "DELETE", __url, "application/json", None, None)

    def post_group(self, body: Optional[object], domain: Optional[str] = None) -> PostGroupResponse:
        """
        Create a new Group.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostGroupResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_groups(self, domain: Optional[str] = None) -> GetGroupsResponse:
        """
        Get UUIDs for all non-root Groups in Domain.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupsResponse, "GET", __url, "application/json", None, None)

    def post_dataset(self, body: object, domain: Optional[str] = None) -> PostDatasetResponse:
        """
        Create a Dataset.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDatasetResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datasets(self, domain: Optional[str] = None) -> GetDatasetsResponse:
        """
        List Datasets.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetsResponse, "GET", __url, "application/json", None, None)

    def post_data_type(self, body: object, domain: Optional[str] = None) -> PostDataTypeResponse:
        """
        Commit a Datatype to the Domain.

        Args:
            domain: 
        """

        __url = "/datatypes"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDataTypeResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_access_lists(self, domain: Optional[str] = None) -> GetAccessListsResponse:
        """
        Get access lists on Domain.

        Args:
            domain: 
        """

        __url = "/acls"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_user_access(self, user: str, domain: Optional[str] = None) -> GetUserAccessResponse:
        """
        Get users's access to a Domain.

        Args:
            domain: 
            user: User identifier/name.
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetUserAccessResponse, "GET", __url, "application/json", None, None)

    def put_user_access(self, user: str, body: object, domain: Optional[str] = None) -> PutUserAccessResponse:
        """
        Set user's access to the Domain.

        Args:
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutUserAccessResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))


class GroupClient:
    """Provides methods to interact with group."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def post_group(self, body: Optional[object], domain: Optional[str] = None) -> PostGroupResponse:
        """
        Create a new Group.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostGroupResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_groups(self, domain: Optional[str] = None) -> GetGroupsResponse:
        """
        Get UUIDs for all non-root Groups in Domain.

        Args:
            domain: 
        """

        __url = "/groups"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupsResponse, "GET", __url, "application/json", None, None)

    def get_group(self, id: str, domain: Optional[str] = None, getalias: Optional[int] = None) -> GetGroupResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if getalias is not None:
            __query_values["getalias"] = quote(_to_string(getalias), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupResponse, "GET", __url, "application/json", None, None)

    def delete_group(self, id: str, domain: Optional[str] = None) -> DeleteGroupResponse:
        """
        Delete a Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteGroupResponse, "DELETE", __url, "application/json", None, None)

    def get_attributes(self, collection: str, obj_uuid: str, domain: Optional[str] = None, limit: Optional[float] = None, marker: Optional[str] = None) -> GetAttributesResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, collection: str, obj_uuid: str, attr: str, body: object, domain: Optional[str] = None) -> PutAttributeResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, collection: str, obj_uuid: str, attr: str, domain: Optional[str] = None) -> GetAttributeResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributeResponse, "GET", __url, "application/json", None, None)

    def get_group_access_lists(self, id: str, domain: Optional[str] = None) -> GetGroupAccessListsResponse:
        """
        List access lists on Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_group_user_access(self, id: str, user: str, domain: Optional[str] = None) -> GetGroupUserAccessResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupUserAccessResponse, "GET", __url, "application/json", None, None)


class LinkClient:
    """Provides methods to interact with link."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def get_links(self, id: str, domain: Optional[str] = None, limit: Optional[float] = None, marker: Optional[str] = None) -> GetLinksResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetLinksResponse, "GET", __url, "application/json", None, None)

    def put_link(self, id: str, linkname: str, body: object, domain: Optional[str] = None) -> PutLinkResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutLinkResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_link(self, id: str, linkname: str, domain: Optional[str] = None) -> GetLinkResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetLinkResponse, "GET", __url, "application/json", None, None)

    def delete_link(self, id: str, linkname: str, domain: Optional[str] = None) -> DeleteLinkResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteLinkResponse, "DELETE", __url, "application/json", None, None)


class DatasetClient:
    """Provides methods to interact with dataset."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def post_dataset(self, body: object, domain: Optional[str] = None) -> PostDatasetResponse:
        """
        Create a Dataset.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDatasetResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datasets(self, domain: Optional[str] = None) -> GetDatasetsResponse:
        """
        List Datasets.

        Args:
            domain: 
        """

        __url = "/datasets"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetsResponse, "GET", __url, "application/json", None, None)

    def get_dataset(self, id: str, domain: Optional[str] = None) -> GetDatasetResponse:
        """
        Get information about a Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetResponse, "GET", __url, "application/json", None, None)

    def delete_dataset(self, id: str, domain: Optional[str] = None) -> DeleteDatasetResponse:
        """
        Delete a Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDatasetResponse, "DELETE", __url, "application/json", None, None)

    def put_shape(self, id: str, body: object, domain: Optional[str] = None) -> PutShapeResponse:
        """
        Modify a Dataset's dimensions.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/shape"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutShapeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_shape(self, id: str, domain: Optional[str] = None) -> GetShapeResponse:
        """
        Get information about a Dataset's shape.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/shape"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetShapeResponse, "GET", __url, "application/json", None, None)

    def get_data_type(self, id: str, domain: Optional[str] = None) -> GetDataTypeResponse:
        """
        Get information about a Dataset's type.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/type"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeResponse, "GET", __url, "application/json", None, None)

    def put_values(self, id: str, body: object, domain: Optional[str] = None) -> None:
        """
        Write values to Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(type(None), "PUT", __url, None, "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_values_as_stream(self, id: str, domain: Optional[str] = None, select: Optional[str] = None, query: Optional[str] = None, limit: Optional[float] = None) -> Response:
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

        if domain is not None:
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

    def get_values_as_json(self, id: str, domain: Optional[str] = None, select: Optional[str] = None, query: Optional[str] = None, limit: Optional[float] = None) -> GetValuesAsJsonResponse:
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

        if domain is not None:
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

    def post_values(self, id: str, body: object, domain: Optional[str] = None) -> PostValuesResponse:
        """
        Get specific data points from Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/value"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostValuesResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attributes(self, collection: str, obj_uuid: str, domain: Optional[str] = None, limit: Optional[float] = None, marker: Optional[str] = None) -> GetAttributesResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, collection: str, obj_uuid: str, attr: str, body: object, domain: Optional[str] = None) -> PutAttributeResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, collection: str, obj_uuid: str, attr: str, domain: Optional[str] = None) -> GetAttributeResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributeResponse, "GET", __url, "application/json", None, None)

    def get_dataset_access_lists(self, id: str, domain: Optional[str] = None) -> GetDatasetAccessListsResponse:
        """
        Get access lists on Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetAccessListsResponse, "GET", __url, "application/json", None, None)


class DatatypeClient:
    """Provides methods to interact with datatype."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def post_data_type(self, body: object, domain: Optional[str] = None) -> PostDataTypeResponse:
        """
        Commit a Datatype to the Domain.

        Args:
            domain: 
        """

        __url = "/datatypes"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PostDataTypeResponse, "POST", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_datatype(self, id: str, domain: Optional[str] = None) -> GetDatatypeResponse:
        """
        Get information about a committed Datatype

        Args:
            domain: 
            id: UUID of the committed datatype.
        """

        __url = "/datatypes/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatatypeResponse, "GET", __url, "application/json", None, None)

    def delete_datatype(self, id: str, domain: Optional[str] = None) -> DeleteDatatypeResponse:
        """
        Delete a committed Datatype.

        Args:
            domain: 
            id: UUID of the committed datatype.
        """

        __url = "/datatypes/{id}"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(DeleteDatatypeResponse, "DELETE", __url, "application/json", None, None)

    def get_attributes(self, collection: str, obj_uuid: str, domain: Optional[str] = None, limit: Optional[float] = None, marker: Optional[str] = None) -> GetAttributesResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, collection: str, obj_uuid: str, attr: str, body: object, domain: Optional[str] = None) -> PutAttributeResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, collection: str, obj_uuid: str, attr: str, domain: Optional[str] = None) -> GetAttributeResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributeResponse, "GET", __url, "application/json", None, None)

    def get_data_type_access_lists(self, id: str, domain: Optional[str] = None) -> GetDataTypeAccessListsResponse:
        """
        List access lists on Datatype.

        Args:
            id: UUID of the committed datatype.
            domain: 
        """

        __url = "/datatypes/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeAccessListsResponse, "GET", __url, "application/json", None, None)


class AttributeClient:
    """Provides methods to interact with attribute."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def get_attributes(self, collection: str, obj_uuid: str, domain: Optional[str] = None, limit: Optional[float] = None, marker: Optional[str] = None) -> GetAttributesResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        if limit is not None:
            __query_values["Limit"] = quote(_to_string(limit), safe="")

        if marker is not None:
            __query_values["Marker"] = quote(_to_string(marker), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributesResponse, "GET", __url, "application/json", None, None)

    def put_attribute(self, collection: str, obj_uuid: str, attr: str, body: object, domain: Optional[str] = None) -> PutAttributeResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutAttributeResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_attribute(self, collection: str, obj_uuid: str, attr: str, domain: Optional[str] = None) -> GetAttributeResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAttributeResponse, "GET", __url, "application/json", None, None)


class ACLSClient:
    """Provides methods to interact with acls."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def get_access_lists(self, domain: Optional[str] = None) -> GetAccessListsResponse:
        """
        Get access lists on Domain.

        Args:
            domain: 
        """

        __url = "/acls"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_user_access(self, user: str, domain: Optional[str] = None) -> GetUserAccessResponse:
        """
        Get users's access to a Domain.

        Args:
            domain: 
            user: User identifier/name.
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetUserAccessResponse, "GET", __url, "application/json", None, None)

    def put_user_access(self, user: str, body: object, domain: Optional[str] = None) -> PutUserAccessResponse:
        """
        Set user's access to the Domain.

        Args:
            user: Identifier/name of a user.
            domain: 
        """

        __url = "/acls/{user}"
        __url = __url.replace("{user}", quote(str(user), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(PutUserAccessResponse, "PUT", __url, "application/json", "application/json", json.dumps(JsonEncoder.encode(body, _json_encoder_options)))

    def get_group_access_lists(self, id: str, domain: Optional[str] = None) -> GetGroupAccessListsResponse:
        """
        List access lists on Group.

        Args:
            id: UUID of the Group, e.g. `g-37aa76f6-2c86-11e8-9391-0242ac110009`.
            domain: 
        """

        __url = "/groups/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_group_user_access(self, id: str, user: str, domain: Optional[str] = None) -> GetGroupUserAccessResponse:
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

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetGroupUserAccessResponse, "GET", __url, "application/json", None, None)

    def get_dataset_access_lists(self, id: str, domain: Optional[str] = None) -> GetDatasetAccessListsResponse:
        """
        Get access lists on Dataset.

        Args:
            id: UUID of the Dataset.
            domain: 
        """

        __url = "/datasets/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDatasetAccessListsResponse, "GET", __url, "application/json", None, None)

    def get_data_type_access_lists(self, id: str, domain: Optional[str] = None) -> GetDataTypeAccessListsResponse:
        """
        List access lists on Datatype.

        Args:
            id: UUID of the committed datatype.
            domain: 
        """

        __url = "/datatypes/{id}/acls"
        __url = __url.replace("{id}", quote(str(id), safe=""))

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(GetDataTypeAccessListsResponse, "GET", __url, "application/json", None, None)






class HsdsAsyncClient:
    """A client for the Hsds system."""
    
    _http_client: AsyncClient

    _domain: DomainAsyncClient
    _group: GroupAsyncClient
    _link: LinkAsyncClient
    _dataset: DatasetAsyncClient
    _datatype: DatatypeAsyncClient
    _attribute: AttributeAsyncClient
    _aCLS: ACLSAsyncClient


    @classmethod
    def create(cls, base_url: str) -> HsdsAsyncClient:
        """
        Initializes a new instance of the HsdsAsyncClient
        
            Args:
                base_url: The base URL to use.
        """
        return HsdsAsyncClient(AsyncClient(base_url=base_url, timeout=60.0))

    def __init__(self, http_client: AsyncClient):
        """
        Initializes a new instance of the HsdsAsyncClient
        
            Args:
                http_client: The HTTP client to use.
        """

        if http_client.base_url is None:
            raise Exception("The base url of the HTTP client must be set.")

        self._http_client = http_client
        self._token_pair = None

        self._domain = DomainAsyncClient(self)
        self._group = GroupAsyncClient(self)
        self._link = LinkAsyncClient(self)
        self._dataset = DatasetAsyncClient(self)
        self._datatype = DatatypeAsyncClient(self)
        self._attribute = AttributeAsyncClient(self)
        self._aCLS = ACLSAsyncClient(self)



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





    async def _invoke(self, typeOfT: Type[T], method: str, relative_url: str, accept_header_value: Optional[str], content_type_value: Optional[str], content: Union[None, str, bytes, Iterable[bytes], AsyncIterable[bytes]]) -> T:

        # prepare request
        request = self._build_request_message(method, relative_url, content, content_type_value, accept_header_value)

        # send request
        response = await self._http_client.send(request)

        # process response
        if not response.is_success:
            

            if not response.is_success:

                message = response.text
                status_code = f"H00.{response.status_code}"

                if not message:
                    raise HsdsException(status_code, f"The HTTP request failed with status code {response.status_code}.")

                else:
                    raise HsdsException(status_code, f"The HTTP request failed with status code {response.status_code}. The response message is: {message}")

        try:

            if typeOfT is type(None):
                return typing.cast(T, type(None))

            elif typeOfT is Response:
                return typing.cast(T, response)

            else:

                jsonObject = json.loads(response.text)
                return_value = JsonEncoder.decode(typeOfT, jsonObject, _json_encoder_options)

                if return_value is None:
                    raise HsdsException(f"H01", "Response data could not be deserialized.")

                return return_value

        finally:
            if typeOfT is not Response:
                await response.aclose()
    
    def _build_request_message(self, method: str, relative_url: str, content: Any, content_type_value: Optional[str], accept_header_value: Optional[str]) -> Request:
       
        request_message = self._http_client.build_request(method, relative_url, content = content)

        if content_type_value is not None:
            request_message.headers["Content-Type"] = content_type_value

        if accept_header_value is not None:
            request_message.headers["Accept"] = accept_header_value

        return request_message


    # "disposable" methods
    async def __aenter__(self) -> HsdsAsyncClient:
        return self

    async def __aexit__(self, exc_type, exc_value, exc_traceback):
        if (self._http_client is not None):
            await self._http_client.aclose()



class HsdsClient:
    """A client for the Hsds system."""
    
    _http_client: Client

    _domain: DomainClient
    _group: GroupClient
    _link: LinkClient
    _dataset: DatasetClient
    _datatype: DatatypeClient
    _attribute: AttributeClient
    _aCLS: ACLSClient


    @classmethod
    def create(cls, base_url: str) -> HsdsClient:
        """
        Initializes a new instance of the HsdsClient
        
            Args:
                base_url: The base URL to use.
        """
        return HsdsClient(Client(base_url=base_url, timeout=60.0))

    def __init__(self, http_client: Client):
        """
        Initializes a new instance of the HsdsClient
        
            Args:
                http_client: The HTTP client to use.
        """

        if http_client.base_url is None:
            raise Exception("The base url of the HTTP client must be set.")

        self._http_client = http_client
        self._token_pair = None

        self._domain = DomainClient(self)
        self._group = GroupClient(self)
        self._link = LinkClient(self)
        self._dataset = DatasetClient(self)
        self._datatype = DatatypeClient(self)
        self._attribute = AttributeClient(self)
        self._aCLS = ACLSClient(self)



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





    def _invoke(self, typeOfT: Type[T], method: str, relative_url: str, accept_header_value: Optional[str], content_type_value: Optional[str], content: Union[None, str, bytes, Iterable[bytes], AsyncIterable[bytes]]) -> T:

        # prepare request
        request = self._build_request_message(method, relative_url, content, content_type_value, accept_header_value)

        # send request
        response = self._http_client.send(request)

        # process response
        if not response.is_success:
            

            if not response.is_success:

                message = response.text
                status_code = f"H00.{response.status_code}"

                if not message:
                    raise HsdsException(status_code, f"The HTTP request failed with status code {response.status_code}.")

                else:
                    raise HsdsException(status_code, f"The HTTP request failed with status code {response.status_code}. The response message is: {message}")

        try:

            if typeOfT is type(None):
                return typing.cast(T, type(None))

            elif typeOfT is Response:
                return typing.cast(T, response)

            else:

                jsonObject = json.loads(response.text)
                return_value = JsonEncoder.decode(typeOfT, jsonObject, _json_encoder_options)

                if return_value is None:
                    raise HsdsException(f"H01", "Response data could not be deserialized.")

                return return_value

        finally:
            if typeOfT is not Response:
                response.close()
    
    def _build_request_message(self, method: str, relative_url: str, content: Any, content_type_value: Optional[str], accept_header_value: Optional[str]) -> Request:
       
        request_message = self._http_client.build_request(method, relative_url, content = content)

        if content_type_value is not None:
            request_message.headers["Content-Type"] = content_type_value

        if accept_header_value is not None:
            request_message.headers["Accept"] = accept_header_value

        return request_message


    # "disposable" methods
    def __enter__(self) -> HsdsClient:
        return self

    def __exit__(self, exc_type, exc_value, exc_traceback):
        if (self._http_client is not None):
            self._http_client.close()

