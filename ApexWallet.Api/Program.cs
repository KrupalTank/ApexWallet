using ApexWallet.Api.Database.Models;
using ApexWallet.Api.Middlewares;
using ApexWallet.Api.Security;
using ApexWallet.Api.Services;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

// 1. 🚀 INITIALIZE DOTNET_ENV BOOTSTRAPPER BEFORE ANYTHING ELSE RUNS
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Tell .NET to prioritize system environment variables (and our loaded .env parameters)
builder.Configuration.AddEnvironmentVariables();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/apexwallet-flight-recorder-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var connectionString = "";

// 1. If running locally in development, look for your local postgres instance
if (builder.Environment.IsDevelopment())
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}

// 2. If local string is empty (or we are in production), look for the .env/environment string
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = builder.Configuration["DATABASE_URL"];

    // Safety check: If Render or a cloud provider passes a raw URI starting with postgres://, convert it.
    // Otherwise, if it already starts with "Host=", it passes straight through perfectly!
    if (!string.IsNullOrEmpty(connectionString) && connectionString.StartsWith("postgres://"))
    {
        var databaseUri = new Uri(connectionString);
        var userInfo = databaseUri.UserInfo.Split(':');
        connectionString = $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseUri.LocalPath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=True;";
    }
}

builder.Services.AddDbContext<ApexWalletDbContext>(options =>
    options.UseNpgsql(connectionString));


// 3. 🛡️ REGISTER RE-ALIGNED CRYPTOGRAPHY ENVIRONMENT VALUES
builder.Services.AddSingleton<ICryptoService, AesCryptoService>();

// 4. 🎟️ CONFIGURE SECURITY JWT INFRASTRUCTURE
var secretKeyString = builder.Configuration["JwtSettings:Key"] ?? throw new ArgumentNullException("JWT Token signature key path resolution fault.");
var secretKey = Encoding.UTF8.GetBytes(secretKeyString);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ClockSkew = TimeSpan.Zero
    };
});

// Validators & Filtering registration pipelines
builder.Services.AddScoped<IValidator<ApexWallet.Api.Modules.WalletModule.TransferDto>, ApexWallet.Api.Modules.Validation.TransferValidator>();
builder.Services.AddScoped<IValidator<ApexWallet.Api.Modules.UserModule.UpdateProfileDto>, ApexWallet.Api.Modules.Validation.UpdateProfileValidator>();
builder.Services.AddScoped<IValidator<ApexWallet.Api.Modules.WalletModule.DepositDto>, ApexWallet.Api.Modules.Validation.DepositValidator>();
builder.Services.AddScoped<ApexWallet.Api.Filters.AccountStatusCheckFilter>();

builder.Services.AddScoped<IEmailService, EmailService>();

// API Versioning configurations
var apiVersioningBuilder = builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

apiVersioningBuilder.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllFrontend", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseMiddleware<BlackBoxLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("AllowAllFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();