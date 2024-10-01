import json
import struct

import pytest
from hsds_api import HsdsClient, HsdsAsyncClient

def sync_test():

    # arrange
    url = "http://hsdshdflab.hdfgroup.org"

    with HsdsClient.create(url) as client:

        domain_name = "/shared/tall.h5"

        # act

        # get domain
        domain = client.v2_0.domain.get_domain(domain_name)
        root_group_links = client.v2_0.link.get_links(domain.root, domain_name)

        # get group "g1"
        g1_link = root_group_links.links[0]
        g1_name = g1_link.title

        # get group "g1.1"
        g1_links = client.v2_0.link.get_links(g1_link.id, domain_name)
        g1_1_link = g1_links.links[0]
        g1_1_name = g1_1_link.title
        g1_1_links = client.v2_0.link.get_links(g1_1_link.id, domain_name)

        # get dataset "dset1.1.1"
        dset_1_1_1_link = g1_1_links.links[0]
        dset_1_1_1_name = dset_1_1_1_link.title

        dset_1_1_1_type = client.v2_0.dataset \
            .get_dataset(dset_1_1_1_link.id, domain_name) \
            .type

        # get data of dataset "dset1.1.1" as JSON
        json_response = client.v2_0.dataset.get_values_as_json(dset_1_1_1_link.id, domain_name)

        # get data of dataset "dset1.1.1" as stream
        stream_response = client.v2_0.dataset.get_values_as_stream(dset_1_1_1_link.id, domain_name)
        data = stream_response.read()

        # assert
        assert "g-d38053ea-3418fe27-5b08-db62bc-9076af" == domain.root
        assert "g1" == g1_name
        assert "g1.1" == g1_1_name
        assert "dset1.1.1" == dset_1_1_1_name
        assert "H5T_INTEGER" == dset_1_1_1_type.class_
        assert "H5T_STD_I32BE" == dset_1_1_1_type.base

        expected_data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]

        # JSON response
        expected_json_data_string = json.dumps(expected_data)
        actual_json_data_string = json.dumps(json_response.value[1])
        assert expected_json_data_string == actual_json_data_string

        # Stream response
        actual_data = list(struct.unpack(f">{int(len(data)/4)}i", data))
        assert expected_data == actual_data[10:20]

@pytest.mark.asyncio
async def async_test():
    
    # arrange
    url = "http://hsdshdflab.hdfgroup.org"

    async with HsdsAsyncClient.create(url) as client:

        domain_name = "/shared/tall.h5"

        # act

        # get domain
        domain = await client.v2_0.domain.get_domain(domain_name)
        root_group_links = await client.v2_0.link.get_links(domain.root, domain_name)

        # get group "g1"
        g1_link = root_group_links.links[0]
        g1_name = g1_link.title

        # get group "g1.1"
        g1_links = await client.v2_0.link.get_links(g1_link.id, domain_name)
        g1_1_link = g1_links.links[0]
        g1_1_name = g1_1_link.title
        g1_1_links = await client.v2_0.link.get_links(g1_1_link.id, domain_name)

        # get dataset "dset1.1.1"
        dset_1_1_1_link = g1_1_links.links[0]
        dset_1_1_1_name = dset_1_1_1_link.title

        dset_1_1_1_type = (await client.v2_0.dataset \
            .get_dataset(dset_1_1_1_link.id, domain_name)) \
            .type

        # get data of dataset "dset1.1.1" as JSON
        json_response = await client.v2_0.dataset.get_values_as_json(dset_1_1_1_link.id, domain_name)

        # get data of dataset "dset1.1.1" as stream
        stream_response = await client.v2_0.dataset.get_values_as_stream(dset_1_1_1_link.id, domain_name)
        data = await stream_response.aread()

        # assert
        assert "g-d38053ea-3418fe27-5b08-db62bc-9076af" == domain.root
        assert "g1" == g1_name
        assert "g1.1" == g1_1_name
        assert "dset1.1.1" == dset_1_1_1_name
        assert "H5T_INTEGER" == dset_1_1_1_type.class_
        assert "H5T_STD_I32BE" == dset_1_1_1_type.base

        expected_data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]

        # JSON response
        expected_json_data_string = json.dumps(expected_data)
        actual_json_data_string = json.dumps(json_response.value[1])
        assert expected_json_data_string == actual_json_data_string

        # Stream response
        actual_data = list(struct.unpack(f">{int(len(data)/4)}i", data))
        assert expected_data == actual_data[10:20]