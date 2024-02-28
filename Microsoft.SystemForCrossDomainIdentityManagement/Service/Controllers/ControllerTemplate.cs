// Copyright (c) Microsoft Corporation.// Licensed under the MIT license.

namespace Microsoft.SCIM
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using KN.KI.LogAggregator.Library;
    using KN.KI.LogAggregator.Library.Abstractions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.WebApiCompatShim;

    [ServiceFilter(typeof(ExtractAppIdFilter))]
    public abstract class ControllerTemplate : ControllerBase
    {
        internal const string AttributeValueIdentifier = "{identifier}";
        private const string HeaderKeyContentType = "Content-Type";
        private const string HeaderKeyLocation = "Location";

        internal readonly IMonitor monitor;
        internal readonly IProvider provider;
        private readonly IKloudIdentityLogger _logger;

        internal ControllerTemplate(IProvider provider, IMonitor monitor, IKloudIdentityLogger logger)
        {
            this.monitor = monitor;
            this.provider = provider;
            _logger = logger;
        }

        protected virtual void ConfigureResponse(Resource resource)
        {
            this.Response.ContentType = ProtocolConstants.ContentType;
            this.Response.StatusCode = (int)HttpStatusCode.Created;

            if (null == this.Response.Headers)
            {
                return;
            }

            if (!this.Response.Headers.ContainsKey(ControllerTemplate.HeaderKeyContentType))
            {
                this.Response.Headers.Add(ControllerTemplate.HeaderKeyContentType, ProtocolConstants.ContentType);
            }

            Uri baseResourceIdentifier = this.ConvertRequest().GetBaseResourceIdentifier();
            Uri resourceIdentifier = resource.GetResourceIdentifier(baseResourceIdentifier);
            string resourceLocation = resourceIdentifier.AbsoluteUri;
            if (!this.Response.Headers.ContainsKey(ControllerTemplate.HeaderKeyLocation))
            {
                this.Response.Headers.Add(ControllerTemplate.HeaderKeyLocation, resourceLocation);
            }
        }

        protected HttpRequestMessage ConvertRequest()
        {
            string appId = HttpContext.Items["appId"] as string;

            HttpRequestMessageFeature hreqmf = new HttpRequestMessageFeature(this.HttpContext);
            HttpRequestMessage result = hreqmf.HttpRequestMessage;

            // Add the appId to the request options
            result.Options.Set(new HttpRequestOptionsKey<string>("appId"), appId);

            return result;
        }

        protected ObjectResult ScimError(HttpStatusCode httpStatusCode, string message, string correlationIdentifier, Exception ex)
        {
            var exceptionType = ex?.GetType()?.Name;

            var eventInfo = $"{Request.Method}{Request.Path} - {exceptionType}";
            _logger.CreateLogAsync(new CreateLogEntity(
                "Error",
                LogSeverities.Error,
                eventInfo,
                null,
                correlationIdentifier,
                "KN.KloudIdentity.SCIM",
                DateTime.UtcNow,
                "system",
                message,
                new ExceptionInfo(
                    ex.Message,
                    ex.StackTrace,
                    ex.InnerException?.Message,
                    ex.InnerException?.StackTrace
                    )

                ));

            return StatusCode((int)httpStatusCode, new Core2Error(message, (int)httpStatusCode));
        }

        protected virtual bool TryGetMonitor(out IMonitor monitor)
        {
            monitor = this.monitor;
            if (null == monitor)
            {
                return false;
            }

            return true;
        }
    }

    public abstract class ControllerTemplate<T> : ControllerTemplate where T : Resource
    {
        internal ControllerTemplate(IProvider provider, IMonitor monitor, IKloudIdentityLogger logger)
            : base(provider, monitor, logger)
        {
        }

        protected abstract IProviderAdapter<T> AdaptProvider(IProvider provider);

        protected virtual IProviderAdapter<T> AdaptProvider()
        {
            IProviderAdapter<T> result = this.AdaptProvider(this.provider);
            return result;
        }


        [HttpDelete(ControllerTemplate.AttributeValueIdentifier)]
        public virtual async Task<IActionResult> Delete(string identifier)
        {
            string correlationIdentifier = null;
            try
            {
                if (string.IsNullOrWhiteSpace(identifier))
                {
                    return this.BadRequest();
                }

                identifier = Uri.UnescapeDataString(identifier);
                HttpRequestMessage request = this.ConvertRequest();
                if (!request.TryGetRequestIdentifier(out correlationIdentifier))
                {
                    throw new HttpResponseException(HttpStatusCode.InternalServerError);
                }

                IProviderAdapter<T> provider = this.AdaptProvider();
                await provider.Delete(request, identifier, correlationIdentifier).ConfigureAwait(false);
                return this.NoContent();
            }
            catch (ArgumentException argumentException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            argumentException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateDeleteArgumentException);
                    monitor.Report(notification);
                }

                return this.BadRequest();
            }
            catch (HttpResponseException responseException)
            {
                if (responseException.Response?.StatusCode == HttpStatusCode.NotFound)
                {
                    return this.NotFound();
                }

                throw;
            }
            catch (NotImplementedException notImplementedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notImplementedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateDeleteNotImplementedException);
                    monitor.Report(notification);
                }

                throw new HttpResponseException(HttpStatusCode.NotImplemented);
            }
            catch (NotSupportedException notSupportedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notSupportedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateDeleteNotSupportedException);
                    monitor.Report(notification);
                }

                throw new HttpResponseException(HttpStatusCode.NotImplemented);
            }
            catch (Exception exception)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            exception,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateDeleteException);
                    monitor.Report(notification);
                }

                throw;
            }
        }

        [HttpGet]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Get", Justification = "The names of the methods of a controller must correspond to the names of hypertext markup verbs")]
        public virtual async Task<ActionResult<QueryResponseBase>> Get()
        {
            string correlationIdentifier = null;
            string appId = HttpContext.Items["appId"] as string;
            try
            {
                HttpRequestMessage request = this.ConvertRequest();
                if (!request.TryGetRequestIdentifier(out correlationIdentifier))
                {
                    throw new HttpResponseException(HttpStatusCode.InternalServerError);
                }

                IResourceQuery resourceQuery = new ResourceQuery(request.RequestUri);
                IProviderAdapter<T> provider = this.AdaptProvider();
                QueryResponseBase result =
                    await provider
                            .Query(
                                request,
                                resourceQuery.Filters,
                                resourceQuery.Attributes,
                                resourceQuery.ExcludedAttributes,
                                resourceQuery.PaginationParameters,
                                correlationIdentifier)
                            .ConfigureAwait(false);
                return this.Ok(result);
            }
            catch (ArgumentException argumentException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            argumentException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateQueryArgumentException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.BadRequest, argumentException.Message, correlationIdentifier, argumentException);
            }
            catch (NotImplementedException notImplementedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notImplementedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateQueryNotImplementedException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.NotImplemented, notImplementedException.Message, correlationIdentifier, notImplementedException);
            }
            catch (NotSupportedException notSupportedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notSupportedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateQueryNotSupportedException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.BadRequest, notSupportedException.Message, correlationIdentifier, notSupportedException);
            }
            catch (HttpResponseException responseException)
            {
                if (responseException.Response?.StatusCode != HttpStatusCode.NotFound)
                {
                    if (this.TryGetMonitor(out IMonitor monitor))
                    {
                        IExceptionNotification notification =
                            ExceptionNotificationFactory.Instance.CreateNotification(
                                responseException.InnerException ?? responseException,
                                correlationIdentifier,
                                ServiceNotificationIdentifiers.ControllerTemplateGetException);
                        monitor.Report(notification);
                    }
                }

                return this.ScimError(HttpStatusCode.InternalServerError, responseException.Message, correlationIdentifier, responseException);
            }
            catch (Exception exception)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            exception,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateQueryException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.InternalServerError, exception.Message, correlationIdentifier, exception);
            }
        }

        [HttpGet(ControllerTemplate.AttributeValueIdentifier)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1716:IdentifiersShouldNotMatchKeywords", MessageId = "Get", Justification = "The names of the methods of a controller must correspond to the names of hypertext markup verbs")]
        public virtual async Task<IActionResult> Get([FromUri] string identifier)
        {
            string correlationIdentifier = null;
            string appId = HttpContext.Items["appId"] as string;
            try
            {
                if (string.IsNullOrWhiteSpace(identifier))
                {
                    var message = SystemForCrossDomainIdentityManagementServiceResources.ExceptionInvalidIdentifier;
                    return this.ScimError(HttpStatusCode.BadRequest,
                        message,
                        correlationIdentifier,
                        new Exception(message));
                }

                HttpRequestMessage request = this.ConvertRequest();
                if (!request.TryGetRequestIdentifier(out correlationIdentifier))
                {
                    throw new HttpResponseException(HttpStatusCode.InternalServerError);
                }

                IResourceQuery resourceQuery = new ResourceQuery(request.RequestUri);
                if (resourceQuery.Filters.Any())
                {
                    if (resourceQuery.Filters.Count != 1)
                    {
                        var message = SystemForCrossDomainIdentityManagementServiceResources.ExceptionFilterCount;
       
                        return this.ScimError(HttpStatusCode.BadRequest, message, correlationIdentifier, new Exception(message));
                    }

                    IFilter filter = new Filter(AttributeNames.Identifier, ComparisonOperator.Equals, identifier);
                    filter.AdditionalFilter = resourceQuery.Filters.Single();
                    IReadOnlyCollection<IFilter> filters =
                        new IFilter[]
                            {
                                filter
                            };
                    IResourceQuery effectiveQuery =
                        new ResourceQuery(
                            filters,
                            resourceQuery.Attributes,
                            resourceQuery.ExcludedAttributes);
                    IProviderAdapter<T> provider = this.AdaptProvider();
                    QueryResponseBase queryResponse =
                        await provider
                            .Query(
                                request,
                                effectiveQuery.Filters,
                                effectiveQuery.Attributes,
                                effectiveQuery.ExcludedAttributes,
                                effectiveQuery.PaginationParameters,
                                correlationIdentifier)
                            .ConfigureAwait(false);
                    if (!queryResponse.Resources.Any())
                    {
                        var message = string.Format(SystemForCrossDomainIdentityManagementServiceResources.ResourceNotFoundTemplate, identifier);

                        return this.ScimError(HttpStatusCode.NotFound, message, correlationIdentifier, new Exception(message));
                    }

                    Resource result = queryResponse.Resources.Single();
                    return this.Ok(result);
                }
                else
                {
                    IProviderAdapter<T> provider = this.AdaptProvider();
                    Resource result =
                        await provider
                            .Retrieve(
                                request,
                                identifier,
                                resourceQuery.Attributes,
                                resourceQuery.ExcludedAttributes,
                                correlationIdentifier)
                            .ConfigureAwait(false);
                    if (null == result)
                    {
                        var message = string.Format(SystemForCrossDomainIdentityManagementServiceResources.ResourceNotFoundTemplate, identifier);
                        return this.ScimError(HttpStatusCode.NotFound, message, correlationIdentifier, new Exception(message));
                    }

                    return this.Ok(result);
                }
            }
            catch (ArgumentException argumentException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            argumentException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateGetArgumentException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.BadRequest, argumentException.Message, correlationIdentifier, argumentException);
            }
            catch (NotImplementedException notImplementedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notImplementedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateGetNotImplementedException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.NotImplemented, notImplementedException.Message, correlationIdentifier, notImplementedException);
            }
            catch (NotSupportedException notSupportedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notSupportedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateGetNotSupportedException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.BadRequest, notSupportedException.Message, correlationIdentifier, notSupportedException);
            }
            catch (HttpResponseException responseException)
            {
                if (responseException.Response?.StatusCode != HttpStatusCode.NotFound)
                {
                    if (this.TryGetMonitor(out IMonitor monitor))
                    {
                        IExceptionNotification notification =
                            ExceptionNotificationFactory.Instance.CreateNotification(
                                responseException.InnerException ?? responseException,
                                correlationIdentifier,
                                ServiceNotificationIdentifiers.ControllerTemplateGetException);
                        monitor.Report(notification);
                    }
                }

                if (responseException.Response?.StatusCode == HttpStatusCode.NotFound)
                {
                    return this.ScimError(HttpStatusCode.NotFound, string.Format(SystemForCrossDomainIdentityManagementServiceResources.ResourceNotFoundTemplate, identifier), correlationIdentifier, responseException);
                }

                return this.ScimError(HttpStatusCode.InternalServerError, responseException.Message, correlationIdentifier, responseException);
            }
            catch (Exception exception)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            exception,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplateGetException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.InternalServerError, exception.Message, correlationIdentifier, exception);
            }
        }

        [HttpPatch(ControllerTemplate.AttributeValueIdentifier)]
        public virtual async Task<IActionResult> Patch(string identifier, [FromBody] PatchRequest2 patchRequest)
        {
            string correlationIdentifier = null;

            try
            {
                if (string.IsNullOrWhiteSpace(identifier))
                {
                    return this.BadRequest();
                }

                identifier = Uri.UnescapeDataString(identifier);

                if (null == patchRequest)
                {
                    return this.BadRequest();
                }

                HttpRequestMessage request = this.ConvertRequest();
                if (!request.TryGetRequestIdentifier(out correlationIdentifier))
                {
                    throw new HttpResponseException(HttpStatusCode.InternalServerError);
                }

                IProviderAdapter<T> provider = this.AdaptProvider();
                await provider.Update(request, identifier, patchRequest, correlationIdentifier).ConfigureAwait(false);

                // If EnterpriseUser, return HTTP code 200 and user object, otherwise HTTP code 204
                if (provider.SchemaIdentifier == SchemaIdentifiers.Core2EnterpriseUser)
                {
                    return await this.Get(identifier).ConfigureAwait(false);
                }
                else
                    return this.NoContent();
            }
            catch (ArgumentException argumentException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            argumentException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePatchArgumentException);
                    monitor.Report(notification);
                }

                return this.BadRequest();
            }
            catch (NotImplementedException notImplementedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notImplementedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePatchNotImplementedException);
                    monitor.Report(notification);
                }

                throw new HttpResponseException(HttpStatusCode.NotImplemented);
            }
            catch (NotSupportedException notSupportedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notSupportedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePatchNotSupportedException);
                    monitor.Report(notification);
                }

                throw new HttpResponseException(HttpStatusCode.NotImplemented);
            }
            catch (HttpResponseException responseException)
            {
                if (responseException.Response?.StatusCode == HttpStatusCode.NotFound)
                {
                    return this.NotFound();
                }
                else
                {
                    if (this.TryGetMonitor(out IMonitor monitor))
                    {
                        IExceptionNotification notification =
                            ExceptionNotificationFactory.Instance.CreateNotification(
                                responseException.InnerException ?? responseException,
                                correlationIdentifier,
                                ServiceNotificationIdentifiers.ControllerTemplateGetException);
                        monitor.Report(notification);
                    }
                }

                throw;

            }
            catch (Exception exception)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            exception,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePatchException);
                    monitor.Report(notification);
                }

                throw;
            }
        }

        [HttpPost]
        public virtual async Task<ActionResult<Resource>> Post([FromBody] T resource)
        {
            string correlationIdentifier = null;

            try
            {
                if (null == resource)
                {
                    return this.BadRequest();
                }

                HttpRequestMessage request = this.ConvertRequest();
                if (!request.TryGetRequestIdentifier(out correlationIdentifier))
                {
                    throw new HttpResponseException(HttpStatusCode.InternalServerError);
                }

                IProviderAdapter<T> provider = this.AdaptProvider();
                Resource result = await provider.Create(request, resource, correlationIdentifier).ConfigureAwait(false);
                this.ConfigureResponse(result);
                return this.CreatedAtAction(nameof(Post), result);
            }
            catch (ArgumentException argumentException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            argumentException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePostArgumentException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.BadRequest, argumentException.Message, correlationIdentifier, argumentException);
            }
            catch (NotImplementedException notImplementedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notImplementedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePostNotImplementedException);
                    monitor.Report(notification);
                }

                throw new HttpResponseException(HttpStatusCode.NotImplemented);
            }
            catch (NotSupportedException notSupportedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notSupportedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePostNotSupportedException);
                    monitor.Report(notification);
                }

                throw new HttpResponseException(HttpStatusCode.NotImplemented);
            }
            catch (HttpResponseException httpResponseException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            httpResponseException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePostNotSupportedException);
                    monitor.Report(notification);
                }

                if (httpResponseException.Response.StatusCode == HttpStatusCode.Conflict)
                    return this.Conflict();
                else
                    return this.ScimError(HttpStatusCode.BadRequest, httpResponseException.Message, correlationIdentifier, httpResponseException);
            }
            catch (Exception exception)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            exception,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePostException);
                    monitor.Report(notification);
                }

                throw;
            }
        }

        [HttpPut(ControllerTemplate.AttributeValueIdentifier)]
        public virtual async Task<ActionResult<Resource>> Put([FromBody] T resource, string identifier)
        {
            string correlationIdentifier = null;

            try
            {
                if (null == resource)
                {
                    var message = SystemForCrossDomainIdentityManagementServiceResources.ExceptionInvalidResource;
                    return this.ScimError(HttpStatusCode.BadRequest, message, correlationIdentifier, new Exception(message));
                }

                if (string.IsNullOrEmpty(identifier))
                {
                    var message = SystemForCrossDomainIdentityManagementServiceResources.ExceptionInvalidIdentifier;
                    return this.ScimError(HttpStatusCode.BadRequest, message, correlationIdentifier, new Exception(message));
                }

                HttpRequestMessage request = this.ConvertRequest();
                if (!request.TryGetRequestIdentifier(out correlationIdentifier))
                {
                    throw new HttpResponseException(HttpStatusCode.InternalServerError);
                }

                IProviderAdapter<T> provider = this.AdaptProvider();
                Resource result = await provider.Replace(request, resource, correlationIdentifier).ConfigureAwait(false);
                this.ConfigureResponse(result);
                return this.Ok(result);
            }
            catch (ArgumentException argumentException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            argumentException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePutArgumentException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.BadRequest, argumentException.Message, correlationIdentifier, argumentException);
            }
            catch (NotImplementedException notImplementedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notImplementedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePutNotImplementedException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.NotImplemented, notImplementedException.Message, correlationIdentifier, notImplementedException);
            }
            catch (NotSupportedException notSupportedException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            notSupportedException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePutNotSupportedException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.BadRequest, notSupportedException.Message,correlationIdentifier,notSupportedException);
            }
            catch (HttpResponseException httpResponseException)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            httpResponseException,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePostNotSupportedException);
                    monitor.Report(notification);
                }

                if (httpResponseException.Response.StatusCode == HttpStatusCode.NotFound)
                    return this.ScimError(HttpStatusCode.NotFound, string.Format(SystemForCrossDomainIdentityManagementServiceResources.ResourceNotFoundTemplate, identifier), correlationIdentifier, httpResponseException);
                else if (httpResponseException.Response.StatusCode == HttpStatusCode.Conflict)
                    return this.ScimError(HttpStatusCode.Conflict, SystemForCrossDomainIdentityManagementServiceResources.ExceptionInvalidRequest, correlationIdentifier, httpResponseException);
                else
                    return this.ScimError(HttpStatusCode.BadRequest, httpResponseException.Message, correlationIdentifier, httpResponseException);
            }
            catch (Exception exception)
            {
                if (this.TryGetMonitor(out IMonitor monitor))
                {
                    IExceptionNotification notification =
                        ExceptionNotificationFactory.Instance.CreateNotification(
                            exception,
                            correlationIdentifier,
                            ServiceNotificationIdentifiers.ControllerTemplatePutException);
                    monitor.Report(notification);
                }

                return this.ScimError(HttpStatusCode.InternalServerError, exception.Message, correlationIdentifier, exception);
            }
        }
    }
}
