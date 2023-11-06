using KN.KloudIdentity.Mapper.Common.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace KN.KloudIdentity.Mapper.Common.Exceptions
{
    public class GlobalExceptionHandlingMiddleware
    {
        private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
        private readonly RequestDelegate _next;

        public GlobalExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionHandlingMiddleware> logger
        )
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            var response = context.Response;
            var exModel = new ErrorResponseModel();

            void HandleCommonException(HttpStatusCode statusCode, string message, string title)
            {
                exModel.Status = (int)statusCode;
                response.StatusCode = (int)statusCode;
                exModel.Message = message;
                exModel.Title = title;
                exModel.Details = exception.StackTrace;
                _logger.LogInformation($"{exception.GetType().Name}. Message: [{message}].");
            }

            switch (exception)
            {
                case NotFoundException ex:
                    HandleCommonException(HttpStatusCode.NotFound, ex.Message, "Not Found");
                    break;

                case ValidationException ex:
                    HandleCommonException(
                        HttpStatusCode.BadRequest,
                        ex.Message,
                        "Validation Error"
                    );
                    break;

                case ArgumentException ex:
                    HandleCommonException(HttpStatusCode.BadRequest, ex.Message, "Bad Request");
                    break;

                case UnauthorizedAccessException ex:
                    HandleCommonException(HttpStatusCode.Unauthorized, ex.Message, "Unauthorized");
                    break;

                case NotImplementedException ex:
                    HandleCommonException(
                        HttpStatusCode.NotImplemented,
                        ex.Message,
                        "Not Implemented"
                    );
                    break;

                default:
                    HandleCommonException(
                        HttpStatusCode.InternalServerError,
                        exception.Message,
                        "Internal Server Error"
                    );
                    break;
            }

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var errorJson = JsonConvert.SerializeObject(exModel, settings);

            await context.Response.WriteAsync(errorJson);

            _logger.LogError(exception, "Unhandled exception occurred.");
        }
    }
}
