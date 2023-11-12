using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<FruitDb>(opt => opt.UseInMemoryDatabase("FruitList"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddEndpointsApiExplorer();

var settings = builder.Configuration.GetSection("Settings").Get<ApiKeyAuthenticationSchemeOptions>();

builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationSchemeHandler>(
        "ApiKey",
        opts => opts.ApiKey = settings.ApiKey
    );
builder.Services.AddSwaggerGen(options =>
{

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Name = "X-API-KEY",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey",
        Description = "API key needed to access the endpoints."
    });
    // Add a security requirement for the "ApiKey" scheme
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>() // The scopes, if any
        }
    });
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Fruit API",
        Description = "API for managing a list of fruit their stock status.",
    });
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<FruitDb>();
    dbContext.Database.EnsureCreated();
}

// Add API key authentication middleware
app.Use(async (context, next) =>
{
    // Check for the presence of the API key in the request headers
    if (!context.Request.Headers.TryGetValue("X-API-KEY", out var apiKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("API key is missing.");
        return;
    }

    // Retrieve the expected API key from configuration
    var settings = context.RequestServices.GetRequiredService<IConfiguration>()
        .GetSection("Settings")
        .Get<ApiKeyAuthenticationSchemeOptions>();
    var expectedApiKey = settings.ApiKey;

    // Compare the provided API key with the expected API key
    if (apiKey != expectedApiKey)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Invalid API key.");
        return;
    }

    // If the API key is valid, continue to the next middleware in the pipeline
    await next();
});

app.MapGet("/fruitlist", async (FruitDb db, HttpContext context) =>
    await db.Fruits.ToListAsync())
    .WithTags("Get all fruit");

app.MapGet("/fruitlist/instock", async (FruitDb db) =>
    await db.Fruits.Where(t => t.Instock).ToListAsync())
    .WithTags("Get all fruit that is in stock");

app.MapGet("/fruitlist/{id}", async (int id, FruitDb db) =>
    await db.Fruits.FindAsync(id)
        is Fruit fruit
            ? Results.Ok(fruit)
            : Results.NotFound())
    .WithTags("Get fruit by Id");

app.MapPost("/fruitlist", async (Fruit fruit, FruitDb db) =>
{
    db.Fruits.Add(fruit);
    await db.SaveChangesAsync();

    return Results.Created($"/fruitlist/{fruit.Id}", fruit);
})
    .WithTags("Add fruit to list");

app.MapPut("/fruitlist/{id}", async (int id, Fruit inputFruit, FruitDb db) =>
{
    var fruit = await db.Fruits.FindAsync(id);

    if (fruit is null) return Results.NotFound();

    fruit.Name = inputFruit.Name;
    fruit.Instock = inputFruit.Instock;

    await db.SaveChangesAsync();

    return Results.NoContent();
})
    .WithTags("Update fruit by Id");

app.MapDelete("/fruitlist/{id}", async (int id, FruitDb db) =>
{
    if (await db.Fruits.FindAsync(id) is Fruit fruit)
    {
        db.Fruits.Remove(fruit);
        await db.SaveChangesAsync();
        return Results.Ok(fruit);
    }

    return Results.NotFound();
})
    .WithTags("Delete fruit by Id");


app.UseSwagger();
app.UseSwaggerUI();

app.Run();