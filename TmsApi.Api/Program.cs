using Asp.Versioning;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using TmsApi.Application.Interfaces;
using TmsApi.Api.Filters;
using TmsApi.Api.Middleware;
using MediatR;
using FluentValidation;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Api.ExceptionHandlers;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using TmsApi.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});

// Configure OpenAPI v1 & v2 documents
builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = description => description.GroupName == "v1";
});
builder.Services.AddOpenApi("v2", options =>
{
    options.ShouldInclude = description => description.GroupName == "v2";
});

// Configure API Versioning with URL segment reader
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

//configuring data base connection and logging
builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
           .LogTo(Console.WriteLine, LogLevel.Information)
           .EnableSensitiveDataLogging());



builder.Services.AddProblemDetails();

builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

// Add MediatR & FluentValidation
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly));

builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);

// LoggingBehavior FIRST so it wraps ValidationBehavior
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Configure Rate Limiting Policies
builder.Services.AddRateLimiter(options =>
{
    // Return HTTP 429 Too Many Requests when limits are exceeded
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Policy 1: Fixed Window for general public API queries (e.g., Course listings)
    options.AddFixedWindowLimiter("fixed-by-ip", opt =>
    {
        opt.PermitLimit = 60; // Max 60 requests
        opt.Window = TimeSpan.FromMinutes(1); // Per 1 minute window
        opt.QueueLimit = 2; // Queue up to 2 extra requests before rejecting
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Policy 2: Strict Concurrency Limiter for resource-intensive POST mutations (e.g., Enrollment)
    options.AddConcurrencyLimiter("strict-concurrency", opt =>
    {
        opt.PermitLimit = 5; // Max 5 concurrent active requests
        opt.QueueLimit = 0; // Reject immediately if limits are exceeded
    });
});

// Configure Output Caching
builder.Services.AddOutputCache(options =>
{
    // Base policy: Cache successful GET requests for 60 seconds
    options.AddBasePolicy(builder => 
        builder.Expire(TimeSpan.FromSeconds(60)));

    // Named policy specifically for course listings: vary by page & pageSize query params
    options.AddPolicy("CoursesCachePolicy", builder =>
        builder.Expire(TimeSpan.FromSeconds(30))
               .SetVaryByQuery("page", "pageSize"));
});

// Global Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

//frontend connection
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Load allowed origins from appsettings.Development.json
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

// Register the named CORS policy in Dependency Injection
builder.Services.AddCors(options =>
{
    options.AddPolicy("TmsClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials() // Vital for HttpOnly auth cookies in later sessions
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// Register Antiforgery service
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

//Register SignalR Services
builder.Services.AddSignalR();

//================================================================//
//              start of middleware pipline
//================================================================//

var app = builder.Build();

app.UseExceptionHandler();
app.UseHsts();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("TMS API Reference")
               .WithTheme(ScalarTheme.DeepSpace)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        
        options.AddDocument("v1", "API Version 1.0")
               .AddDocument("v2", "API Version 2.0");
    });
}

app.UseStatusCodePages();
app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("TmsClient");

// Standard ASP.NET Core Auth pipeline (prepares context.User for Module 12)
app.UseAuthentication();
app.UseAuthorization();

// Issue readable XSRF-TOKEN cookie when tms_auth cookie is present
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true || context.Request.Cookies.ContainsKey("tms_auth"))
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(context);

        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // MUST be false so Angular JavaScript can read it!
            Secure = !app.Environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict
        });
    }

    await next(context);
});

app.UseRateLimiter();
app.UseOutputCache();

app.UseMiddleware<V1DeprecationMiddleware>();

app.MapControllers();
app.MapHub<TmsHub>("/hubs/tms").RequireCors("TmsClient");

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    await context.Database.MigrateAsync();

    if (app.Environment.IsDevelopment())
    {
        await DataSeeder.SeedAsync(context);
    }
}

app.Run();