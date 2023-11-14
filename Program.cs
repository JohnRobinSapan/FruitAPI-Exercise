using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<FruitDb>(opt => opt.UseInMemoryDatabase("FruitList"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddEndpointsApiExplorer();

// var settings = builder.Configuration.GetSection("Settings").Get<ApiKeyAuthenticationSchemeOptions>();

// builder.Services.AddAuthentication("ApiKey")
//     .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationSchemeHandler>(
//         "ApiKey",
//         opts => opts.ApiKey = settings.ApiKey
//     );
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Fruit API",
        Description = "API for managing a list of fruit their stock status.",
    });
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "ApiKey must appear in header",
        Type = SecuritySchemeType.ApiKey,
        Name = "X-API-KEY",
        In = ParameterLocation.Header,
        Scheme = "ApiKeyScheme"
    });
    var key = new OpenApiSecurityScheme()
    {
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "ApiKey"
        },
        In = ParameterLocation.Header
    };
    var requirement = new OpenApiSecurityRequirement
                    {
                             { key, new List<string>() }
                    };
    c.AddSecurityRequirement(requirement);
    // To implement swagger IDocumentFilter
    // c.DocumentFilter<ApiKeyEndpointFilter>();
});



builder.Services.AddTransient<IApiKeyValidation, ApiKeyValidation>();

var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<FruitDb>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add API key authentication middleware
// app.UseMiddleware<ApiKeyMiddleware>();

app.MapGet("/fruitlist", async (FruitDb db, HttpContext context) =>
    await db.Fruits.ToListAsync())
    .WithTags("Get all fruit").AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapGet("/fruitlist/instock", async (FruitDb db) =>
    await db.Fruits.Where(t => t.Instock).ToListAsync())
    .WithTags("Get all fruit that is in stock").AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapGet("/fruitlist/{id}", async (int id, FruitDb db) =>
    await db.Fruits.FindAsync(id)
        is Fruit fruit
            ? Results.Ok(fruit)
            : Results.NotFound())
    .WithTags("Get fruit by Id").AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapPost("/fruitlist", async (Fruit fruit, FruitDb db) =>
{
    db.Fruits.Add(fruit);
    await db.SaveChangesAsync();

    return Results.Created($"/fruitlist/{fruit.Id}", fruit);
})
    .WithTags("Add fruit to list").AddEndpointFilter<ApiKeyEndpointFilter>();

app.MapPut("/fruitlist/{id}", async (int id, Fruit inputFruit, FruitDb db) =>
{
    var fruit = await db.Fruits.FindAsync(id);

    if (fruit is null) return Results.NotFound();

    fruit.Name = inputFruit.Name;
    fruit.Instock = inputFruit.Instock;

    await db.SaveChangesAsync();

    return Results.NoContent();
})
    .WithTags("Update fruit by Id").AddEndpointFilter<ApiKeyEndpointFilter>();

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
    .WithTags("Delete fruit by Id").AddEndpointFilter<ApiKeyEndpointFilter>();



app.Run();