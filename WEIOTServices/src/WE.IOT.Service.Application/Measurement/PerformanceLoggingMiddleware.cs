using Common.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Measurement;

public class PerformanceLoggingMiddleware
{
    private RequestDelegate _next;
    private readonly ILogger _logger;

    public PerformanceLoggingMiddleware(ILogger logger, RequestDelegate next)
    {
        _logger = logger;
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        StringBuilder log = new StringBuilder();
        Stopwatch timer = new Stopwatch();

        try
        {
            timer.Start();
            // Request
            log.Append("\n=================== START PERFORMANCE TRACING REQUEST =================== \n");
            log.Append($"\nA {context.Request.Method} request arrived at {context.Request.Path} {context.Request.QueryString.ToString()} \n");
            log.Append($"\nRequest time (local): {DateTime.Now.ToString("r")} \n");

            // Response
            var originalBodyStream = context.Response.Body;

            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                // Invoke next middleware
                await _next(context);

                var response = context.Response;

                response.Body.Seek(0, SeekOrigin.Begin);

                string responseText = await new StreamReader(response.Body).ReadToEndAsync();

                string bodyLog = !string.IsNullOrEmpty(responseText) ? responseText : "EMPTY";

                response.Body.Seek(0, SeekOrigin.Begin);

                log.Append($"\nResponse time (local): {DateTime.Now.ToString("r")}\n");
                log.Append($"\nResponse status: {context.Response.StatusCode}\n");
                log.Append($"\nTotal time: {timer.Elapsed.TotalSeconds} seconds.\n");
                //log.Append($"\nBody: {bodyLog}");


                log.Append("\n=================== END PERFORMANCE TRACING REQUEST =================== \n");
                timer.Stop();

                await responseBody.CopyToAsync(originalBodyStream);
            }
        } 
        catch
        {
            throw;
        }
        finally
        {
            await Task.Run(() =>
            {
                Console.WriteLine(log.ToString());
                _logger.Trace(log.ToString());
                return Task.CompletedTask;
            });
        }
    }
}

public static class PerformanceLoggingExtensions
{
    public static void UsePerformanceLoggingMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<PerformanceLoggingMiddleware>();
    }
}