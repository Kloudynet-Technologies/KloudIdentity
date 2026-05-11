using KN.KI.LogAggregator.Library.Abstractions;
using KN.KloudIdentity.Mapper;
using KN.KloudIdentity.Mapper.Domain;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Domain.Authentication;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using KN.KloudIdentity.Mapper.MapperCore;
using KN.KloudIdentity.Mapper.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SCIM;
using Moq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace KN.KloudIdentity.MapperTests.SOAPIntegration;

public class SOAPIntegrationUnitTests
{
    [Fact]
    public async Task MapAndPreparePayloadAsync_WithoutAppConfig_ThrowsNotSupportedException()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), new Core2EnterpriseUser()));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_WithAppConfigAndNoTemplate_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(templates: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.MapAndPreparePayloadAsync(new List<AttributeSchema>(), new Core2EnterpriseUser(), appConfig));
    }

    [Fact]
    public async Task MapAndPreparePayloadAsync_WithValidTemplate_ReturnsMappedSoapPayload()
    {
        var sut = CreateSut();
        var schema = new List<AttributeSchema>
        {
            new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct },
            new() { DestinationField = "UserName", SourceValue = "UserName", MappingType = MappingTypes.Direct }
        };

        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<Envelope><Identifier>{{Identifier}}</Identifier><UserName>{{UserName}}</UserName></Envelope>", SOAPActions.Create)
            });

        var resource = new Core2EnterpriseUser { Identifier = "ID-123", UserName = "soap.user" };

        var payload = await sut.MapAndPreparePayloadAsync(schema, resource, appConfig);

        string payloadText = Assert.IsType<string>(payload);
        Assert.Contains("<Identifier>ID-123</Identifier>", payloadText);
        Assert.Contains("<UserName>soap.user</UserName>", payloadText);
    }

    [Fact]
    public void ParseSoapUserResponse_ValidResponse_MapsCoreFields()
    {
        var sut = CreateSut();
        var responseBody = """
			<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
			  <soap:Body>
				<GetUserResponse>
				  <Identifier>U-001</Identifier>
				  <UserName>alice</UserName>
				  <DisplayName>Alice Doe</DisplayName>
				</GetUserResponse>
			  </soap:Body>
			</soap:Envelope>
			""";

        var user = sut.ParseSoapUserResponse(responseBody);

        Assert.Equal("U-001", user.Identifier);
        Assert.Equal("alice", user.UserName);
        Assert.Equal("Alice Doe", user.DisplayName);
    }

    [Fact]
    public void ParseSoapUserResponse_EmptyResponse_ThrowsArgumentException()
    {
        var sut = CreateSut();
        Assert.Throws<ArgumentException>(() => sut.ParseSoapUserResponse(""));
    }

    [Fact]
    public void ParseSoapUserResponse_InvalidXml_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        Assert.Throws<InvalidOperationException>(() => sut.ParseSoapUserResponse("<not-valid-xml"));
    }

    [Fact]
    public void ParseSoapUserResponse_NoSoapBody_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var responseBody = "<root><body/></root>";

        Assert.Throws<InvalidOperationException>(() => sut.ParseSoapUserResponse(responseBody));
    }

    [Fact]
    public void ExtractIdentifierFromSoapResponse_WhenIdentifierMissing_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var responseBody = """
			<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
			  <soap:Body>
				<GetUserResponse>
				  <UserName>alice</UserName>
				</GetUserResponse>
			  </soap:Body>
			</soap:Envelope>
			""";

        var appConfig = CreateAppConfig();
        Assert.Throws<InvalidOperationException>(() => sut.ExtractIdentifierFromSoapResponse(responseBody, appConfig));
    }

    [Fact]
    public void ExtractIdentifierFromSoapResponse_WhenIdentifierPresent_ReturnsIdentifier()
    {
        var sut = CreateSut();
        var responseBody = """
			<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
			  <soap:Body>
				<GetUserResponse>
				  <Identifier>U-901</Identifier>
				</GetUserResponse>
			  </soap:Body>
			</soap:Envelope>
			""";

        var appConfig = CreateAppConfig();
        var result = sut.ExtractIdentifierFromSoapResponse(responseBody, appConfig);

        Assert.Equal("U-901", result);
    }

    [Fact]
    public async Task GetAsync_WhenTemplateMissingForGet_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<Delete>{{Identifier}}</Delete>", SOAPActions.Delete)
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetAsync("U-100", appConfig, "corr-1"));
    }

    [Fact]
    public async Task GetAsync_WithValidTemplateAndResponse_ReturnsMappedUser()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
					<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
					  <soap:Body>
						<GetUserResponse>
						  <Identifier>U-555</Identifier>
						  <UserName>test.soap</UserName>
						  <DisplayName>Test Soap</DisplayName>
						</GetUserResponse>
					  </soap:Body>
					</soap:Envelope>
					""", Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<GetUser><Identifier>{{Identifier}}</Identifier></GetUser>", SOAPActions.Get)
            },
            schema: new List<AttributeSchema>
            {
                new()
                {
                    DestinationField = "Identifier",
                    SourceValue = "Identifier",
                    MappingType = MappingTypes.Direct,
                    HttpRequestType = HttpRequestTypes.GET
                }
            });

        var result = await sut.GetAsync("U-555", appConfig, "corr-2");

        Assert.Equal("U-555", result.Identifier);
        Assert.Equal("test.soap", result.UserName);
        Assert.Equal("Test Soap", result.DisplayName);
        Assert.Contains("U-555", handler.LastRequestBody);
    }

    [Fact]
    public async Task DeleteAsync_WhenTemplateMissingForDelete_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<GetUser/>", SOAPActions.Get)
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync("U-777", appConfig, "corr-3"));
    }

    [Fact]
    public async Task DeleteAsync_WithSoapFaultResponse_ThrowsHttpRequestException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
					<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
					  <soap:Body>
						<soap:Fault>
						  <faultcode>soap:Server</faultcode>
						  <faultstring>Failure</faultstring>
						</soap:Fault>
					  </soap:Body>
					</soap:Envelope>
					""", Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<DeleteUser><Identifier>{{Identifier}}</Identifier></DeleteUser>", SOAPActions.Delete)
            },
            schema: new List<AttributeSchema>
            {
                new()
                {
                    DestinationField = "Identifier",
                    SourceValue = "Identifier",
                    MappingType = MappingTypes.Direct,
                    HttpRequestType = HttpRequestTypes.DELETE
                }
            });

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.DeleteAsync("U-777", appConfig, "corr-4"));
    }

    [Fact]
    public async Task ProvisionAsync_WithHttpFailure_ThrowsHttpRequestException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad request", Encoding.UTF8, "text/plain")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.ProvisionAsync("<CreateUser />", appConfig, "corr-5"));
    }

    [Fact]
    public async Task ProvisionAsync_WithSuccessfulResponse_ReturnsIdentifier()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
					<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
					  <soap:Body>
						<CreateUserResponse>
						  <Identifier>U-909</Identifier>
						</CreateUserResponse>
					  </soap:Body>
					</soap:Envelope>
					""", Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();

        var result = await sut.ProvisionAsync("<CreateUser />", appConfig, "corr-6");

        Assert.NotNull(result);
        Assert.Equal("U-909", result!.Identifier);
    }

    [Fact]
    public async Task ProvisionAsync_WithBasicAuth_SetsBasicAuthorizationHeader()
    {
        var handler = new TestHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                                        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                                            <soap:Body>
                                                <CreateUserResponse>
                                                    <Identifier>U-1001</Identifier>
                                                </CreateUserResponse>
                                            </soap:Body>
                                        </soap:Envelope>
                                        """, Encoding.UTF8, "text/xml")
                });

        var sut = CreateSut(handler, "dXNlcjpwYXNz");
        var appConfig = CreateAppConfig(
                authMethodOutbound: AuthenticationMethods.Basic,
                authDetails: new { Username = "user", Password = "pass" });

        await sut.ProvisionAsync("<CreateUser />", appConfig, "corr-basic");

        Assert.NotNull(handler.LastAuthorizationHeader);
        Assert.Equal("Basic", handler.LastAuthorizationHeader!.Scheme);
        Assert.Equal("dXNlcjpwYXNz", handler.LastAuthorizationHeader.Parameter);
    }

    [Fact]
    public async Task ProvisionAsync_WithNtlmEnabledAndMissingCredentials_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
                authMethodOutbound: AuthenticationMethods.None,
                authenticationFlow: new AuthenticationFlow
                {
                    AppId = "soap-app",
                    Name = "NTLM Flow",
                    Steps = [CreateSoapFlowStep(new SOAPAuthenticationOptions
                    {
                        Transport = new BasicOrNtlmSoapAuthOptions
                        {
                            Enabled = true,
                            UseNtlm = true,
                            UseDefaultCredentials = false
                        }
                    })]
                });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.ProvisionAsync("<CreateUser />", appConfig, "corr-ntlm-invalid"));
    }

    [Fact]
    public async Task ProvisionAsync_WithWsSecurityUsernameToken_AddsWsseHeaderToPayload()
    {
        var handler = new TestHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                                        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                                            <soap:Body>
                                                <CreateUserResponse>
                                                    <Identifier>U-2002</Identifier>
                                                </CreateUserResponse>
                                            </soap:Body>
                                        </soap:Envelope>
                                        """, Encoding.UTF8, "text/xml")
                });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig(
                authMethodOutbound: AuthenticationMethods.None,
                authenticationFlow: new AuthenticationFlow
                {
                    AppId = "soap-app",
                    Name = "WsSecurity Flow",
                    Steps = [CreateSoapFlowStep(new SOAPAuthenticationOptions
                    {
                        WsSecurity = new WsSecuritySoapAuthOptions
                        {
                            Enabled = true,
                            Username = "ws-user",
                            Password = "ws-pass",
                            IncludeTimestamp = true
                        }
                    })]
                });

        const string payload = """
                        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                            <soap:Body>
                                <CreateUser />
                            </soap:Body>
                        </soap:Envelope>
                        """;

        await sut.ProvisionAsync(payload, appConfig, "corr-wsse");

        Assert.Contains("wsse:Security", handler.LastRequestBody);
        Assert.Contains("wsse:Username", handler.LastRequestBody);
        Assert.Contains("ws-user", handler.LastRequestBody);
        Assert.Contains("wsu:Timestamp", handler.LastRequestBody);
    }

    [Fact]
    public async Task ProvisionAsync_WithTokenPlacement_AddsAuthorizationCustomHttpAndSoapHeaders()
    {
        var handler = new TestHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                                        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                                            <soap:Body>
                                                <CreateUserResponse>
                                                    <Identifier>U-3003</Identifier>
                                                </CreateUserResponse>
                                            </soap:Body>
                                        </soap:Envelope>
                                        """, Encoding.UTF8, "text/xml")
                });

        var sut = CreateSut(handler, "token-123");
        var appConfig = CreateAppConfig(
                authMethodOutbound: AuthenticationMethods.Bearer,
                authenticationFlow: new AuthenticationFlow
                {
                    AppId = "soap-app",
                    Name = "Token Placement Flow",
                    Steps = [CreateSoapFlowStep(new SOAPAuthenticationOptions
                    {
                        TokenPlacement = new SoapTokenPlacementOptions
                        {
                            Enabled = true,
                            UseAuthorizationHeader = true,
                            CustomHttpHeaders = new Dictionary<string, string>
                            {
                                ["X-SOAP-Token"] = "{{token}}"
                            },
                            SoapHeaderTemplate = "<auth:Token xmlns:auth='urn:test'>{{token}}</auth:Token>"
                        }
                    })]
                });

        const string payload = """
                        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                            <soap:Body>
                                <CreateUser />
                            </soap:Body>
                        </soap:Envelope>
                        """;

        await sut.ProvisionAsync(payload, appConfig, "corr-token");

        Assert.NotNull(handler.LastAuthorizationHeader);
        Assert.Equal("Bearer", handler.LastAuthorizationHeader!.Scheme);
        Assert.Equal("token-123", handler.LastAuthorizationHeader.Parameter);
        Assert.True(handler.LastHeaders.TryGetValue("X-SOAP-Token", out var customToken));
        Assert.Equal("token-123", customToken);
        Assert.Contains("auth:Token", handler.LastRequestBody);
        Assert.Contains("token-123", handler.LastRequestBody);
    }

    [Fact]
    public void ConfigureMapperServices_RegistersSoapAuthAndSoapIntegrationServices()
    {
        var services = new ServiceCollection();
        services.Configure<AppSettings>(options =>
        {
            options.IntegrationMappings.DefaultIntegration[IntegrationMethods.SOAP.ToString()] = nameof(global::KN.KloudIdentity.Mapper.MapperCore.SOAPIntegration);
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        services.ConfigureMapperServices(configuration);

        Assert.Contains(services, d => d.ServiceType == typeof(IAuthStrategy) && d.ImplementationType == typeof(BearerAuthStratergy));
        Assert.Contains(services, d => d.ServiceType == typeof(IIntegrationBaseV2) && d.ImplementationType == typeof(global::KN.KloudIdentity.Mapper.MapperCore.SOAPIntegration));
        Assert.Contains(services, d => d.ServiceType == typeof(ISoapAuthApplier) && d.ImplementationType == typeof(SoapTransportAuthApplier));
        Assert.Contains(services, d => d.ServiceType == typeof(ISoapAuthApplier) && d.ImplementationType == typeof(WsSecuritySoapAuthApplier));
        Assert.Contains(services, d => d.ServiceType == typeof(ISoapAuthApplier) && d.ImplementationType == typeof(SoapTokenHeaderApplier));
    }

    [Fact]
    public void IntegrationBaseFactory_WithSoapDefaultMapping_ResolvesSoapIntegration()
    {
        var soapIntegration = CreateSut();
        var appSettings = Options.Create(new AppSettings
        {
            IntegrationMappings = new IntegrationMappings
            {
                DefaultIntegration = new Dictionary<string, string>
                {
                    [IntegrationMethods.SOAP.ToString()] = nameof(global::KN.KloudIdentity.Mapper.MapperCore.SOAPIntegration)
                }
            }
        });

        var factory = new IntegrationBaseFactory(new List<IIntegrationBaseV2> { soapIntegration }, appSettings);

        var resolved = factory.GetIntegration(IntegrationMethods.SOAP, "soap-app");
        Assert.IsType<global::KN.KloudIdentity.Mapper.MapperCore.SOAPIntegration>(resolved);
    }

    #region SOAPAuthenticationOptions resolution from AuthenticationFlow steps

    [Fact]
    public async Task SOAPAuthOptions_ResolvedFromFlowStep_DirectShape_AddsWsseHeaderToPayload()
    {
        // AuthenticationDetails is a SOAPAuthenticationOptions object directly (no wrapper key).
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body><CreateUserResponse><Identifier>U-DS-1</Identifier></CreateUserResponse></soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var flow = new AuthenticationFlow
        {
            AppId = "soap-app",
            Name = "SOAP Direct Shape Flow",
            Steps = [CreateSoapFlowStep(new SOAPAuthenticationOptions
            {
                WsSecurity = new WsSecuritySoapAuthOptions
                {
                    Enabled = true,
                    Username = "flow-direct-user",
                    Password = "flow-direct-pass"
                }
            })]
        };

        var appConfig = CreateAppConfig(authenticationFlow: flow);

        const string payload = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body><CreateUser /></soap:Body>
            </soap:Envelope>
            """;

        await sut.ProvisionAsync(payload, appConfig, "corr-flow-direct");

        Assert.Contains("wsse:Security", handler.LastRequestBody);
        Assert.Contains("flow-direct-user", handler.LastRequestBody);
    }

    [Fact]
    public async Task SOAPAuthOptions_ResolvedFromFlowStep_NestedKeyShape_AddsWsseHeaderToPayload()
    {
        // AuthenticationDetails wraps SOAPAuthenticationOptions under the "SOAPAuthenticationOptions" key.
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body><CreateUserResponse><Identifier>U-NK-1</Identifier></CreateUserResponse></soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var flow = new AuthenticationFlow
        {
            AppId = "soap-app",
            Name = "SOAP Nested Key Flow",
            Steps = [CreateSoapFlowStep(new
            {
                SOAPAuthenticationOptions = new SOAPAuthenticationOptions
                {
                    WsSecurity = new WsSecuritySoapAuthOptions
                    {
                        Enabled = true,
                        Username = "flow-nested-user",
                        Password = "flow-nested-pass"
                    }
                }
            })]
        };

        var appConfig = CreateAppConfig(authenticationFlow: flow);

        const string payload = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body><CreateUser /></soap:Body>
            </soap:Envelope>
            """;

        await sut.ProvisionAsync(payload, appConfig, "corr-flow-nested");

        Assert.Contains("wsse:Security", handler.LastRequestBody);
        Assert.Contains("flow-nested-user", handler.LastRequestBody);
    }

    [Fact]
    public async Task SOAPAuthOptions_ResolvedFromFlowStep_WsSecurityCredentialsAreApplied()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body><CreateUserResponse><Identifier>U-PRI-1</Identifier></CreateUserResponse></soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var flow = new AuthenticationFlow
        {
            AppId = "soap-app",
            Name = "SOAP Priority Flow",
            Steps = [CreateSoapFlowStep(new SOAPAuthenticationOptions
            {
                WsSecurity = new WsSecuritySoapAuthOptions
                {
                    Enabled = true,
                    Username = "flow-step-user",
                    Password = "flow-step-pass"
                }
            })]
        };

        var appConfig = CreateAppConfig(authenticationFlow: flow);

        const string payload = """
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body><CreateUser /></soap:Body>
            </soap:Envelope>
            """;

        await sut.ProvisionAsync(payload, appConfig, "corr-flow-priority");

        Assert.Contains("wsse:Security", handler.LastRequestBody);
        Assert.Contains("flow-step-user", handler.LastRequestBody);
    }

    #endregion

    #region V2 ActionStep-aware tests

    [Fact]
    public async Task ProvisionAsyncV2_WithNullActionStep_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.ProvisionAsync("<CreateUser />", "soap-app", appConfig, null!, "corr-v2-1"));
    }

    [Fact]
    public async Task ProvisionAsyncV2_WithEmptyEndpoint_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(endpoint: "");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ProvisionAsync("<CreateUser />", "soap-app", appConfig, step, "corr-v2-2"));
    }

    [Fact]
    public async Task ProvisionAsyncV2_WithValidStep_ReturnsIdentifier()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body>
                        <CreateUserResponse>
                          <Identifier>V2-100</Identifier>
                        </CreateUserResponse>
                      </soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(endpoint: "https://soap.example.test/v2/users", httpVerb: HttpVerbs.POST);

        var result = await sut.ProvisionAsync("<CreateUser />", "soap-app", appConfig, step, "corr-v2-3");

        Assert.NotNull(result);
        Assert.Equal("V2-100", result!.Identifier);
    }

    [Fact]
    public async Task GetAsyncV2_WithNullActionStep_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.GetAsync("U-1", appConfig, null!, "corr-v2-4"));
    }

    [Fact]
    public async Task GetAsyncV2_WithEmptyEndpoint_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(endpoint: "", httpVerb: HttpVerbs.GET);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetAsync("U-1", appConfig, step, "corr-v2-5"));
    }

    [Fact]
    public async Task GetAsyncV2_WithNoAttributesOnStep_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<GetUser><Identifier>{{Identifier}}</Identifier></GetUser>", SOAPActions.Get)
            });
        var step = CreateActionStep(
            endpoint: "https://soap.example.test/v2/users/get",
            httpVerb: HttpVerbs.GET,
            attributes: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetAsync("U-1", appConfig, step, "corr-v2-6"));
    }

    [Fact]
    public async Task GetAsyncV2_WithValidStepAndTemplate_ReturnsMappedUser()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body>
                        <GetUserResponse>
                          <Identifier>V2-GET-1</Identifier>
                          <UserName>soap.get</UserName>
                        </GetUserResponse>
                      </soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var stepAttributes = new List<AttributeSchema>
        {
            new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct, HttpRequestType = HttpRequestTypes.GET }
        };
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<GetUser><Identifier>{{Identifier}}</Identifier></GetUser>", SOAPActions.Get)
            });
        var step = CreateActionStep(
            endpoint: "https://soap.example.test/v2/users/get",
            httpVerb: HttpVerbs.GET,
            attributes: stepAttributes);

        var result = await sut.GetAsync("V2-GET-1", appConfig, step, "corr-v2-7");

        Assert.Equal("V2-GET-1", result.Identifier);
        Assert.Equal("soap.get", result.UserName);
    }

    [Fact]
    public async Task ReplaceAsyncV2_WithNullActionStep_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.ReplaceAsync("<Update />", new Core2EnterpriseUser { Identifier = "U-1" }, "soap-app", appConfig, null!, "corr-v2-8"));
    }

    [Fact]
    public async Task ReplaceAsyncV2_WithValidStep_ReturnsUserWithUpdatedIdentifier()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body>
                        <ReplaceResponse>
                          <Identifier>V2-REPLACED</Identifier>
                        </ReplaceResponse>
                      </soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(endpoint: "https://soap.example.test/v2/users/replace", httpVerb: HttpVerbs.PUT);
        var resource = new Core2EnterpriseUser { Identifier = "V2-OLD" };

        var result = await sut.ReplaceAsync("<Replace />", resource, "soap-app", appConfig, step, "corr-v2-9");

        Assert.Equal("V2-REPLACED", result.Identifier);
    }

    [Fact]
    public async Task UpdateAsyncV2_WithNullActionStep_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.UpdateAsync("<Update />", new Core2EnterpriseUser(), "soap-app", appConfig, null!, "corr-v2-10"));
    }

    [Fact]
    public async Task UpdateAsyncV2_WithValidStep_CompletesWithoutException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body><UpdateResponse/></soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(endpoint: "https://soap.example.test/v2/users/update", httpVerb: HttpVerbs.PATCH);

        await sut.UpdateAsync("<Update />", new Core2EnterpriseUser(), "soap-app", appConfig, step, "corr-v2-11");
    }

    [Fact]
    public async Task DeleteAsyncV2_WithNullActionStep_ThrowsArgumentNullException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            sut.DeleteAsync("U-1", "soap-app", appConfig, null!, "corr-v2-12"));
    }

    [Fact]
    public async Task DeleteAsyncV2_WithEmptyEndpoint_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig();
        var step = CreateActionStep(endpoint: "", httpVerb: HttpVerbs.DELETE);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync("U-1", "soap-app", appConfig, step, "corr-v2-13"));
    }

    [Fact]
    public async Task DeleteAsyncV2_WithMissingTemplate_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<GetUser/>", SOAPActions.Get)
            });
        var step = CreateActionStep(
            endpoint: "https://soap.example.test/v2/users/delete",
            httpVerb: HttpVerbs.DELETE,
            attributes: new List<AttributeSchema>
            {
                new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct, HttpRequestType = HttpRequestTypes.DELETE }
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync("U-1", "soap-app", appConfig, step, "corr-v2-14"));
    }

    [Fact]
    public async Task DeleteAsyncV2_WithValidStepAndTemplate_CompletesSuccessfully()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body><DeleteResponse/></soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<DeleteUser><Identifier>{{Identifier}}</Identifier></DeleteUser>", SOAPActions.Delete)
            });
        var step = CreateActionStep(
            endpoint: "https://soap.example.test/v2/users/delete",
            httpVerb: HttpVerbs.DELETE,
            attributes: new List<AttributeSchema>
            {
                new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct, HttpRequestType = HttpRequestTypes.DELETE }
            });

        await sut.DeleteAsync("U-999", "soap-app", appConfig, step, "corr-v2-15");
    }

    [Fact]
    public async Task DeleteAsyncV2_WithSoapFaultResponse_ThrowsHttpRequestException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body>
                        <soap:Fault>
                          <faultcode>soap:Server</faultcode>
                          <faultstring>Delete failed</faultstring>
                        </soap:Fault>
                      </soap:Body>
                    </soap:Envelope>
                    """, Encoding.UTF8, "text/xml")
            });

        var sut = CreateSut(handler);
        var appConfig = CreateAppConfig(
            templates: new List<SOAPTemplate>
            {
                new("<DeleteUser><Identifier>{{Identifier}}</Identifier></DeleteUser>", SOAPActions.Delete)
            });
        var step = CreateActionStep(
            endpoint: "https://soap.example.test/v2/users/delete",
            httpVerb: HttpVerbs.DELETE,
            attributes: new List<AttributeSchema>
            {
                new() { DestinationField = "Identifier", SourceValue = "Identifier", MappingType = MappingTypes.Direct, HttpRequestType = HttpRequestTypes.DELETE }
            });

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            sut.DeleteAsync("U-999", "soap-app", appConfig, step, "corr-v2-16"));
    }

    #endregion

    private static ActionStep CreateActionStep(
        string endpoint = "https://soap.example.test/v2/action",
        HttpVerbs httpVerb = HttpVerbs.POST,
        ICollection<AttributeSchema>? attributes = null)
    {
        return new ActionStep
        {
            EndPoint = endpoint,
            HttpVerb = httpVerb,
            StepOrder = 1,
            IsMandatory = true,
            UserAttributeSchemas = attributes
        };
    }

    private static global::KN.KloudIdentity.Mapper.MapperCore.SOAPIntegration CreateSut(TestHttpMessageHandler? handler = null, string token = "test-token")
    {
        handler ??= new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
					<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
					  <soap:Body>
						<Result/>
					  </soap:Body>
					</soap:Envelope>
					""", Encoding.UTF8, "text/xml")
            });

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler));

        var authContextMock = new Mock<IAuthContext>();
        authContextMock
            .Setup(context => context.GetTokenListAsync(It.IsAny<object>(), It.IsAny<SCIMDirections>()))
            .ReturnsAsync(new Dictionary<int, string> { { 1, token } });

        var options = Options.Create(new AppSettings());
        var configuration = new ConfigurationBuilder().Build();
        var loggerMock = new Mock<IKloudIdentityLogger>();

        return new global::KN.KloudIdentity.Mapper.MapperCore.SOAPIntegration(
            authContextMock.Object,
            httpClientFactoryMock.Object,
            configuration,
            options,
            loggerMock.Object);
    }

    private static AppConfig CreateAppConfig(ICollection<SOAPTemplate>? templates = null,
        ICollection<AttributeSchema>? schema = null,
        AuthenticationMethods authMethodOutbound = AuthenticationMethods.None,
        dynamic? authDetails = null,
        AuthenticationFlow? authenticationFlow = null)
    {
        return new AppConfig
        {
            AppId = "soap-app",
            IntegrationMethodOutbound = IntegrationMethods.SOAP,
            AuthenticationMethodOutbound = authMethodOutbound,
            AuthenticationDetails = authDetails ?? new { },
            UserAttributeSchemas = schema ?? new List<AttributeSchema>(),
            UserURIs = new List<UserURIs>
            {
                new()
                {
                    AppId = "soap-app",
                    BaseUrl = "https://soap.example.test",
                    Post = new Uri("https://soap.example.test/users"),
                    Get = new Uri("https://soap.example.test/users/get"),
                    Put = new Uri("https://soap.example.test/users/put"),
                    Patch = new Uri("https://soap.example.test/users/patch"),
                    Delete = new Uri("https://soap.example.test/users/delete")
                }
            },
            SOAPTemplates = templates,
            AuthenticationFlow = authenticationFlow
        };
    }

    private static AuthenticationFlowStep CreateSoapFlowStep(dynamic authenticationDetails, int stepOrder = 1) =>
        new()
        {
            StepTitle = "SOAP Auth",
            StepOrder = stepOrder,
            AuthenticationMethod = AuthenticationMethods.None,
            IsRequired = true,
            AuthenticationDetails = authenticationDetails
        };

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public string LastRequestBody { get; private set; } = string.Empty;
        public AuthenticationHeaderValue? LastAuthorizationHeader { get; private set; }
        public Dictionary<string, string> LastHeaders { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            LastAuthorizationHeader = request.Headers.Authorization;
            LastHeaders = request.Headers
                .ToDictionary(
                    h => h.Key,
                    h => string.Join(",", h.Value),
                    StringComparer.OrdinalIgnoreCase);

            return _responseFactory(request);
        }
    }
}
