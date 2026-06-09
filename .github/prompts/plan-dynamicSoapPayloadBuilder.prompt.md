## Plan: Dynamic SOAP Payload Builder (Like JSONParserUtilV2)

To enable dynamic SOAP payload generation (similar to JSONParserUtilV2 for REST/JSON), you need a mechanism to:

- Store SOAP payload templates/configurations in a flexible, maintainable format.
- Dynamically build SOAP XML payloads at runtime using attribute mapping and resource data.

### 1. Configuration Format for SOAP Payloads

**Recommended Approach:**  
Use an XML template with placeholders (tokens) for dynamic values, and a mapping configuration (similar to AttributeSchema) that links SCIM attributes to those placeholders.

**Example Configuration:**

- **SOAP XML Template (as string or file):**

  ```xml
  <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:urn="urn:example">
    <soapenv:Header/>
    <soapenv:Body>
      <urn:CreateUser>
        <urn:UserName>{{UserName}}</urn:UserName>
        <urn:Email>{{Email}}</urn:Email>
        <urn:IsActive>{{IsActive}}</urn:IsActive>
        <!-- More fields as needed -->
      </urn:CreateUser>
    </soapenv:Body>
  </soapenv:Envelope>
  ```

- **Mapping Configuration (JSON or DB, similar to AttributeSchema):**
  ```json
  [
    {
      "Placeholder": "UserName",
      "SourceValue": "userName",
      "MappingType": "Direct",
      "DataType": "String"
    },
    {
      "Placeholder": "Email",
      "SourceValue": "emails[0].value",
      "MappingType": "Direct",
      "DataType": "String"
    },
    {
      "Placeholder": "IsActive",
      "SourceValue": "active",
      "MappingType": "Direct",
      "DataType": "Boolean"
    }
  ]
  ```

### 2. Dynamic SOAP Payload Builder

**How it works:**

- At runtime, load the SOAP XML template and the mapping configuration.
- For each mapping entry:
  - Extract the value from the SCIM resource (using logic similar to JSONParserUtilV2).
  - Replace the corresponding placeholder (e.g., `{{UserName}}`) in the XML template with the actual value.
- The result is a fully-formed SOAP XML payload ready for transmission.

**Implementation Steps:**

1. **SOAPParserUtil (new utility class):**
   - Accepts: XML template, mapping config, resource object.
   - For each mapping, uses reflection/property access to get the value from the resource.
   - Replaces placeholders in the XML template with the extracted values (using string replacement or an XML DOM approach).

2. **Integration with Attribute Mapping:**
   - Reuse or extend the existing AttributeSchema model for mapping.
   - Support for constants, default values, and nested/array properties as in JSONParserUtilV2.

3. **Extensibility:**
   - Support for complex/nested XML structures by allowing nested placeholders or recursive mapping.
   - Optionally, support for XSLT or other XML transformation tools for advanced scenarios.

### 3. Usage Flow

- On a SCIM operation, the SOAP integration method:
  1. Loads the relevant SOAP template and mapping config for the operation.
  2. Calls SOAPParserUtil to build the payload.
  3. Sends the payload to the SOAP endpoint.

---

## Summary Table

| Aspect               | REST/JSON (Current)        | SOAP/XML (Proposed)            |
| -------------------- | -------------------------- | ------------------------------ |
| Payload Format       | JSON                       | XML (with placeholders)        |
| Mapping Config       | AttributeSchema (C#/DB)    | Mapping config (JSON/DB)       |
| Template Location    | N/A (built in code)        | XML template (string/file/DB)  |
| Builder Utility      | JSONParserUtilV2           | SOAPParserUtil (new)           |
| Dynamic Value Insert | Reflection/property access | Placeholder replacement in XML |

---

## Decisions

- Use XML templates with placeholders for maximum flexibility and maintainability.
- Mapping config mirrors AttributeSchema for consistency.
- SOAPParserUtil logic parallels JSONParserUtilV2, but outputs XML.

---

**This approach ensures SOAP payloads are as dynamic and maintainable as your current REST/JSON solution, with minimal duplication and maximum reuse of mapping logic.**
