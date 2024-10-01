using System.Runtime.InteropServices;
using System.Text.Json;
using Xunit;

namespace Hsds.Api.Tests
{
    public class ClientTests
    {
        private static void SwitchEndianness<T>(Span<T> dataset) where T : unmanaged
        {
            var size = Marshal.SizeOf<T>();
            var dataset_bytes = MemoryMarshal.Cast<T, byte>(dataset);

            for (int i = 0; i < dataset_bytes.Length; i += size)
            {
                for (int j = 0; j < size / 2; j++)
                {
                    var i1 = i + j;
                    var i2 = i - j + size - 1;

                    byte tmp = dataset_bytes[i1];
                    dataset_bytes[i1] = dataset_bytes[i2];
                    dataset_bytes[i2] = tmp;
                }
            }
        }

        [Fact]
        public void Test()
        {
            // Arrange
            var url = new Uri("http://hsdshdflab.hdfgroup.org");
            using var client = new HsdsClient(url);
            var domainName = "/shared/tall.h5";

            // Act

            /* get domain */
            var domain = client.V2_0.Domain.GetDomain(domainName);
            var rootGroupLinks = client.V2_0.Link.GetLinks(domain.Root, domainName);

            /* get group "g1" */
            var g1_Link = rootGroupLinks.Links[0];
            var g1_Name = g1_Link.Title;

            /* get group "g1.1" */
            var g1_Links = client.V2_0.Link.GetLinks(g1_Link.Id, domainName);
            var g1_1_Link = g1_Links.Links[0];
            var g1_1_Name = g1_1_Link.Title;
            var g1_1_Links = client.V2_0.Link.GetLinks(g1_1_Link.Id, domainName);

            /* get dataset "dset1.1.1" */
            var dset_1_1_1_Link = g1_1_Links.Links[0];
            var dset_1_1_1_Name = dset_1_1_1_Link.Title;

            var dset_1_1_1_Type = client.V2_0.Dataset
                .GetDataset(dset_1_1_1_Link.Id, domainName)
                .Type;

            /* get data of dataset "dset1.1.1" as JSON */
            var jsonResponse = client.V2_0.Dataset.GetValuesAsJson(dset_1_1_1_Link.Id, domainName);

            /* get data of dataset "dset1.1.1" as stream */
            var streamResponse = client.V2_0.Dataset.GetValuesAsStream(dset_1_1_1_Link.Id, domainName);
            var stream = streamResponse.Content.ReadAsStream();

            // Assert
            Assert.Equal("g-d38053ea-3418fe27-5b08-db62bc-9076af", domain.Root);
            Assert.Equal("g1", g1_Name);
            Assert.Equal("g1.1", g1_1_Name);
            Assert.Equal("dset1.1.1", dset_1_1_1_Name);
            Assert.Equal("H5T_INTEGER", dset_1_1_1_Type.Class);
            Assert.Equal("H5T_STD_I32BE", dset_1_1_1_Type.Base);

            var expectedData = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            /* JSON response */
            var expectedJsonDataString = JsonSerializer.Serialize(expectedData);
            var actualJsonDataString = JsonSerializer.Serialize(jsonResponse.Value[1]);
            Assert.Equal(expectedJsonDataString, actualJsonDataString);

            /* Stream response */
            var actualdata = new int[20];
            stream.ReadExactly(MemoryMarshal.AsBytes<int>(actualdata));
            SwitchEndianness<int>(actualdata);
            Assert.True(expectedData.SequenceEqual(actualdata.Skip(10)));
        }

        [Fact]
        public async Task TestAsync()
        {
            // Arrange
            var url = new Uri("http://hsdshdflab.hdfgroup.org");
            using var client = new HsdsClient(url);
            var domainName = "/shared/tall.h5";

            // Act

            /* get domain */
            var domain = await client.V2_0.Domain.GetDomainAsync(domainName);
            var rootGroupLinks = await client.V2_0.Link.GetLinksAsync(domain.Root, domainName);

            /* get group "g1" */
            var g1_Link = rootGroupLinks.Links[0];
            var g1_Name = g1_Link.Title;

            /* get group "g1.1" */
            var g1_Links = await client.V2_0.Link.GetLinksAsync(g1_Link.Id, domainName);
            var g1_1_Link = g1_Links.Links[0];
            var g1_1_Name = g1_1_Link.Title;
            var g1_1_Links = await client.V2_0.Link.GetLinksAsync(g1_1_Link.Id, domainName);

            /* get dataset "dset1.1.1" */
            var dset_1_1_1_Link = g1_1_Links.Links[0];
            var dset_1_1_1_Name = dset_1_1_1_Link.Title;

            var dset_1_1_1_Type = (await client.V2_0.Dataset
                .GetDatasetAsync(dset_1_1_1_Link.Id, domainName))
                .Type;

            /* get data of dataset "dset1.1.1" as JSON */
            var jsonResponse = await client.V2_0.Dataset.GetValuesAsJsonAsync(dset_1_1_1_Link.Id, domainName);

            /* get data of dataset "dset1.1.1" as stream */
            var streamResponse = await client.V2_0.Dataset.GetValuesAsStreamAsync(dset_1_1_1_Link.Id, domainName);
            var stream = await streamResponse.Content.ReadAsStreamAsync();

            // Assert
            Assert.Equal("g-d38053ea-3418fe27-5b08-db62bc-9076af", domain.Root);
            Assert.Equal("g1", g1_Name);
            Assert.Equal("g1.1", g1_1_Name);
            Assert.Equal("dset1.1.1", dset_1_1_1_Name);
            Assert.Equal("H5T_INTEGER", dset_1_1_1_Type.Class);
            Assert.Equal("H5T_STD_I32BE", dset_1_1_1_Type.Base);

            var expectedData = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            /* JSON response */
            var expectedJsonDataString = JsonSerializer.Serialize(expectedData);
            var actualJsonDataString = JsonSerializer.Serialize(jsonResponse.Value[1]);
            Assert.Equal(expectedJsonDataString, actualJsonDataString);

            /* Stream response */
            var actualdata = new int[20];
            stream.ReadExactly(MemoryMarshal.AsBytes<int>(actualdata));
            SwitchEndianness<int>(actualdata);
            Assert.True(expectedData.SequenceEqual(actualdata.Skip(10)));
        }
    }
}
