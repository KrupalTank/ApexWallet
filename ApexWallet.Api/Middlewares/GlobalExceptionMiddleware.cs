using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace ApexWallet.Api.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Let the request pass through to the next middleware or controller smoothly
                await _next(context);
            }
            catch (Exception ex)
            {
                // If ANY unhandled exception crashes a controller down the line, it lands right here!
               
                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // 1. Pull the unique Correlation-ID we generated in our logging layer to map the error
            var correlationId = context.Response.Headers["X-Correlation-ID"].ToString();

            // 2. Log the exact internal stack trace cleanly into your local text files
            _logger.LogError(exception, "Unhandled Exception Intercepted [{CorrelationId}]: {Message}", correlationId, exception.Message);

            // 3. Set up a secure, standardized JSON error structure
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError; // Status 500

            var errorResponse = new
            {
                Status = context.Response.StatusCode,
                Message = "An unexpected internal error occurred while processing your wallet transaction.",
                CorrelationId = correlationId // Provide the user/QA tester with the ID so they can look it up in your logs
            };

            var jsonResult = JsonSerializer.Serialize(errorResponse);
            return context.Response.WriteAsync(jsonResult);
        }
    }
}