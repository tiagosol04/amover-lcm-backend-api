using API_AMOVER.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// --------------------
// Controllers + JSON
// --------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        // Evita problemas quando (por engano) devolves entidades EF com navegações cíclicas.
        o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// --------------------
// CORS
// --------------------
// Dev: aberto (Swagger, testes locais)
// Prod: restringir para as origens definidas em appsettings (Cors:AllowedOrigins)
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());

    options.AddPolicy("AppCors", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            // fallback seguro (se não configurarem, não abre tudo sem querer)
            policy.AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// --------------------
// DB
// --------------------
builder.Services.AddDbContext<LcmContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LCMDatabase")));

// --------------------
// JWT
// --------------------
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key");
var jwtIssuer = jwtSection.GetValue<string>("Issuer");
var jwtAudience = jwtSection.GetValue<string>("Audience");

if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Falta configurar Jwt:Key no appsettings.json");
if (string.IsNullOrWhiteSpace(jwtIssuer))
    throw new InvalidOperationException("Falta configurar Jwt:Issuer no appsettings.json");
if (string.IsNullOrWhiteSpace(jwtAudience))
    throw new InvalidOperationException("Falta configurar Jwt:Audience no appsettings.json");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

// Todos os endpoints exigem token por defeito.
// Só endpoints com [AllowAnonymous] ficam públicos (ex.: /api/auth/login e /api/health)
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// --------------------
// Swagger (com botão Authorize)
// --------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API_AMOVER", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Insere: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // Referência correta para aparecer e aplicar em todas as operações
    var securitySchemeRef = new OpenApiSecurityScheme
    {
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securitySchemeRef, Array.Empty<string>() }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Em DEV (emulador + HTTP), não redirecionar.
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseCors(app.Environment.IsDevelopment() ? "DevCors" : "AppCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
