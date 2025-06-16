using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using slskd.Common;
using slskd.Configuration;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace slskd.Common.Middleware
{
    public class RateLimitingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Options.SecurityOptions.RateLimitingOptions _rateLimitingOptions;
        private static readonly ConcurrentDictionary<string, TokenBucket> _buckets = new ConcurrentDictionary<string, TokenBucket>();

        public RateLimitingMiddleware(RequestDelegate next, IOptionsMonitor<Options> optionsMonitor)
        {
            _next = next;
            _rateLimitingOptions = optionsMonitor.CurrentValue.Security.RateLimiting;
        }

        public async Task Invoke(HttpContext context)
        {
            // Only apply rate limiting to authentication and search endpoints
            if (!context.Request.Path.StartsWithSegments("/api/v0/auth") &&
                !context.Request.Path.StartsWithSegments("/api/v0/searches"))
            {
                await _next(context);
                return;
            }

            if (!_rateLimitingOptions.Enabled)
            {
                await _next(context);
                return;
            }

            var clientIp = context.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(clientIp))
            {
                await _next(context);
                return;
            }

            var bucket = _buckets.GetOrAdd(clientIp, _ =>
            {
                // Use different rate limiting settings for search vs. auth
                if (context.Request.Path.StartsWithSegments("/api/v0/searches"))
                {
                    // Example: 10 searches per minute per IP
                    return new TokenBucket(10, (int)TimeSpan.FromMinutes(1).TotalMilliseconds);
                }
                else // /api/v0/auth
                {
                    return new TokenBucket(
                        _rateLimitingOptions.LoginAttempts,
                        (int)TimeSpan.FromMinutes(_rateLimitingOptions.WindowMinutes).TotalMilliseconds);
                }
            });

            // Use the non-blocking TryTake to check for available tokens
            if (!bucket.TryTake(1, out _))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Request.Path.StartsWithSegments("/api/v0/searches"))
                {
                    await context.Response.WriteAsync("Too many search requests. Please try again later.");
                }
                else
                {
                    await context.Response.WriteAsync("Too many login attempts. Please try again later.");
                }
                return;
            }

            await _next(context);
        }
    }
}
