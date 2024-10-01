using Apollo3zehn.OpenApiClientGenerator;
using Microsoft.OpenApi.Readers;

namespace Hsds.ClientGenerator;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var solutionRoot = args.Length >= 1
            ? args[0]
            : "../../../../../";

        // read open API document
        var client = new HttpClient();
        var response = await client.GetAsync("https://raw.githubusercontent.com/HDFGroup/hdf-rest-api/master/openapi.yaml");

        response.EnsureSuccessStatusCode();

        var openApiYamlString = await response.Content.ReadAsStringAsync();
        // File.WriteAllText("/home/vincent/Downloads/openapi1.yaml", openApiYamlString);

        // TODO: https://github.com/HDFGroup/hdf-rest-api/issues/created_by/Apollo3zehn
        openApiYamlString = OpenApiFixer.Apply(openApiYamlString);
        // File.WriteAllText("/home/vincent/Downloads/openapi2.yaml", openApiYamlString);

        var document = new OpenApiStringReader()
            .Read(openApiYamlString, out var diagnostic);

        // generate clients

        // TODO: remove when https://github.com/HDFGroup/hdf-rest-api/issues/10 is resolved
        var pathToMethodNameMap = new Dictionary<string, string>()
        {
            ["/"] = "Domain",
            ["Post:/groups"] = "Group",
            ["Get:/groups"] = "Groups",
            ["/groups/{id}"] = "Group",
            ["/groups/{id}/links"] = "Links",
            ["/groups/{id}/links/{linkname}"] = "Link",
            ["Post:/datasets"] = "Dataset",
            ["Get:/datasets"] = "Datasets",
            ["/datasets/{id}"] = "Dataset",
            ["/datasets/{id}/shape"] = "Shape",
            ["/datasets/{id}/type"] = "DataType",
            ["/datasets/{id}/value"] = "Values",
            ["/datatypes"] = "DataType",
            ["/datatypes/{id}"] = "Datatype",
            ["/{collection}/{obj_uuid}/attributes"] = "Attributes",
            ["/{collection}/{obj_uuid}/attributes/{attr}"] = "Attribute",
            ["/acls"] = "AccessLists",
            ["/acls/{user}"] = "UserAccess",
            ["/groups/{id}/acls"] = "GroupAccessLists",
            ["/groups/{id}/acls/{user}"] = "GroupUserAccess",
            ["/datasets/{id}/acls"] = "DatasetAccessLists",
            ["/datatypes/{id}/acls"] = "DataTypeAccessLists"
        };

        var settings = new GeneratorSettings(
            Namespace: "Hsds.Api",
            ClientName: "Hsds",
            ExceptionType: "HsdsException",
            ExceptionCodePrefix: "H",
            GetOperationName: (path, type, _) => {
                if (!pathToMethodNameMap.TryGetValue($"{type}:{path}", out var methodName))
                    methodName = pathToMethodNameMap[path];

                return $"{type}{methodName}";
            },
            Special_ConfigurationHeaderKey: default!,
            Special_WebAssemblySupport: false,
            Special_AccessTokenSupport: false,
            Special_NexusFeatures: false
        );

        // generate C# client
        var csharpGenerator = new CSharpGenerator(settings);
        csharpGenerator.Generate($"{solutionRoot}src/clients/dotnet-client", document);

        // generate Python client
        var pythonGenerator = new PythonGenerator(settings);
        pythonGenerator.Generate($"{solutionRoot}src/clients/python-client/hsds_api/", document);
    }
}