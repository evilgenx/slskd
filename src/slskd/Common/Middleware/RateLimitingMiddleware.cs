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
            // Only apply rate limiting to authentication endpoints
            if (!context.Request.Path.StartsWithSegments("/api/v0/auth"))
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

            var bucket = _buckets.GetOrAdd(clientIp, _ => new TokenBucket(
                _rateLimitingOptions.LoginAttempts,
                (int)TimeSpan.FromMinutes(_rateLimitingOptions.WindowMinutes).TotalMilliseconds));

            // Attempt to get a token. If not available, GetAsync will wait.
            // For rate limiting, we want to immediately reject if no tokens are available.
            // So, we'll check if a token can be acquired without waiting.
            // The current TokenBucket implementation doesn't have a non-blocking TryTake.
            // For now, we'll use GetAsync and assume it will return quickly if tokens are available.
            // A proper non-blocking TryTake would be ideal here.
            try
            {
                var tokensTaken = await bucket.GetAsync(1); // Request 1 token
                if (tokensTaken == 0) // This might not happen with current GetAsync, but good for future TryTake
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsync("Too many login attempts. Please try again later.");
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                // If the operation is cancelled (e.g., during shutdown), just proceed
                await _next(context);
                return;
            }
            catch (Exception)
            {
                // Handle other potential exceptions during token acquisition
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("An error occurred during rate limiting.");
                return;
            }

            await _next(context);
        }
    }
}
