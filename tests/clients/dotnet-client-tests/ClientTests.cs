using Hsds.Api;
using Xunit;

namespace Nexus.Api.Tests
{
    public class ClientTests
    {
        [Fact]
        public void CanGetDomain()
        {
            // Arrange
            var url = new Uri("http://hsdshdflab.hdfgroup.org");
            using var client = new HsdsClient(url);
            var domain = "/shared/tall.h5";

            // Act
            var actual = client.Domain.GetDomain(domain).Root;

            // Assert
            var expected = "g-d38053ea-3418fe27-5b08-db62bc-9076af";
            Assert.Equal(expected, actual);
        }

        [Fact]
        public async Task CanGetDomainAsync()
        {
            // Arrange
            var url = new Uri("http://hsdshdflab.hdfgroup.org");
            using var client = new HsdsClient(url);
            var domain = "/shared/tall.h5";

            // Act
            var actual = (await client.Domain.GetDomainAsync(domain)).Root;

            // Assert
            var expected = "g-d38053ea-3418fe27-5b08-db62bc-9076af";
            Assert.Equal(expected, actual);
        }
    }
}
