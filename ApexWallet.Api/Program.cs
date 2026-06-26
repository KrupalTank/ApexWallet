using Serilog;
using Microsoft.EntityFrameworkCore;
using ApexWallet.Api.Database.Models;
using ApexWallet.Api.Middlewares;
using ApexWallet.Api.Security;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog to write to both the Console and a daily rolling Text File
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/apexwallet-flight-recorder-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add Framework Controllers
builder.Services.AddControllers();

// Register .NET 10's Native OpenAPI support
builder.Services.AddOpenApi();

// 📌 PLACE THIS INSIDE PROGRAM.CS TO PARSE RENDER ONSITE CONNECTIVITY KEYS

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

// If running locally, fall back to the appsettings configuration string
if (string.IsNullOrEmpty(connectionString))
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
}
else
{
    // Neon connection strings use the "postgres://" identifier.
    // .NET's Npgsql provider strictly prefers "Host=", so we normalize it if needed:
    if (connectionString.StartsWith("postgres://"))
    {
        var databaseUri = new Uri(connectionString);
        var userInfo = databaseUri.UserInfo.Split(':');

        connectionString = $"Host={databaseUri.Host};Port={databaseUri.Port};Database={databaseUri.LocalPath.TrimStart('/')};Username={userInfo[0]};Password={userInfo[1]};SSL Mode=Require;Trust Server Certificate=True;";
    }
}

builder.Services.AddDbContext<ApexWallet.Api.Database.Models.ApexWalletDbContext>(options =>
    options.UseNpgsql(connectionString));


// Register Cryptography Layer
builder.Services.AddSingleton<ICryptoService, AesCryptoService>();

//builder.Services.AddOpenApi();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new ArgumentNullException("JWT Key is missing."));

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
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ClockSkew = TimeSpan.Zero // Removes the default 5-minute grace period for expired tokens
    };
});

// Automatically register all FluentValidation classes in the project
// Explicitly register our workflow validators to guarantee DI activation
builder.Services.AddScoped<FluentValidation.IValidator<ApexWallet.Api.Modules.WalletModule.TransferDto>, ApexWallet.Api.Modules.Validation.TransferValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<ApexWallet.Api.Modules.UserModule.UpdateProfileDto>, ApexWallet.Api.Modules.Validation.UpdateProfileValidator>();
builder.Services.AddScoped<FluentValidation.IValidator<ApexWallet.Api.Modules.WalletModule.DepositDto>, ApexWallet.Api.Modules.Validation.DepositValidator>();
builder.Services.AddScoped<ApexWallet.Api.Filters.AccountStatusCheckFilter>();

// Register Core Email Notification Engine
builder.Services.AddScoped<ApexWallet.Api.Services.IEmailService, ApexWallet.Api.Services.EmailService>();

// Configure Enterprise API Versioning
var apiVersioningBuilder = builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader(); // Reads version from URL: /api/v1/
});

// Correctly chain the ApiExplorer onto the versioning builder instance
apiVersioningBuilder.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

//CORS
// Allow our frontend interface to securely communicate with the API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();


// ACTIVATE CUSTOM MIDDLEWARE (Always place your logging middleware at the very front!)
app.UseMiddleware<BlackBoxLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
// Configure the HTTP Request Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseStaticFiles();
app.UseCors("AllowAllFrontend");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();