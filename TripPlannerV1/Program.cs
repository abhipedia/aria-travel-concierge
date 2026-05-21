using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSingleton<TripPlannerV1.Services.ICountryCurrencyService, TripPlannerV1.Services.CountryCurrencyService>();
builder.Services.AddSingleton<TripPlannerV1.Services.IDestinationPlacesService, TripPlannerV1.Services.DestinationPlacesService>();
builder.Services.AddSingleton<TripPlannerV1.Services.ITripPromptBuilder, TripPlannerV1.Services.TripPromptBuilder>();

builder.Services.AddOptions<TripPlannerV1.Services.AiOptions>()
    .Bind(builder.Configuration.GetSection(TripPlannerV1.Services.AiOptions.SectionName));

builder.Services.AddHttpClient<TripPlannerV1.Services.IAiClient, TripPlannerV1.Services.AiClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});

// Health checks for hosting platforms (Azure App Service, Kubernetes, etc.).
builder.Services.AddHealthChecks();

// Per-IP rate limiting to protect the AI endpoint from abuse / runaway cost.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,               // 20 requests...
            Window = TimeSpan.FromMinutes(1), // ...per minute per IP
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    });
});

var app = builder.Build();

// Startup diagnostic: confirm whether the AI provider is configured. We never
// log the key itself - just length + provider so misconfiguration is obvious.
{
    var aiOpts = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TripPlannerV1.Services.AiOptions>>().Value;
    var keyLen = (aiOpts.ApiKey ?? string.Empty).Trim().Length;
    if (keyLen == 0)
    {
        app.Logger.LogWarning("Ai:ApiKey is NOT configured. Set it via user-secrets or the Ai__ApiKey environment variable. AI calls will fail until this is fixed.");
    }
    else
    {
        app.Logger.LogInformation("AI configured: provider={Provider} model={Model} keyLength={KeyLength}",
            aiOpts.Provider, aiOpts.Model ?? "(default)", keyLen);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
