using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ApexWallet.Api.Middlewares
{
    public class BlackBoxLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<BlackBoxLoggingMiddleware> _logger;

        public BlackBoxLoggingMiddleware(RequestDelegate next, ILogger<BlackBoxLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 1. Generate a unique Correlation ID for this specific API lifecycle
            var correlationId = Guid.NewGuid().ToString();
            context.Response.Headers.Append("X-Correlation-ID", correlationId);

            // 2. Enable buffering so we can read the request body stream without breaking it for controllers
            context.Request.EnableBuffering();

            string requestBody = await ReadRequestBodyAsync(context.Request);
            var path = context.Request.Path;
            var method = context.Request.Method;

            _logger.LogInformation("Incoming Request [{CorrelationId}]: {Method} {Path} initiated.", correlationId, method, path);

            try
            {
                // Continue moving down the ASP.NET Core pipeline to your controllers
                await _next(context);
            }
            catch (Exception ex)
            {
                // 3. Black Box Flight Recorder Action: Capture the raw input payload and internal state on error
                _logger.LogError(ex,
                    "CRITICAL API CRASH [{CorrelationId}] | Method: {Method} | Path: {Path} | Payload: {Payload} | Exception: {Message}",
                    correlationId, method, path, requestBody, ex.Message);

                throw; // Propagate the exception up for global error formatting later
            }
        }

        private async Task<string> ReadRequestBodyAsync(HttpRequest request)
        {
            request.Body.Position = 0; // Rewind to start
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            request.Body.Position = 0; // Reset position so the JSON parser reads it normally
            return body;
        }
    }
}