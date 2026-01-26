using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Common;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace KN.KloudIdentity.MapperTests.RESTIntegration;

public class RESTIntegrationTests
{
    private Mapper.MapperCore.RESTIntegration CreateRESTIntegration(
        IConfiguration? configuration = null,
        IAuthContext? authContext = null)
    {
        var httpClientFactory = new Mock<System.Net.Http.IHttpClientFactory>().Object;
        var logger = new Mock<KN.KI.LogAggregator.Library.Abstractions.IKloudIdentityLogger>().Object;
        var appSettings = Microsoft.Extensions.Options.Options.Create(new AppSettings());
        configuration ??= new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"urnPrefix", "urn:kloudidentity:scim:schemas:extension:custom:1.0:"}
        }).Build();
        authContext ??= new Mock<IAuthContext>().Object;
        return new Mapper.MapperCore.RESTIntegration(authContext, httpClientFactory, configuration, appSettings, logger);
    }

    private AppConfig CreateAppConfig(string? idField = "Identifier", string? urnPrefix = "urn:kloudidentity:scim:schemas:extension:custom:1.0:", HttpRequestTypes? requestType = null)
    {
        var attrSchema = new AttributeSchema
        {
            SourceValue = idField,
            DestinationField = urnPrefix + idField,
            HttpRequestType = requestType ?? default
        };

        return new AppConfig
        {
            AppId = "test-app",
            UserAttributeSchemas = new List<AttributeSchema> { attrSchema },
            UserURIs = new List<UserURIs> { new UserURIs { AppId = "test-app" } },
            AuthenticationDetails = default!
        };
    }

    [Fact]
    public void GetIDValue_ReturnsIdentifier_WhenIdentifierPresent()
    {
        // Arrange
        var restIntegration = CreateRESTIntegration();
        var appConfig = CreateAppConfig();
        var correlationId = "corr-1";
        var payload = JObject.FromObject(new { Identifier = "user-123" });

        // Act
        var result = InvokeGetIDValue(restIntegration, payload, appConfig, correlationId, HttpRequestTypes.POST);

        // Assert
        Assert.Equal("user-123", result);
    }

    [Fact]
    public void GetIDValue_Throws_WhenIdentifierNotPresentAndNoFallback()
    {
        // Arrange
        var restIntegration = CreateRESTIntegration();
        var appConfig = CreateAppConfig();
        var correlationId = "corr-2";
        var payload = JObject.FromObject(new { SomeOtherField = "value" });

        // Act & Assert
        var ex = Assert.Throws<TargetInvocationException>(() =>
            InvokeGetIDValue(restIntegration, payload, appConfig, correlationId));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Identifier field not configured and no fallback found in payload", ex.InnerException!.Message);
    }

    [Fact]
    public void GetIDValue_ReturnsIdentifier_WhenIdentifierInChildSchema()
    {
        // Arrange
        var restIntegration = CreateRESTIntegration();
        var urnPrefix = "urn:kloudidentity:scim:schemas:extension:custom:1.0:";
        var childSchema = new AttributeSchema
        {
            SourceValue = "Identifier",
            DestinationField = urnPrefix + "child.id",
            HttpRequestType = HttpRequestTypes.POST
        };
        var appConfig = new AppConfig
        {
            AppId = "test-app",
            UserAttributeSchemas = new List<AttributeSchema> { childSchema },
            UserURIs = new List<UserURIs> { new UserURIs { AppId = "test-app" } },
            AuthenticationDetails = default!
        };
        var correlationId = "corr-3";
        var payload = JObject.Parse("{" +
            "\"child\": { \"id\": \"nested-456\" }" +
            "}");

        // Act
        var result = InvokeGetIDValue(restIntegration, payload, appConfig, correlationId, HttpRequestTypes.POST);

        // Assert
        Assert.Equal("nested-456", result);
    }

    [Fact]
    public void GetIDValue_ReturnsFallback_WhenNoIdentifierButKnownKeyPresent()
    {
        // Arrange
        var restIntegration = CreateRESTIntegration();
        // Remove Identifier mapping
        var appConfig = new AppConfig
        {
            AppId = "test-app",
            UserAttributeSchemas = new List<AttributeSchema>(),
            UserURIs = new List<UserURIs> { new UserURIs { AppId = "test-app" } },
            AuthenticationDetails = default!
        };
        var correlationId = "corr-4";
        var payload = JObject.FromObject(new { key = "fallback-789" });

        // Act
        var result = InvokeGetIDValue(restIntegration, payload, appConfig, correlationId);

        // Assert
        Assert.Equal("fallback-789", result);
    }

    [Fact]
    public void GetIdValue_ReturnsFallback_WhenNoIdentifierButKnownKeyPresentInNestedObject()
    {
        // Arrange
        var restIntegration = CreateRESTIntegration();
        // Remove Identifier mapping
        var appConfig = new AppConfig
        {
            AppId = "test-app",
            UserAttributeSchemas = new List<AttributeSchema>(),
            UserURIs = new List<UserURIs> { new UserURIs { AppId = "test-app" } },
            AuthenticationDetails = default!
        };
        var correlationId = "corr-5";
        var payload = JObject.Parse("{" +
            "\"profile\": { \"identifier\": \"nested-fallback-101\" }" +
            "}");

        // Act
        var result = InvokeGetIDValue(restIntegration, payload, appConfig, correlationId);

        // Assert
        Assert.Equal("nested-fallback-101", result);
    }

    private static dynamic InvokeGetIDValue(object instance, JObject payload, AppConfig appConfig, string correlationId, HttpRequestTypes? requestType = null)
    {
        var method = instance.GetType().GetMethod("GetIDValue", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) throw new InvalidOperationException("GetIDValue method not found");
        try
        {
            return method.Invoke(instance, new object[] { payload, appConfig, correlationId, requestType });
        }
        catch (TargetInvocationException ex)
        {
            throw ex;
        }
    }
}
