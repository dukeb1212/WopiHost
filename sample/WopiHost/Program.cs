using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using System.Text;
using WopiHost.Abstractions;
using WopiHost.Core.Extensions;
using WopiHost.Core.Infrastructure;
using WopiHost.Core.Models;
using WopiHost.Data;
using WopiHost.Discovery;
using WopiHost.FileSystemProvider;
using WopiHost.Infrastructure;
using WopiHost.Models;
using WopiHost.Models.Configuration;
using WopiHost.Services;

namespace WopiHost;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Debug()
            .CreateLogger();

        try
        {
            Log.Information("Starting WOPI host");

            var builder = WebApplication.CreateBuilder(args);

            // Add Serilog
            builder.Host.UseSerilog();

            // Add service defaults from Aspire
            builder.AddServiceDefaults();

            // Add services to the container
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
            });

            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                // Cấu hình để ứng dụng tin tưởng các header X-Forwarded-For và X-Forwarded-Proto
                // do reverse proxy gửi đến.
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                // Vì proxy của bạn đang chạy trên cùng máy (localhost),
                // chúng ta cần xóa danh sách proxy và network mặc định để nó chấp nhận header từ bất kỳ nguồn nào.
                // Lưu ý: Trong môi trường production thực tế, bạn nên cấu hình KnownProxies/KnownNetworks
                // một cách cụ thể để tăng cường bảo mật.
                options.KnownProxies.Clear();
                options.KnownNetworks.Clear();

                options.ForwardLimit = null; // No limit on number of entries in forwarded headers
                options.RequireHeaderSymmetry = false; // Do not require all headers to be present
                options.ForwardedHostHeaderName = "X-Forwarded-Host"; // Use X-Forwarded-Host header
            });

            // Configuration
            // this makes sure that the configuration exists and is valid
            var wopiHostOptionsSection = builder.Configuration.GetRequiredSection(WopiConfigurationSections.WOPI_ROOT);
            builder.Services
                .AddOptionsWithValidateOnStart<WopiHostOptions>()
                .BindConfiguration(wopiHostOptionsSection.Path)
                .ValidateDataAnnotations();

            var wopiHostOptions = wopiHostOptionsSection.Get<WopiHostOptions>();

            // Configure app settings
            builder.Services.Configure<FileStorageSettings>(
                builder.Configuration.GetSection("FileStorageSettings"));
            builder.Services.Configure<WopiDiscoverySettings>(
                builder.Configuration.GetSection("WopiDiscovery"));
            builder.Services.Configure<JwtSettings>(
                builder.Configuration.GetSection("JwtSettings"));
            // Add API Key authentication
            builder.Services.AddApiKeyAuthentication(builder.Configuration);
            
            // Add Rate Limiting from configuration
            builder.Services.AddRateLimiting(options => builder.Configuration.GetSection("RateLimiter").Bind(options));

            // Add PostgreSQL DbContext
            builder.Services.AddDbContext<WopiDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

            // Add custom services
            builder.Services.AddHttpContextAccessor(); // Required for JWT token access in InMemoryWopiLockProvider
            builder.Services.AddScoped<IFileService, FileService>();
            builder.Services.AddScoped<DataSeeder>();
            builder.Services.AddSingleton<IJwtService>(provider =>
            {
                var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
                return new JwtService(jwtSettings ?? new JwtSettings());
            });

            // Register security handler
            builder.Services.AddSingleton<IWopiSecurityHandler, MyWopiSecurityHandler>();
            builder.Services.AddMemoryCache();

            // Register storage providers
            builder.Services.AddScoped<IWopiStorageProvider, DatabaseStorageProvider>();
            builder.Services.AddScoped<IWopiWritableStorageProvider, DatabaseStorageProvider>();
            builder.Services.AddSingleton<IWopiLockProvider, InMemoryWopiLockProvider>();

            // Add HttpClient for session cleanup save operations
            builder.Services.AddHttpClient();

            // Add Discovery services
            builder.Services.AddWopiDiscovery<WopiHostOptions>(
                options => builder.Configuration.GetSection(WopiConfigurationSections.DISCOVERY_OPTIONS).Bind(options));

            // Add Cobalt support
            // if (wopiHostOptions.UseCobalt)
            // {
            //     // Add cobalt
            //     builder.Services.AddCobalt();
            // }

            builder.Services.AddControllers();

            // Add OpenAPI
            builder.Services.AddOpenApi();

            var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
            var key = Encoding.ASCII.GetBytes(jwtSettings.SecretKey);

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // Đặt là true ở production
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                // Rất quan trọng: WOPI client gửi token qua query string tên là "access_token"
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            // Add WOPI with custom events
            builder.Services.AddWopi(options =>
            {
                options.OnCheckFileInfo = WopiEvents.OnGetWopiCheckFileInfo;
            });

            var app = builder.Build();

            // Initialize WopiEvents with service provider for database access
            WopiEvents.Initialize(app.Services);

            // Seed database in development
            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
                await seeder.SeedAsync();
            }

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();

                // Map OpenAPI endpoint (only in development)
                app.MapOpenApi();

                app.MapScalarApiReference(options => options.WithTitle(nameof(WopiHost)).WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient));
            }

            app.UseForwardedHeaders();
            
            // Apply rate limiting early in the pipeline, but after forwarded headers
            // to ensure we get the correct client IP
            app.UseRateLimiting();

            app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = LogHelper.EnrichWithWopiDiagnostics;
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} with [WOPI CorrelationID: {" + nameof(WopiHeaders.CORRELATION_ID) + "}, WOPI SessionID: {" + nameof(WopiHeaders.SESSION_ID) + "}] responded {StatusCode} in {Elapsed:0.0000} ms";
            });

            app.Use(async (context, next) =>
            {
                if (!context.Request.Path.StartsWithSegments("/api") && 
                    !context.Request.Path.StartsWithSegments("/wopi"))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsync("Not Found");
                    return;
                }

                await next();
            });

            app.UseRouting();

            // Apply API Key authentication before other auth mechanisms
            app.UseApiKeyAuthentication();
            
            // Automatically authenticate
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapGet("/", () => "This is just a WOPI server. You need a WOPI client to access it...").ShortCircuit(404);

            // Map health check endpoints
            app.MapHealthChecks("/health");

            // Map default endpoints from Aspire
            app.MapDefaultEndpoints();

            app.Run();

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "WOPI Host terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
