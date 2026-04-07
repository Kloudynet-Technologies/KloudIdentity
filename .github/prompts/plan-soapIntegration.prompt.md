## Plan: Generic SOAP Integration Method

This plan outlines the creation of a new, generic SOAP integration method for the SCIM Connector Service. The new integration will implement the `IIntegrationBase` interface, enabling it to be used interchangeably with existing integration methods (REST, AS400, Linux, SQL). The implementation will leverage `SOAPParserUtil` to dynamically build SOAP XML payloads from configuration and templates, supporting a wide range of SOAP-based LOB applications. The integration will be compatible with user operations (Create, Update, Delete, Get) as defined in the `User` folder.

**Steps**

1. **Create SOAPIntegration Class**
   - Add `SOAPIntegration.cs` in `MapperCore/IntegrationMethods`.
   - Implement `IIntegrationBase` and set `IntegrationMethod = IntegrationMethods.SOAP`.
   - Inject required dependencies (auth, config, logger, etc.) similar to `RESTIntegration`.

2. **Payload Mapping and Preparation**
   - Implement `MapAndPreparePayloadAsync`:
     - Retrieve the SOAP XML template and mapping config from the app configuration.
     - Use `SOAPParserUtil<T>.BuildPayload` to generate the payload XML string.
     - Return the XML payload (as string or appropriate type for HTTP content).

3. **Authentication Handling**
   - Implement `GetAuthenticationAsync`:
     - Support token or credential retrieval as required by the SOAP endpoint.
     - Reuse or adapt logic from `RESTIntegration` for token handling if applicable.

4. **Provision (Create) Operation**
   - Implement `ProvisionAsync`:
     - Build the SOAP payload.
     - Create and configure an `HttpClient` for SOAP (set headers, content type `text/xml` or `application/soap+xml`).
     - POST the payload to the SOAP endpoint.
     - Parse the SOAP response to extract identifiers or error details.
     - Log and handle errors as in other integrations.

5. **Validation**
   - Implement `ValidatePayloadAsync`:
     - Optionally validate the generated XML against schema or required fields.
     - Return validation status and errors.

6. **Get (Read) Operation**
   - Implement `GetAsync`:
     - Build and send a SOAP request to fetch user/resource by identifier.
     - Parse the SOAP response and map to `Core2EnterpriseUser`.

7. **Replace/Update/Delete Operations**
   - Implement `ReplaceAsync`, `UpdateAsync`, `DeleteAsync`:
     - Build the appropriate SOAP payload for each operation.
     - Send the request to the correct endpoint.
     - Handle and parse responses, logging as needed.

8. **Configuration and Extensibility**
   - Ensure the implementation reads endpoint URLs, templates, and mappings from `AppConfig` or similar configuration.
   - Support custom headers, authentication, and content types as needed for different SOAP services.

9. **Testing and Integration**
   - Update or add tests in `KN.KloudIdentity.MapperTests` to cover the new integration.
   - Ensure user operation classes (e.g., `CreateUserV2`, `DeleteUserV2`) can invoke the new SOAP integration via the factory.

**Verification**

- Manual: Configure a sample SOAP LOB app and verify Create, Get, Update, Delete operations via the SCIM connector.
- Automated: Add/extend unit and integration tests for SOAP integration.
- Logging: Check logs for correct request/response handling and error reporting.
- Extensibility: Validate with at least two different SOAP service schemas/templates.

**Decisions**

- Chose a generic, template-driven approach using `SOAPParserUtil` for maximum flexibility.
- Configuration-driven endpoints and mappings to support multiple SOAP LOB apps.
- Reuse authentication and HTTP client patterns from `RESTIntegration` for consistency.
