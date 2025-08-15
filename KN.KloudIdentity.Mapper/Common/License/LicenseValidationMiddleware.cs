using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.License;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KN.KloudIdentity.Mapper.Common.License;

public class LicenseValidationMiddleware(
    RequestDelegate next,
    ILicenseStatusCheckQuery licenseStatusCheckQuery,
    IMemoryCache cache,
    IOptions<AppSettings> options)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));
    private readonly ILicenseStatusCheckQuery _licenseStatusCheckQuery = licenseStatusCheckQuery ?? throw new ArgumentNullException(nameof(licenseStatusCheckQuery));
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly AppSettings _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    public async Task InvokeAsync(HttpContext context)
    {
        var cacheKey = _options.LicenseValidation.CacheKey;
        var cacheDuration = TimeSpan.FromMinutes(_options.LicenseValidation.CacheDurationMinutes);

        if (!_cache.TryGetValue(cacheKey, out var cachedStatusObj))
        {
            var licenseStatus = await _licenseStatusCheckQuery.IsLicenseValidAsync(context.RequestAborted);
            _cache.Set(cacheKey, licenseStatus, cacheDuration);

            if (!licenseStatus.IsValid)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(licenseStatus.Message ?? "License is invalid.",
                    context.RequestAborted);
                return;
            }
        }
        else
        {
            if (cachedStatusObj is LicenseStatus { IsValid: false } cachedStatus)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync(cachedStatus.Message ?? "License is invalid.",
                    context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }
}
