using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using slskd.Configuration;

namespace slskd.Common.Middleware
{
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly Options.SecurityOptions.HeadersOptions _options;

        public SecurityHeadersMiddleware(RequestDelegate next, IOptionsMonitor<Options> optionsMonitor)
        {
            _next = next;
            _options = optionsMonitor.CurrentValue.Security.Headers;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!_options.Enabled)
            {
                await _next(context);
                return;
            }

            var headers = context.Response.Headers;

            // Add Content Security Policy header
            if (!string.IsNullOrWhiteSpace(_options.ContentSecurityPolicy))
            {
                headers["Content-Security-Policy"] = _options.ContentSecurityPolicy;
            }

            // Add Strict-Transport-Security header
            if (!string.IsNullOrWhiteSpace(_options.StrictTransportSecurity))
            {
                headers["Strict-Transport-Security"] = _options.StrictTransportSecurity;
            }

            // Add X-Content-Type-Options header
            headers["X-Content-Type-Options"] = "nosniff";

            // Add X-Frame-Options header
            headers["X-Frame-Options"] = "DENY";

            // Add X-XSS-Protection header
            headers["X-XSS-Protection"] = "1; mode=block";

            // Add Referrer-Policy header
            headers["Referrer-Policy"] = "no-referrer";

            // Add Public-Key-Pins header
            if (!string.IsNullOrWhiteSpace(_options.PublicKeyPins))
            {
                headers["Public-Key-Pins"] = _options.PublicKeyPins;
            }

            await _next(context);
        }
    }
}
