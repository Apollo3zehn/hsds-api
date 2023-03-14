from hsds_api import HsdsClient

def can_get_domain_test():

    # arrange
    url = "http://hsdshdflab.hdfgroup.org"

    with HsdsClient.create(url) as client:

        domain = "/shared/tall.h5"

        # act
        actual = client.domain.get_domain(domain).root

        # assert
        expected = "g-d38053ea-3418fe27-5b08-db62bc-9076af"
        assert expected == actual