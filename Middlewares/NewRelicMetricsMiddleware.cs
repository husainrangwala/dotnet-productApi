using NewRelic.Api.Agent;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace ProductApi.Middlewares;

public class NewRelicMetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<NewRelicMetricsMiddleware> _logger;

    public NewRelicMetricsMiddleware(RequestDelegate next, ILogger<NewRelicMetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _logger.LogInformation("[MIDDLEWARE] New Relic Metrics Middleware initialized");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation($"[REQUEST] {context.Request.Method} {context.Request.Path} - Started");
        
        try
        {
            await _next(context);
        }
        finally
        {
            RecordMetrics(context, startTime);
        }
    }

    private void RecordMetrics(HttpContext context, DateTime startTime)
    {
        try
        {
            var statusCode = context.Response.StatusCode;
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var productId = context.GetRouteValue("id")?.ToString();
            var httpMethod = context.Request.Method;
            var path = context.Request.Path.Value;

            _logger.LogInformation($"[METRICS] Processing request: {httpMethod} {path} - Status: {statusCode}, Duration: {duration}ms");

            // Categorize status code
            string statusCategory = statusCode switch
            {
                >= 200 and < 300 => "2xx",
                >= 300 and < 400 => "3xx",
                >= 400 and < 500 => "4xx",
                >= 500 => "5xx",
                _ => "Other"
            };

            var agent = NewRelic.Api.Agent.NewRelic.GetAgent();
            var transaction = agent.CurrentTransaction;

            if (agent == null)
            {
                _logger.LogWarning("[METRICS] ⚠️  New Relic Agent is NULL - Metrics will NOT be recorded");
                return;
            }

            if (transaction == null)
            {
                _logger.LogWarning("[METRICS] ⚠️  Current Transaction is NULL - Some metrics may not be recorded");
            }
            else
            {
                _logger.LogInformation("[METRICS] ✓ Transaction is active");
            }

            // 1. General Traffic Metrics
            _logger.LogInformation("[METRICS] Recording: Custom/Traffic/AllRequests = 1");
            NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Traffic/AllRequests", 1);
            
            _logger.LogInformation($"[METRICS] Recording: Custom/Traffic/StatusCode/{statusCode} = 1");
            NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Traffic/StatusCode/{statusCode}", 1);
            
            _logger.LogInformation($"[METRICS] Recording: Custom/Traffic/StatusCategory/{statusCategory} = 1");
            NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Traffic/StatusCategory/{statusCategory}", 1);
            
            _logger.LogInformation($"[METRICS] Recording: Custom/Traffic/Method/{httpMethod} = 1");
            NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Traffic/Method/{httpMethod}", 1);

            // 2. Response Time Metrics
            _logger.LogInformation($"[METRICS] Recording: Custom/ResponseTime/AllEndpoints = {duration}ms");
            NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/ResponseTime/AllEndpoints", (float)duration);
            
            _logger.LogInformation($"[METRICS] Recording: Custom/ResponseTime/{httpMethod} = {duration}ms");
            NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/ResponseTime/{httpMethod}", (float)duration);

            // 3. Product-Specific Metrics (if product ID exists)
            if (!string.IsNullOrEmpty(productId))
            {
                _logger.LogInformation($"[METRICS] Recording product-specific metrics for ProductId: {productId}");
                
                // Track requests per product
                NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Product/{productId}/Requests", 1);
                NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Product/{productId}/StatusCode/{statusCode}", 1);
                NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Product/{productId}/StatusCategory/{statusCategory}", 1);
                NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Product/{productId}/Method/{httpMethod}", 1);
                
                // Response time per product
                NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Product/{productId}/ResponseTime", (float)duration);

                // Transaction attributes for detailed tracking
                if (transaction != null)
                {
                    transaction.AddCustomAttribute("productId", productId);
                    transaction.AddCustomAttribute("statusCode", statusCode);
                    transaction.AddCustomAttribute("statusCategory", statusCategory);
                    transaction.AddCustomAttribute("httpMethod", httpMethod);
                    transaction.AddCustomAttribute("responseTimeMs", duration);
                    transaction.AddCustomAttribute("path", path);
                    _logger.LogInformation($"[METRICS] Added custom attributes to transaction for ProductId: {productId}");
                }
            }
            else
            {
                _logger.LogInformation("[METRICS] Recording non-product endpoint metrics");
                
                // Non-product endpoints
                if (transaction != null)
                {
                    transaction.AddCustomAttribute("statusCode", statusCode);
                    transaction.AddCustomAttribute("statusCategory", statusCategory);
                    transaction.AddCustomAttribute("httpMethod", httpMethod);
                    transaction.AddCustomAttribute("responseTimeMs", duration);
                    transaction.AddCustomAttribute("path", path);
                    _logger.LogInformation("[METRICS] Added custom attributes to transaction");
                }
            }

            // 4. Success/Error Tracking
            if (statusCategory == "2xx")
            {
                _logger.LogInformation("[METRICS] Recording success metric");
                NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Traffic/Success", 1);
                if (!string.IsNullOrEmpty(productId))
                {
                    NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Product/{productId}/Success", 1);
                }
            }
            else if (statusCategory == "4xx")
            {
                _logger.LogWarning("[METRICS] Recording client error metric");
                NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Traffic/ClientError", 1);
                if (!string.IsNullOrEmpty(productId))
                {
                    NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Product/{productId}/ClientError", 1);
                }
            }
            else if (statusCategory == "5xx")
            {
                _logger.LogError("[METRICS] Recording server error metric");
                NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/Traffic/ServerError", 1);
                if (!string.IsNullOrEmpty(productId))
                {
                    NewRelic.Api.Agent.NewRelic.RecordMetric($"Custom/Product/{productId}/ServerError", 1);
                }
            }

            _logger.LogInformation($"[METRICS] ✓ All metrics recorded successfully for {httpMethod} {path}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[METRICS] ✗ Error recording metrics: {ex.Message}\n{ex.StackTrace}");
        }
    }
}