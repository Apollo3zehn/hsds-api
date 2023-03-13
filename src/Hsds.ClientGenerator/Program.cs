using System.Reflection;
using Apollo3zehn.OpenApiClientGenerator;
using Hsds.SpecGenerator.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Readers;

namespace Hsds.ClientGenerator;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var solutionRoot = args.Length >= 1
            ? args[0]
            : "../../../../../";

        var openApiFileName = args.Length == 2
            ? args[1]
            : "openapi.json";

        //
        var builder = WebApplication.CreateBuilder(args);

        builder.Services
            .AddMvcCore().AddApplicationPart(typeof(DomainController).Assembly);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApiDocument();
        builder.Services.AddRouting(options => options.LowercaseUrls = true);


        var app = builder.Build();

        app.UseOpenApi(settings => settings.Path = "/openapi/{documentName}/openapi.json");

        _ = app.RunAsync();

        // read open API document
        var client = new HttpClient();
        var response = await client.GetAsync("http://localhost:5000/openapi/v1/openapi.json");

        response.EnsureSuccessStatusCode();

        var openApiJsonString = await response.Content.ReadAsStringAsync();

        var document = new OpenApiStringReader()
            .Read(openApiJsonString, out var diagnostic);

        // generate clients
        var basePath = Assembly.GetExecutingAssembly().Location;

        var settings = new GeneratorSettings(
            Namespace: "Hsds.Api",
            ClientName: "Hsds",
            TokenFolderName: default!,
            ConfigurationHeaderKey: default!,
            ExceptionType: "HsdsException",
            ExceptionCodePrefix: "H",
            Special_RefreshTokenSupport: false,
            Special_NexusFeatures: false);

        // generate C# client
        var csharpGenerator = new CSharpGenerator();
        var csharpCode = csharpGenerator.Generate(document, settings);

        var csharpOutputPath = $"{solutionRoot}src/clients/dotnet-client/HsdsClient.g.cs";
        File.WriteAllText(csharpOutputPath, csharpCode);

        // generate Python client
        var pythonGenerator = new PythonGenerator();
        var pythonCode = pythonGenerator.Generate(document, settings);

        var pythonOutputPath = $"{solutionRoot}src/clients/python-client/hsds_api/_hsds_api.py";
        File.WriteAllText(pythonOutputPath, pythonCode);

        // save open API document
        var openApiDocumentOutputPath = $"{solutionRoot}{openApiFileName}";
        File.WriteAllText(openApiDocumentOutputPath, openApiJsonString);
    }
}