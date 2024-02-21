using Microsoft.Extensions.Primitives;
using Middleware.Common;
using PurchasingApp.Common.Exceptions;
using Middleware.Types;
using Newtonsoft.Json;
using PurchasingApp.Resources;
using System;
using Purchasing.Common.Exceptions;
using System.Net;
using PurchasingApp.Types;
using System.ServiceModel.Security;
using Microsoft.Extensions.Logging;
using Microsoft.ApplicationInsights.DataContracts;
using System.Text;
using System.Text.RegularExpressions;
using Dynamics_Approvals.Common;
using System.Runtime.ConstrainedExecution;

namespace PurchasingApp.Middleware
{
    public class GlobalExceptionMiddleware
    {
        ILogger<GlobalExceptionMiddleware> _logger;
        private readonly RequestDelegate _next;
        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _logger = logger;
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {

            MemoryStream responseBody = new();
            // Capture the response body 
            var originalResponseBody = context.Response.Body;
            context.Response.Body = responseBody;

            try
            {

                    // Log request
                    var request = await FormatRequest(context.Request);
                    _logger.LogInformation(request);

                    await _next(context);


            }
            catch (UnAuthorizedAccessException ex)
            {
                await HandleExceptionAsync(context, ex, HttpStatusCode.Unauthorized);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
            finally{

                using (responseBody)
                {
                    // Log the response
                    string response = await FormatResponse(context.Response);
                    _logger.LogInformation(response);
                    //  Copy the captured response body back to the original stream
                    responseBody.Seek(0, SeekOrigin.Begin);
                    await responseBody.CopyToAsync(originalResponseBody);
                }

            }

        }
        private Task HandleExceptionAsync(HttpContext context, Exception ex, HttpStatusCode httpStatusCode = HttpStatusCode.InternalServerError)
        {

            _logger.LogError(JsonConvert.SerializeObject(ex, Formatting.Indented));
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)httpStatusCode;

            var response = new ResponseDTO
            {
                Success = false,
                Message = ex.Message,
                ErrorDetails = ex.StackTrace,
                Payload = null,
            };

            return context.Response.WriteAsync(JsonConvert.SerializeObject(response));
        }
        private async Task<string> FormatResponse(HttpResponse response)
        {
            // Format status code and headers
            return JsonConvert.SerializeObject(new
            {
                Type = "Response",
                Method=response.HttpContext.Request.Method,
                Path = response.HttpContext.Request.Path,
                StatusCode = response.StatusCode,
                Headers = response.Headers,
                Body = await GetResponseBody(response)
            }, Formatting.Indented);
        }
        private async Task<string> GetResponseBody(HttpResponse response)
        {
            // Check if the response body is not null
            if (response.Body != null)
            {
               
                // Reset the position of the response body stream to the beginning
                response.Body.Seek(0, SeekOrigin.Begin);

                // Read the response body content asynchronously
                // Read the response body content asynchronously
                string responseBody = await new StreamReader(response.Body).ReadToEndAsync();
                // Reset the position of the response body stream back to the beginning
                response.Body.Seek(0, SeekOrigin.Begin);

                // Return the response body content
                return responseBody;
            }
            // If the response body is null, return null
            return null;
        }
        private async Task<string> FormatRequest(HttpRequest request)
        {
            var requestBody = await ReadRequestBody(request);

            return JsonConvert.SerializeObject(new
            {
                Type = "Request",
                Method = request.Method,
                Path = request.Path,
                Headers = request.Headers,
                Body = requestBody
            }, Formatting.Indented);
        }
        private async Task<string> ReadRequestBody(HttpRequest request)
        {
            request.EnableBuffering();
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, true, 1024, true))
            {
                var body = await reader.ReadToEndAsync();
                request.Body.Position = 0; // Reset the request body stream position for future reads
                return body;
            }
        }

    }
}
