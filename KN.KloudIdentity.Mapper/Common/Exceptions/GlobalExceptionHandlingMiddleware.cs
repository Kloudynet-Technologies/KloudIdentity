//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Common.Models;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper.Config.Db;
using KN.KI.LogAggregator.Library;

namespace KN.KloudIdentity.Mapper.Common.Exceptions
{
    /// <summary>
    /// Global exception handling middleware and return a standard error response.
    /// </summary>
    public class GlobalExceptionHandlingMiddleware
    {
        private readonly IKloudIdentityLogger _logger;
        private readonly RequestDelegate _next;

        public GlobalExceptionHandlingMiddleware(
            RequestDelegate next,
            IKloudIdentityLogger logger
        )
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Invoke the middleware
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Handles all application exceptions and returns a standard error response with status code.
        /// </summary>
        /// <param name="context">The HttpContext of the current request.</param>
        /// <param name="exception">The exception that needs to be handled.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
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

                _ = CreateLogAsync(context, exception);
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

        }

        private async Task CreateLogAsync(HttpContext context, Exception exception)
        {
            var exceptionType = exception.GetType().Name;
            var eventInfo = $@"path: [{context.Request.Method}]{context.Request.Path} - {exceptionType}";

            await _logger.CreateLogAsync(new CreateLogEntity(
                LogType.Error.ToString(),
                LogSeverities.Error,
                eventInfo,
                null,
                context.TraceIdentifier,
                "KN.KloudyIdentity.SCIM",
                DateTime.UtcNow,
                "system",
                exception?.Message,
                new ExceptionInfo(
                    exception?.Message,
                    exception?.StackTrace,
                    exception?.InnerException?.Message,
                    exception?.InnerException?.StackTrace
                    )

                ));
        }
    }
}
