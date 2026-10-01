using OcharoBrand.Api.Configurations;

var builder = WebApplication.CreateBuilder(args);
builder.Configure();

var app = builder.Build();
app.Configure();

await app.InitializeAsync();

app.Run();

/// <summary>Entry point marker for in-memory E2E hosting.</summary>
public partial class Program;
