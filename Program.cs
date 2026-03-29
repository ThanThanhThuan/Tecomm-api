using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Tecomm.Data;
using Tecomm.Middleware;
using Tecomm.Services;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════════
//  1. DEPENDENCY INJECTION — register all services with the DI container
// ═══════════════════════════════════════════════════════════════════════════════

// In-memory store is a singleton so state persists across requests
builder.Services.AddSingleton<InMemoryStore>();

// Business-logic services (scoped = one instance per HTTP request)
builder.Services.AddScoped<IProductService,   ProductService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IOrderService,     OrderService>();
builder.Services.AddScoped<IAuthService,      AuthService>();
builder.Services.AddScoped<IStatsService,     StatsService>();

// Controllers + JSON serialisation (pretty-print + enums as strings)
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.WriteIndented              = true;
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// ═══════════════════════════════════════════════════════════════════════════════
//  2. JWT AUTHENTICATION
// ═══════════════════════════════════════════════════════════════════════════════

var jwtKey = builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = builder.Configuration["Jwt:Issuer"],
        ValidAudience            = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew                = TimeSpan.Zero   // no grace period on expiry
    };

    // Return 401 JSON instead of the default HTML challenge
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            ctx.Response.StatusCode  = 401;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                """{"message":"You must be logged in to perform this action."}""");
        },
        OnForbidden = async ctx =>
        {
            ctx.Response.StatusCode  = 403;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(
                """{"message":"You do not have permission to perform this action."}""");
        }
    };
});

builder.Services.AddAuthorization();

// ═══════════════════════════════════════════════════════════════════════════════
//  3. SWAGGER / OPENAPI
// ═══════════════════════════════════════════════════════════════════════════════

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Tecomm Store API",
        Version     = "v1",
        Description = "RESTful API for the Tecomm online store — products, orders & inventory.",
        Contact     = new OpenApiContact { Name = "Tecomm Dev Team" }
    });

    // Add JWT bearer security definition so Swagger UI has an Authorize button
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.ApiKey,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter: Bearer {your-token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// CORS – allow the frontend dev server
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ═══════════════════════════════════════════════════════════════════════════════
//  4. BUILD THE APP
// ═══════════════════════════════════════════════════════════════════════════════

var app = builder.Build();

// ── Middleware pipeline (order matters!) ──────────────────────────────────────

// Custom request-logging middleware — first in, captures every request
app.UseRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tecomm API v1");
        c.DocumentTitle = "Tecomm API";
        c.DefaultModelsExpandDepth(-1); // collapse schemas by default
    });
}

app.UseDefaultFiles();   // serve index.html from wwwroot
app.UseStaticFiles();    // serve CSS / JS from wwwroot

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health-check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status    = "healthy",
    timestamp = DateTime.UtcNow,
    version   = "1.0.0"
}));

app.Run();
