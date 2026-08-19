using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.SqlServer;
using ai_speis_be.BackgroundJobs;
using ai_speis_be.Models;
using ai_speis_be.Repositories.UserRepo;
using ai_speis_be.Repositories.CVRepo;
using ai_speis_be.Services.UserService;
using ai_speis_be.Services.AdminDashboardService;
using ai_speis_be.Services.TokenService;
using ai_speis_be.Services.EmailService;
using ai_speis_be.Services.CVService;
using ai_speis_be.Services.FileValidatorService;
using ai_speis_be.Services.PdfExtractorService;
using ai_speis_be.Services.GeminiAiParsingService;
using ai_speis_be.Services.BackgroundWorker;
using ai_speis_be.Repositories.QuestionRepo;
using ai_speis_be.Services.QuestionService;
using ai_speis_be.Repositories.SavedQuestionRepo;
using ai_speis_be.Services.SavedQuestionService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.OpenApi.Models;
using ai_speis_be.Services.JDService;
using ai_speis_be.Repositories.JDRepo;
using ai_speis_be.Repositories.InterviewCampaignRepo;
using ai_speis_be.Services.InterviewSessionService;
using ai_speis_be.Services.SpeechToTextService;
using ai_speis_be.Services.TextToSpeechService;
using ai_speis_be.Repositories.CodingRepo;
using ai_speis_be.Services.CodingService;
using ai_speis_be.Services.CodingService.Harness;
using ai_speis_be.Services.CodingService.Selection;
using ai_speis_be.Services.Judge0Service;
using ai_speis_be.Repositories.PaymentRepo;
using ai_speis_be.Services.PaymentService;
using ai_speis_be.Services.SubscriptionPlanService;
using ai_speis_be.Services.RewardService;
using ai_speis_be.Services.SubscriptionService;
using ai_speis_be.Services.NotificationService;
using ai_speis_be.Hubs;
using ai_speis_be.BehaviouralInterviews.AI;
using ai_speis_be.BehaviouralInterviews.Configuration;
using ai_speis_be.BehaviouralInterviews.Orchestration;
using ai_speis_be.BehaviouralInterviews.Rubrics;
using ai_speis_be.BehaviouralInterviews.Scoring;
using ai_speis_be.BehaviouralInterviews.Selection;
using ai_speis_be.BehaviouralInterviews.Validation;
using ai_speis_be.AI.Json;
using ai_speis_be.TechnicalInterviews.AI;
using ai_speis_be.TechnicalInterviews.Configuration;
using ai_speis_be.TechnicalInterviews.Planning;
using ai_speis_be.TechnicalInterviews.Rubrics;
using ai_speis_be.TechnicalInterviews.Scoring;
using ai_speis_be.TechnicalInterviews.Selection;
using ai_speis_be.TechnicalInterviews.Validation;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddSingleton<IAiJsonRecoveryService, AiJsonRecoveryService>();
builder.Services.AddSignalR();
    
builder.Services.AddHttpClient();
var technicalInterviewOptions = TechnicalInterviewOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(technicalInterviewOptions);
builder.Services.AddHttpClient("TechnicalInterviewAI", client =>
{
    // Each interview AI operation owns its timeout and retry budget.
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("technical-interview", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst("UserId")?.Value
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
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

var allowedOriginsConfig = builder.Configuration["Cors:AllowedOrigins"]
    ?? builder.Configuration["ALLOWED_ORIGINS"]
    ?? "https://aispeis.site,https://www.aispeis.site,http://localhost:3000,http://localhost:5173";

var allowedOrigins = allowedOriginsConfig
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", corsBuilder =>
    {
        if (allowedOrigins.Contains("*"))
        {
            corsBuilder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
        }
        else
        {
            corsBuilder.WithOrigins(allowedOrigins)
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials();
        }
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.")));

var hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found for Hangfire.");
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(hangfireConnectionString, new SqlServerStorageOptions
    {
        PrepareSchemaIfNecessary = true,
        QueuePollInterval = TimeSpan.FromSeconds(15),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5)
    }));
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Math.Max(1, builder.Configuration.GetValue<int?>("Hangfire:WorkerCount") ?? 4);
    options.Queues = new[] { "default", "payments", "subscriptions", "notifications" };
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailSender, EmailService>();
builder.Services.AddScoped<ICVRepository, CVRepository>();
builder.Services.AddScoped<IFileValidatorService, FileValidatorService>();
builder.Services.AddScoped<ICVService, CVService>();
builder.Services.AddScoped<IPdfExtractorService, PdfExtractorService>();
builder.Services.AddScoped<IGeminiAiParsingService, GeminiAiParsingService>();
builder.Services.AddScoped<IJDService, JDService>();
builder.Services.AddScoped<IJDRepository, JDRepository>();
builder.Services.AddScoped<ISpeechToTextService, SpeechToTextService>();
builder.Services.AddScoped<ITextToSpeechService, TextToSpeechService>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
builder.Services.AddScoped<IRewardService, RewardService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<ISubscriptionMaintenanceService, SubscriptionMaintenanceService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationEmailDeliveryService, NotificationEmailDeliveryService>();
builder.Services.AddScoped<INotificationEventPublisher, NotificationEventPublisher>();
builder.Services.AddScoped<IAdminNotificationPublisher, AdminNotificationPublisher>();
builder.Services.AddScoped<INotificationRealtimeNotifier, NotificationRealtimeNotifier>();

// Background Worker for CV Parsing
builder.Services.AddSingleton<ICvParseQueue, CvParseQueue>();
builder.Services.AddHostedService<CvParsingBackgroundService>();

// Background Worker for JD Parsing
builder.Services.AddSingleton<IJdParseQueue, JdParseQueue>();
builder.Services.AddHostedService<JdParsingBackgroundService>();
builder.Services.AddHostedService<PendingPaymentExpiryBackgroundService>();
builder.Services.AddHostedService<AdminOperationalNotificationBackgroundService>();

// Register Question Bank
builder.Services.AddScoped<IQuestionRepoitory, QuestionRepository>();
builder.Services.AddScoped<IQuestionService, QuestionService>();

// Register Saved Questions
builder.Services.AddScoped<ISavedQuestionRepository, SavedQuestionRepository>();
builder.Services.AddScoped<ISavedQuestionService, SavedQuestionService>();

// Register Interview Session & Campaign
builder.Services.AddScoped<IInterviewCampaignRepository, InterviewCampaignRepository>();
builder.Services.AddScoped<IInterviewSessionService, InterviewSessionService>();

// Register Judge0 Code Execution
builder.Services.AddHttpClient("Judge0", client =>
{
    var baseUrl = builder.Configuration["Judge0:BaseUrl"]
        ?? builder.Configuration["JUDGE0_BASE_URL"]
        ?? builder.Configuration["Judge0__BaseUrl"]
        ?? "http://localhost:2358";
    client.BaseAddress = new Uri(baseUrl);
    var timeoutSeconds = int.TryParse(builder.Configuration["Judge0:TimeoutSeconds"], out var t) ? t : 30;
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});

builder.Services.AddScoped<IJudge0Service, Judge0Service>();
builder.Services.AddScoped<ICodingRepository, CodingRepository>();
builder.Services.AddScoped<ICodingQuestionSelectionService, CodingQuestionSelectionService>();
builder.Services.AddSingleton<ICodingHarnessEngine, CodingHarnessEngine>();
builder.Services.AddScoped<ICodingService, CodingService>();
// Register Behavioural Interview Components
var behaviouralOptions = BehaviouralInterviewOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(behaviouralOptions);
builder.Services.AddHttpClient("BehaviouralInterviewAI", client =>
{
    client.Timeout = TimeSpan.FromSeconds(behaviouralOptions.TimeoutSeconds);
});
builder.Services.AddScoped<ExternalBehaviouralInterviewAIProvider>();
builder.Services.AddScoped<IBehaviouralInterviewAIProvider, GeminiBehaviouralInterviewAIProvider>();
builder.Services.AddScoped<IBehaviouralInterviewAIProvider, OllamaBehaviouralInterviewAIProvider>();
builder.Services.AddSingleton<IBehaviouralRubricProvider, BehaviouralRubricProvider>();
builder.Services.AddSingleton<IBehaviouralRubricScoringService, BehaviouralRubricScoringService>();
builder.Services.AddSingleton<IBehaviouralAIResponseValidator, BehaviouralAIResponseValidator>();
builder.Services.AddScoped<IBehaviouralInterviewAIProviderResolver, BehaviouralInterviewAIProviderResolver>();
builder.Services.AddScoped<IBehaviouralQuestionSelectionService, BehaviouralQuestionSelectionService>();
builder.Services.AddSingleton<IBehaviouralFollowUpDecisionEngine, BehaviouralFollowUpDecisionEngine>();
builder.Services.AddScoped<IBehaviouralInterviewOrchestrator, BehaviouralInterviewOrchestrator>();

// Technical Interview module
builder.Services.AddSingleton<ITechnicalRubricProvider, TechnicalRubricProvider>();
builder.Services.AddSingleton<ITechnicalAIConcurrencyGate, TechnicalAIConcurrencyGate>();
builder.Services.AddScoped<ExternalTechnicalInterviewAIProvider>();
builder.Services.AddScoped<ITechnicalInterviewAIProvider, GeminiTechnicalInterviewAIProvider>();
builder.Services.AddScoped<ITechnicalInterviewAIProvider, OllamaTechnicalInterviewAIProvider>();
builder.Services.AddScoped<ITechnicalInterviewAIProviderResolver, TechnicalInterviewAIProviderResolver>();
builder.Services.AddScoped<ITechnicalAIResponseValidator, TechnicalAIResponseValidator>();
builder.Services.AddScoped<ITechnicalRubricScoringService, TechnicalRubricScoringService>();
builder.Services.AddScoped<ITechnicalQuestionPlanBuilder, TechnicalQuestionPlanBuilder>();
builder.Services.AddScoped<ITechnicalQuestionOrderRandomizer, TechnicalQuestionOrderRandomizer>();
builder.Services.AddScoped<ITechnicalQuestionSelectionService, TechnicalQuestionSelectionService>();
builder.Services.AddScoped<ai_speis_be.TechnicalInterviews.V2.ITechnicalV2InterviewOrchestrator, ai_speis_be.TechnicalInterviews.V2.TechnicalV2InterviewOrchestrator>();
builder.Services.AddScoped<ai_speis_be.Services.ISingleQuestionRetryService, ai_speis_be.Services.SingleQuestionRetryService>();

// Google Cloud Quota & Billing Cost Monitoring
builder.Services.AddSingleton<ai_speis_be.Services.GoogleQuotaService.GoogleQuotaConfig>();
builder.Services.AddSingleton<ai_speis_be.Services.GoogleQuotaService.IGoogleQuotaService,
    ai_speis_be.Services.GoogleQuotaService.GoogleQuotaService>();
builder.Services.AddSingleton<ai_speis_be.Services.GoogleQuotaService.ICloudCostService,
    ai_speis_be.Services.GoogleQuotaService.CloudCostService>();

// Admin Payment Service
builder.Services.AddScoped<ai_speis_be.Services.AdminPaymentService.IAdminPaymentService, ai_speis_be.Services.AdminPaymentService.AdminPaymentService>();
builder.Services.AddScoped<IAdminDashboardService, ai_speis_be.Services.AdminDashboardService.AdminDashboardService>();

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
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrWhiteSpace(accessToken)
                && context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var userIdClaim = context.Principal?.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                context.Fail("Thiếu hoặc sai định danh người dùng trong token.");
                return;
            }

            var userRepository = context.HttpContext.RequestServices
                .GetRequiredService<IUserRepository>();
            var user = await userRepository.GetUserByIdAsync(
                userId,
                context.HttpContext.RequestAborted);

            if (user is null || !user.Status || user.IsLocked)
            {
                context.Fail("Tài khoản người dùng chưa kích hoạt hoặc đã bị khóa.");
            }
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Database migration notice: {ex.Message}");
    }

    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InterviewCampaign]') AND name = N'DashboardMetricsJson')
            BEGIN
                ALTER TABLE [dbo].[InterviewCampaign] ADD [DashboardMetricsJson] nvarchar(max) NULL;
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[InterviewCampaign]') AND name = N'OverallScore')
            BEGIN
                ALTER TABLE [dbo].[InterviewCampaign] ADD [OverallScore] decimal(5,2) NULL;
            END;

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = N'UserSkillScore')
            BEGIN
                CREATE TABLE [dbo].[UserSkillScore] (
                    [UserSkillScoreId] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [UserId] INT NOT NULL,
                    [InterviewCampaignId] INT NULL,
                    [InterviewSessionId] INT NULL,
                    [SkillCode] NVARCHAR(100) NOT NULL,
                    [SkillName] NVARCHAR(200) NOT NULL,
                    [Score] DECIMAL(5,2) NOT NULL,
                    [SessionTitle] NVARCHAR(255) NULL,
                    [EvaluatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT [FK_UserSkillScore_User] FOREIGN KEY ([UserId]) REFERENCES [User]([UserId]) ON DELETE CASCADE,
                    CONSTRAINT [FK_UserSkillScore_InterviewCampaign] FOREIGN KEY ([InterviewCampaignId]) REFERENCES [InterviewCampaign]([InterviewCampaignId]),
                    CONSTRAINT [FK_UserSkillScore_InterviewSession] FOREIGN KEY ([InterviewSessionId]) REFERENCES [InterviewSession]([InterviewSessionId])
                );
            END;

            IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TechnicalAnswer]') AND name = N'AiApplicationScore')
            BEGIN
                ALTER TABLE [dbo].[TechnicalAnswer] ADD [AiApplicationScore] decimal(18,2) NULL;
            END;
        ");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Column check notice: {ex.Message}");
    }
}

// Recurring registration occurs after migrations so the application's SQL Server is
// ready. Hangfire creates and maintains its own schema in the same SQL Server storage.
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<Hangfire.IRecurringJobManager>();

    var subscriptionExpiryCron = builder.Configuration["BackgroundJobs:SubscriptionExpiryCron"] ?? "10 0 * * *";
    recurringJobManager.AddOrUpdate<SubscriptionExpiryJob>(
        "job-02-subscription-expiry",
        job => job.ExecuteAsync(),
        subscriptionExpiryCron,
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    var quotaResetCron = builder.Configuration["BackgroundJobs:QuotaResetCron"] ?? "5 0 1 * *";
    recurringJobManager.AddOrUpdate<QuotaResetJob>(
        "job-03-quota-reset",
        job => job.ExecuteAsync(),
        quotaResetCron,
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    var notificationRetryCron = builder.Configuration["BackgroundJobs:NotificationRetryCron"] ?? "20 0 * * *";
    recurringJobManager.AddOrUpdate<NotificationRetryJob>(
        "job-04-notification-retry",
        job => job.ExecuteAsync(),
        notificationRetryCron,
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    var questionPurgeCron = builder.Configuration["BackgroundJobs:QuestionPurgeCron"] ?? "30 0 * * *";
    recurringJobManager.AddOrUpdate<QuestionPurgeJob>(
        "job-05-question-purge",
        job => job.ExecuteAsync(),
        questionPurgeCron,
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var exceptionHandlerPathFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;

            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(exception, "Unhandled Exception occurred in Production: {Message}", exception?.Message);

            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                Message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.",
                StatusCode = 500
            });

            await context.Response.WriteAsync(result);
        });
    });
}

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseWebSockets();

// Catch Google OAuth Correlation failed errors and redirect gracefully
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex) when (ex is Microsoft.AspNetCore.Authentication.AuthenticationFailureException
        || (ex.InnerException is Microsoft.AspNetCore.Authentication.AuthenticationFailureException))
    {
        var loginUrl = app.Configuration["Frontend:LoginUrl"]
            ?? app.Configuration["FRONTEND_LOGIN_URL"]
            ?? "http://localhost:3000/#login";
        context.Response.Redirect(loginUrl + "?status=error&message=" + Uri.EscapeDataString("Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."));
    }
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

static string GetRequiredConfiguration(IConfiguration configuration, string key)
{
    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        var altKey = key.Replace(":", "__");
        value = configuration[altKey];
    }
    if (string.IsNullOrWhiteSpace(value) && key.Equals("Jwt:Key", StringComparison.OrdinalIgnoreCase))
    {
        value = configuration["JWT_KEY"];
    }

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"FATAL: Required configuration '{key}' is missing. Add it to .env or environment variables.");
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

public partial class Program { }
