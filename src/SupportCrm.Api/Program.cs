using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SupportCrm.Api.Security;
using SupportCrm.Application.Ai;
using SupportCrm.Application.CustomerPortal;
using SupportCrm.Application.Customers;
using SupportCrm.Application.Security;
using SupportCrm.Infrastructure;
using SupportCrm.Infrastructure.Persistence;
using SupportCrm.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// --- Options ------------------------------------------------------------------------------
builder.Services.Configure<AiFeaturesOptions>(builder.Configuration.GetSection(AiFeaturesOptions.SectionName));
builder.Services.Configure<CustomerPortalOptions>(builder.Configuration.GetSection(CustomerPortalOptions.SectionName));
builder.Services.Configure<AttachmentOptions>(builder.Configuration.GetSection(AttachmentOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));

builder.Services.Configure<LocalDiskAttachmentStorageOptions>(builder.Configuration.GetSection(LocalDiskAttachmentStorageOptions.SectionName));
builder.Services.Configure<LocalDiskArticleAttachmentStorageOptions>(builder.Configuration.GetSection(LocalDiskArticleAttachmentStorageOptions.SectionName));
builder.Services.Configure<LocalDiskGuideAttachmentStorageOptions>(builder.Configuration.GetSection(LocalDiskGuideAttachmentStorageOptions.SectionName));
builder.Services.Configure<LocalDiskBrandingAssetStorageOptions>(builder.Configuration.GetSection(LocalDiskBrandingAssetStorageOptions.SectionName));
builder.Services.Configure<LocalDiskTicketAttachmentStorageOptions>(builder.Configuration.GetSection(LocalDiskTicketAttachmentStorageOptions.SectionName));

// --- Infrastructure (DbContext, repositories, application services, hosted services) ------
builder.Services.AddInfrastructure(builder.Configuration);

// --- MVC — AuditLoggingActionFilter runs globally for every mutating request on every ------
// controller (see its own doc comment for why this is the only place AuditLogEntry is written).
builder.Services.AddControllers(options => options.Filters.Add<AuditLoggingActionFilter>());

// --- Authentication — two independent schemes side by side: JWT bearer for the agent UI's ---
// own logged-in session, API key for api/integrations/v1/* (INT-1).
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtSigningKey = jwtSection["SigningKey"];
var jwtIssuer = jwtSection["Issuer"] ?? "SupportCrm";
var jwtAudience = jwtSection["Audience"] ?? "SupportCrm";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(string.IsNullOrEmpty(jwtSigningKey) ? "dev-only-insecure-signing-key-change-me" : jwtSigningKey)),
            ValidateLifetime = true
        };
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.Scheme, _ => { });

// --- Authorization — one policy per API-key scope (INT-1), checked against the "scope" ------
// claims ApiKeyAuthenticationHandler attaches, using the ApiKey scheme specifically.
builder.Services.AddAuthorization(options =>
{
    void AddScopePolicy(string scope) =>
        options.AddPolicy(scope, policy => policy
            .AddAuthenticationSchemes(ApiKeyAuthenticationOptions.Scheme)
            .RequireClaim("scope", scope));

    AddScopePolicy("customers.read");
    AddScopePolicy("customers.write");
    AddScopePolicy("tickets.read");
    AddScopePolicy("tickets.write");
    AddScopePolicy("users.read");
});

// --- Rate limiting — api/integrations/v1/* controllers apply this via [EnableRateLimiting] ---
// (100 requests/minute per process, fixed window — see docs/API.md "Scope of this prototype").
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new { error = "Rate limit exceeded. Try again later." }, ct);
    };
    options.AddFixedWindowLimiter(RateLimitPolicies.IntegrationsApi, limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

// --- CORS — the Angular frontend (../frontend, `ng serve` default port) in Development -------
const string FrontendCorsPolicy = "Frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- Apply pending EF Core migrations on startup — this prototype has no separate deploy step.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SupportCrmDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(FrontendCorsPolicy);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
