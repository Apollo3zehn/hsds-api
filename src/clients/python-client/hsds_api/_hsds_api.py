# pyright: reportPrivateUsage=false

# Python <= 3.9
from __future__ import annotations

from typing import TypeVar

T = TypeVar("T")

import dataclasses
import re
import typing
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from enum import Enum
from typing import (Any, Callable, ClassVar, Optional, Type, Union,
                    cast)
from uuid import UUID

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


import asyncio
import base64
import hashlib
import json
import os
import time
import typing
from array import array
from dataclasses import dataclass
from datetime import datetime, timedelta
from enum import Enum
from pathlib import Path
from tempfile import NamedTemporaryFile
from threading import Lock
from typing import (Any, AsyncIterable, Awaitable, Callable, Iterable,
                    Optional, Type, Union, cast)
from urllib.parse import quote
from uuid import UUID
from zipfile import ZipFile

from httpx import AsyncClient, Client, Request, Response, codes

def _to_string(value: Any) -> str:

    if type(value) is datetime:
        return value.isoformat()

    elif type(value) is str:
        return value

    else:
        return str(value)

_json_encoder_options: JsonEncoderOptions = JsonEncoderOptions(
    property_name_encoder=to_camel_case,
    property_name_decoder=to_snake_case
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


class DomainAsyncClient:
    """Provides methods to interact with domain."""

    ___client: HsdsAsyncClient
    
    def __init__(self, client: HsdsAsyncClient):
        self.___client = client

    def create_domain(self, domain: Optional[str], folder: int = None) -> Awaitable[str]:
        """
        Create a new Domain on the service.

        Args:
            domain: Domain on service to access, e.g., `/home/user/someproject/somefile`
            folder: If present and `1`, creates a Folder instead of a Domain.
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query_values["folder"] = quote(_to_string(folder), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(str, "PUT", __url, "application/json", None, None)



class DomainClient:
    """Provides methods to interact with domain."""

    ___client: HsdsClient
    
    def __init__(self, client: HsdsClient):
        self.___client = client

    def create_domain(self, domain: Optional[str], folder: int = None) -> str:
        """
        Create a new Domain on the service.

        Args:
            domain: Domain on service to access, e.g., `/home/user/someproject/somefile`
            folder: If present and `1`, creates a Folder instead of a Domain.
        """

        __url = "/"

        __query_values: dict[str, str] = {}

        if domain is not None:
            __query_values["domain"] = quote(_to_string(domain), safe="")

        __query_values["folder"] = quote(_to_string(folder), safe="")

        __query: str = "?" + "&".join(f"{key}={value}" for (key, value) in __query_values.items())
        __url += __query

        return self.___client._invoke(str, "PUT", __url, "application/json", None, None)






class HsdsAsyncClient:
    """A client for the Hsds system."""
    
    _http_client: AsyncClient

    _domain: DomainAsyncClient


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



    @property
    def domain(self) -> DomainAsyncClient:
        """Gets the DomainAsyncClient."""
        return self._domain





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



    @property
    def domain(self) -> DomainClient:
        """Gets the DomainClient."""
        return self._domain





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

