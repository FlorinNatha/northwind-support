using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application;
using SupportDesk.Application.Abstractions;
using SupportDesk.Infrastructure;
using SupportDesk.Infrastructure.Configuration;
using SupportDesk.Presentation.Extensions;
using SupportDesk.Presentation.Filters;
using SupportDesk.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "frontend";

builder.Services
    .AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(options =>
    {
        // Enums travel as their names, both ways, so the API reads well from the browser.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options => options.AddPolicy(
    FrontendCorsPolicy,
    policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var slaOptions = builder.Configuration.GetSection(SupportDesk.Domain.Aggregates.Tickets.SlaOptions.SectionName)
    .Get<SupportDesk.Domain.Aggregates.Tickets.SlaOptions>() ?? new SupportDesk.Domain.Aggregates.Tickets.SlaOptions();
builder.Services.AddSingleton(slaOptions);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Central authorization policy (AU-6): require authenticated user by default on all endpoints.
    // Endpoints that should be public (like /api/auth/login) are marked with [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Keeps a developer machine one command away from a working database.
    await app.MigrateAndSeedAsync();
}

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
