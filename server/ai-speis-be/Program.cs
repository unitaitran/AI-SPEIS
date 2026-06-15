using Microsoft.EntityFrameworkCore;
using ai_speis_be.Models;
using ai_speis_be.Repositories.UserRepo;
using ai_speis_be.Repositories.CVRepo;
using ai_speis_be.Services.UserService;
using ai_speis_be.Services.TokenService;
using ai_speis_be.Services.EmailService;
using ai_speis_be.Services.CVService;
using ai_speis_be.Services.FileValidatorService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.OpenApi.Models;

LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
    
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    const string securitySchemeName = "Bearer";

    options.AddSecurityDefinition(securitySchemeName, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT token. Swagger will add the 'Bearer' prefix automatically.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = securitySchemeName
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<ICVRepository, CVRepository>();
builder.Services.AddScoped<IFileValidatorService, FileValidatorService>();
builder.Services.AddScoped<ICVService, CVService>();

var googleCookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;
    
var googleCookieSameSite = builder.Environment.IsDevelopment()
    ? SameSiteMode.Lax
    : SameSiteMode.None;

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
//thêm cookie để lưu thông tin trước khi controller xử lý
.AddCookie("External", options =>
{
    options.Cookie.SecurePolicy = googleCookieSecurePolicy;
    options.Cookie.SameSite = googleCookieSameSite;
})
.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
{
    // lưu thông tin user vào cookie sau khi login thành công
    options.SignInScheme = "External";
    options.ClientId = GetRequiredConfiguration(builder.Configuration, "Authentication:Google:ClientId");
    options.ClientSecret = GetRequiredConfiguration(builder.Configuration, "Authentication:Google:ClientSecret");
    options.CallbackPath = "/api/Authentication/oauth/google/callback";
    options.CorrelationCookie.SecurePolicy = googleCookieSecurePolicy;
    options.CorrelationCookie.SameSite = googleCookieSameSite;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(GetRequiredConfiguration(builder.Configuration, "Jwt:Key")))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string GetRequiredConfiguration(IConfiguration configuration, string key)
{
    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"{key} is missing. Add it to .env or environment variables.");
    }

    return value;
}

static void LoadEnvFile()
{
    var envPath = FindEnvFile();
    if (envPath is null)
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(envPath))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            continue;
        }

        var equalsIndex = line.IndexOf('=');
        if (equalsIndex <= 0)
        {
            continue;
        }

        var key = line[..equalsIndex].Trim();
        var value = line[(equalsIndex + 1)..].Trim().Trim('"');

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

static string? FindEnvFile()
{
    foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            var envPath = Path.Combine(directory.FullName, ".env");
            if (File.Exists(envPath))
            {
                return envPath;
            }

            directory = directory.Parent;
        }
    }

    return null;
}
