using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Forwarded Headers for reverse proxy (IIS / Docker)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Enable CORS for testing
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = ".NET 8 Production Demo API",
        Version = "v1.0.0",
        Description = "Production-ready .NET Core 8 Web API deployed via Docker container on Windows Server VPS (`net.sujeetkumarsingh.com`). Includes interactive Swagger UI for live API testing.",
        Contact = new OpenApiContact
        {
            Name = "Sujeet Kumar Singh",
            Url = new Uri("https://net.sujeetkumarsingh.com")
        }
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var startTime = DateTime.UtcNow;

// Thread-safe in-memory store seeded with sample data
var products = new ConcurrentDictionary<int, Product>(new[]
{
    new KeyValuePair<int, Product>(1, new Product(1, "Cloud VPS Pro", "Infrastructure", 29.99m, 100, true, DateTime.UtcNow.AddDays(-10))),
    new KeyValuePair<int, Product>(2, new Product(2, ".NET 8 Microservice Kit", "Software", 49.50m, 250, true, DateTime.UtcNow.AddDays(-5))),
    new KeyValuePair<int, Product>(3, new Product(3, "Docker Enterprise License", "Containers", 99.00m, 50, true, DateTime.UtcNow.AddDays(-2))),
    new KeyValuePair<int, Product>(4, new Product(4, "API Gateway Shield", "Security", 19.99m, 500, true, DateTime.UtcNow.AddDays(-1)))
});
int nextId = 5;

var app = builder.Build();

app.UseForwardedHeaders();
app.UseCors();

// Enable Swagger and Swagger UI in ALL environments (including Production)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", ".NET 8 Demo API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = ".NET 8 Demo API - Swagger UI";
    options.EnableTryItOutByDefault();
    options.DisplayRequestDuration();
});

// Redirect root and /docs to Swagger UI for easy browser testing
app.MapGet("/", () => Results.Redirect("/swagger/index.html"))
   .ExcludeFromDescription();

app.MapGet("/docs", () => Results.Redirect("/swagger/index.html"))
   .ExcludeFromDescription();

// ==================== SYSTEM & HEALTH ENDPOINTS ====================

app.MapGet("/api/info", () => Results.Ok(new
{
    service = ".NET 8 Production Demo API",
    version = "1.0.0",
    domain = "net.sujeetkumarsingh.com",
    dockerHubImage = "singhkgp/net-demo-api:latest",
    framework = RuntimeInformation.FrameworkDescription,
    osDescription = RuntimeInformation.OSDescription,
    osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
    machineName = Environment.MachineName,
    environment = app.Environment.EnvironmentName,
    startedAtUtc = startTime,
    uptimeSeconds = Math.Round((DateTime.UtcNow - startTime).TotalSeconds, 2),
    swaggerUrl = "/swagger/index.html"
}))
.WithName("GetSystemInfo")
.WithTags("1. System & Health")
.WithSummary("Get API metadata, container runtime info, and uptime")
.WithOpenApi();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "DotNetDemoApi",
    timestampUtc = DateTime.UtcNow,
    totalProducts = products.Count
}))
.WithName("HealthCheck")
.WithTags("1. System & Health")
.WithSummary("Health check endpoint for container and reverse proxy monitoring")
.WithOpenApi();

// ==================== PRODUCTS CRUD ENDPOINTS ====================

app.MapGet("/api/products", (string? category, string? search) =>
{
    var query = products.Values.AsEnumerable();
    if (!string.IsNullOrWhiteSpace(category))
    {
        query = query.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }
    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                 p.Category.Contains(search, StringComparison.OrdinalIgnoreCase));
    }
    var list = query.OrderBy(p => p.Id).ToList();
    return Results.Ok(new
    {
        count = list.Count,
        items = list
    });
})
.WithName("GetProducts")
.WithTags("2. Products CRUD")
.WithSummary("List all products with optional category and search filters")
.WithOpenApi();

app.MapGet("/api/products/{id:int}", (int id) =>
{
    return products.TryGetValue(id, out var product)
        ? Results.Ok(product)
        : Results.NotFound(new { error = $"Product with ID {id} was not found." });
})
.WithName("GetProductById")
.WithTags("2. Products CRUD")
.WithSummary("Get a single product by its numeric ID")
.WithOpenApi();

app.MapPost("/api/products", (ProductCreateRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Product name is required." });
    }
    var id = Interlocked.Increment(ref nextId) - 1;
    var product = new Product(
        id,
        request.Name.Trim(),
        string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
        request.Price,
        request.Stock,
        request.InStock,
        DateTime.UtcNow
    );
    products[id] = product;
    return Results.Created($"/api/products/{id}", product);
})
.WithName("CreateProduct")
.WithTags("2. Products CRUD")
.WithSummary("Create a new product item")
.WithOpenApi();

app.MapPut("/api/products/{id:int}", (int id, ProductUpdateRequest request) =>
{
    if (!products.TryGetValue(id, out var existing))
    {
        return Results.NotFound(new { error = $"Product with ID {id} was not found." });
    }
    var updated = existing with
    {
        Name = string.IsNullOrWhiteSpace(request.Name) ? existing.Name : request.Name.Trim(),
        Category = string.IsNullOrWhiteSpace(request.Category) ? existing.Category : request.Category.Trim(),
        Price = request.Price ?? existing.Price,
        Stock = request.Stock ?? existing.Stock,
        InStock = request.InStock ?? existing.InStock
    };
    products[id] = updated;
    return Results.Ok(updated);
})
.WithName("UpdateProduct")
.WithTags("2. Products CRUD")
.WithSummary("Update an existing product by ID")
.WithOpenApi();

app.MapDelete("/api/products/{id:int}", (int id) =>
{
    return products.TryRemove(id, out var removed)
        ? Results.Ok(new { message = $"Product {id} deleted successfully.", deleted = removed })
        : Results.NotFound(new { error = $"Product with ID {id} was not found." });
})
.WithName("DeleteProduct")
.WithTags("2. Products CRUD")
.WithSummary("Delete a product by ID")
.WithOpenApi();

// ==================== WEATHER DEMO ENDPOINT ====================

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/api/weather", (int days = 5) =>
{
    var count = Math.Clamp(days, 1, 14);
    var forecast = Enumerable.Range(1, count).Select(index =>
        new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-10, 42),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return Results.Ok(forecast);
})
.WithName("GetWeatherForecast")
.WithTags("3. Utilities & Demo")
.WithSummary("Generate sample weather forecast data")
.WithOpenApi();

app.Run();

public record Product(
    int Id,
    string Name,
    string Category,
    decimal Price,
    int Stock,
    bool InStock,
    DateTime CreatedAtUtc
);

public record ProductCreateRequest(
    string Name,
    string Category = "General",
    decimal Price = 10.00m,
    int Stock = 10,
    bool InStock = true
);

public record ProductUpdateRequest(
    string? Name,
    string? Category,
    decimal? Price,
    int? Stock,
    bool? InStock
);

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

