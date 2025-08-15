using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.License;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KN.KloudIdentity.Mapper.Common.License;

public class LicenseValidationMiddleware(
    RequestDelegate next,
    ILicenseValidationQuery licenseValidationQuery,
    IMemoryCache cache,
    IOptions<AppSettings> options)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ILicenseValidationQuery _licenseValidationQuery = licenseValidationQuery ?? throw new ArgumentNullException(nameof(licenseValidationQuery));
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly AppSettings _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    private static readonly TimeSpan CircuitBreakerDuration = TimeSpan.FromMinutes(1);
    private const string CircuitBreakerKey = "LicenseValidation_CircuitBreaker";

    public async Task InvokeAsync(HttpContext context)
    {
        var cacheKey = _options.LicenseValidation.CacheKey;
        var cacheDuration = TimeSpan.FromMinutes(_options.LicenseValidation.CacheDurationMinutes);

        // Check if circuit breaker is open
        if (_cache.TryGetValue(CircuitBreakerKey, out _))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync("License validation service unavailable. Please try again later.", context.RequestAborted);
            return;
        }

        LicenseStatus licenseStatus;
        if (!_cache.TryGetValue(cacheKey, out var cachedStatusObj) || cachedStatusObj is not LicenseStatus { IsValid: true } cachedValidStatus)
        {
            try
            {
                licenseStatus = await _licenseValidationQuery.IsLicenseValidAsync(context.RequestAborted);
                _cache.Set(cacheKey, licenseStatus, cacheDuration);
            }
            catch (Exception)
            {
                // Open circuit breaker for a short duration
                _cache.Set(CircuitBreakerKey, true, CircuitBreakerDuration);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync("License validation service unavailable. Please try again later.", context.RequestAborted);
                return;
            }
        }
        else
        {
            licenseStatus = cachedValidStatus;
        }

        if (!licenseStatus.IsValid)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync(licenseStatus.Message ?? "License is invalid.",
                context.RequestAborted);
            return;
        }

        await _next(context);
    }
}
