using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.License;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KN.KloudIdentity.Mapper.Common.License;

public class LicenseValidationMiddleware(
    RequestDelegate next,  
    IMemoryCache cache,
    IOptions<AppSettings> options,
    IServiceProvider serviceProvider)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly AppSettings _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    private static readonly TimeSpan CircuitBreakerDuration = TimeSpan.FromMinutes(1);
    private const string CircuitBreakerKey = "LicenseValidation_CircuitBreaker";

    public async Task InvokeAsync(HttpContext context)
    {
        // Paths to ignore
        var ignoredPaths = new[] { "/api/healthz" };

        if (ignoredPaths.Any(p => context.Request.Path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var cacheKey = _options.LicenseValidation.CacheKey;
        var cacheDuration = TimeSpan.FromMinutes(_options.LicenseValidation.CacheDurationMinutes);

        using (var scope = _serviceProvider.CreateScope())
        {
            var _licenseValidationQuery = scope.ServiceProvider.GetRequiredService<ILicenseValidationQuery>();

            LicenseStatus licenseStatus;

            if (!_cache.TryGetValue(cacheKey, out var cachedStatusObj) ||
                cachedStatusObj is not LicenseStatus { IsValid: true } cachedValidStatus)
            {
                licenseStatus = await _licenseValidationQuery.IsLicenseValidAsync(context.RequestAborted);
                _cache.Set(cacheKey, licenseStatus, cacheDuration);
            }
            else
            {
                licenseStatus = cachedValidStatus;
            }

            if (!licenseStatus.IsValid)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(
                    "KloudIdentity platform license is invalid or expired. " +
                    "Please contact Kloudynet Technologies to obtain a new license and activate the platform.",
                    context.RequestAborted);
                return;
            }

            await _next(context);

        }       
    }
}