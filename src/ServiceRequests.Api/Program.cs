using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceRequests.Api.Data;
using ServiceRequests.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));

// Caminho estável, relativo ao projeto, independentemente do diretório do terminal.
var databasePath = builder.Configuration["Database:Path"] ?? "data/requests.db";
databasePath = Path.GetFullPath(databasePath, builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
var connection = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
builder.Services.AddDbContext<RequestsDbContext>(options => options.UseSqlite(connection));

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapRequests();
await app.RunAsync();
