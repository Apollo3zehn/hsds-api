# Python <= 3.9
from __future__ import annotations

import json
from typing import (Any, AsyncIterable, Iterable, Optional, Type, TypeVar,
                    Union, cast)

from httpx import AsyncClient, Client, Request, Response
from V2_0 import V2_0, V2_0Async

from ._encoder import JsonEncoder
from ._shared import HsdsException, _json_encoder_options

T = TypeVar("T")


class HsdsClient:
    """A client for the Hsds system."""
    
    ___http_client: Client

    _v2_0: V2_0


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

        self.___http_client = http_client

        self._v2_0 = V2_0(self)



    @property
    def v2_0(self) -> V2_0:
        """Gets the client for version V2_0."""
        return self._v2_0





    def _invoke(self, typeOfT: Optional[Type[T]], method: str, relative_url: str, accept_header_value: Optional[str], content_type_value: Optional[str], content: Union[None, str, bytes, Iterable[bytes], AsyncIterable[bytes]]) -> T:

        # prepare request
        request = self._build_request_message(method, relative_url, content, content_type_value, accept_header_value)

        # send request
        response = self.___http_client.send(request)

        # process response
        if not response.is_success:
            
            message = response.text
            status_code = f"H00.{response.status_code}"

            if not message:
                raise HsdsException(status_code, f"The HTTP request failed with status code {response.status_code}.")

            else:
                raise HsdsException(status_code, f"The HTTP request failed with status code {response.status_code}. The response message is: {message}")

        try:

            if typeOfT is type(None):
                return cast(T, type(None))

            elif typeOfT is Response:
                return cast(T, response)

            else:

                jsonObject = json.loads(response.text)
                return_value = JsonEncoder.decode(cast(Type[T], typeOfT), jsonObject, _json_encoder_options)

                if return_value is None:
                    raise HsdsException("H01", "Response data could not be deserialized.")

                return return_value

        finally:
            if typeOfT is not Response:
                response.close()
    
    def _build_request_message(self, method: str, relative_url: str, content: Any, content_type_value: Optional[str], accept_header_value: Optional[str]) -> Request:
       
        request_message = self.___http_client.build_request(method, relative_url, content = content)

        if content_type_value is not None:
            request_message.headers["Content-Type"] = content_type_value

        if accept_header_value is not None:
            request_message.headers["Accept"] = accept_header_value

        return request_message

    # "disposable" methods
    def __enter__(self) -> HsdsClient:
        return self

    def __exit__(self, exc_type, exc_value, exc_traceback):
        if (self.___http_client is not None):
            self.___http_client.close()



class HsdsAsyncClient:
    """A client for the Hsds system."""
    
    ___http_client: AsyncClient

    _v2_0: V2_0Async


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

        self.___http_client = http_client

        self._v2_0 = V2_0Async(self)



    @property
    def v2_0(self) -> V2_0Async:
        """Gets the client for version V2_0."""
        return self._v2_0





    async def _invoke(self, typeOfT: Optional[Type[T]], method: str, relative_url: str, accept_header_value: Optional[str], content_type_value: Optional[str], content: Union[None, str, bytes, Iterable[bytes], AsyncIterable[bytes]]) -> T:

        # prepare request
        request = self._build_request_message(method, relative_url, content, content_type_value, accept_header_value)

        # send request
        response = await self.___http_client.send(request)

        # process response
        if not response.is_success:
            
            message = response.text
            status_code = f"H00.{response.status_code}"

            if not message:
                raise HsdsException(status_code, f"The HTTP request failed with status code {response.status_code}.")

            else:
                raise HsdsException(status_code, f"The HTTP request failed with status code {response.status_code}. The response message is: {message}")

        try:

            if typeOfT is type(None):
                return cast(T, type(None))

            elif typeOfT is Response:
                return cast(T, response)

            else:

                jsonObject = json.loads(response.text)
                return_value = JsonEncoder.decode(cast(Type[T], typeOfT), jsonObject, _json_encoder_options)

                if return_value is None:
                    raise HsdsException("H01", "Response data could not be deserialized.")

                return return_value

        finally:
            if typeOfT is not Response:
                await response.aclose()
    
    def _build_request_message(self, method: str, relative_url: str, content: Any, content_type_value: Optional[str], accept_header_value: Optional[str]) -> Request:
       
        request_message = self.___http_client.build_request(method, relative_url, content = content)

        if content_type_value is not None:
            request_message.headers["Content-Type"] = content_type_value

        if accept_header_value is not None:
            request_message.headers["Accept"] = accept_header_value

        return request_message

    # "disposable" methods
    async def __aenter__(self) -> HsdsAsyncClient:
        return self

    async def __aexit__(self, exc_type, exc_value, exc_traceback):
        if (self.___http_client is not None):
            await self.___http_client.aclose()

