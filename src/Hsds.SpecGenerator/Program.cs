var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument();
builder.Services.AddRouting(options => options.LowercaseUrls = true);

var app = builder.Build();

app.UseOpenApi(settings => settings.Path = "/openapi/{documentName}/openapi.json");
app.UseSwaggerUi3();

app.MapControllers();

app.Run();
