using KN.KloudIdentity.Mapper.Domain.License;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace KN.KloudIdentity.Mapper.Common.License;

public class LicenseValidationMiddleware(
    RequestDelegate next,
    ILicenseStatusCheckQuery licenseStatusCheckQuery,
    IMemoryCache cache)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    private readonly ILicenseStatusCheckQuery _licenseStatusCheckQuery =
        licenseStatusCheckQuery ?? throw new ArgumentNullException(nameof(licenseStatusCheckQuery));

    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private const string LicenseCacheKey = "LicenseStatus";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_cache.TryGetValue(LicenseCacheKey, out var cachedStatusObj))
        {
            var licenseStatus = await _licenseStatusCheckQuery.IsLicenseValidAsync(context.RequestAborted);
            _cache.Set(LicenseCacheKey, licenseStatus, CacheDuration);

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