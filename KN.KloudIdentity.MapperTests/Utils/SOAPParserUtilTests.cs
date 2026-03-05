using System;

using System.Collections.Generic;
using KN.KloudIdentity.Mapper.Utils;
using KN.KloudIdentity.Mapper.Domain.Mapping;
using Microsoft.SCIM;
using Xunit;

namespace KN.KloudIdentity.MapperTests.Utils;

public class SOAPParserUtilTests
{
	public class TestResource : Resource
	{
		public string UserName { get; set; } = "TestUser";
		public string Email { get; set; } = "test@example.com";
		public bool IsActive { get; set; } = true;
		public List<EmailInfo> Emails { get; set; } = new() { new EmailInfo { Value = "primary@example.com" }, new EmailInfo { Value = "secondary@example.com" } };
		public NestedInfo Nested { get; set; } = new NestedInfo { InnerValue = "Inner" };
	}

	public class EmailInfo
	{
		public string Value { get; set; } = "";
	}

	public class NestedInfo
	{
		public string InnerValue { get; set; } = "";
	}

	[Fact]
	public void BuildPayload_DirectMapping_Works()
	{
		var resource = new TestResource();
		var template = "<User><UserName>{{UserName}}</UserName><Email>{{Email}}</Email><IsActive>{{IsActive}}</IsActive></User>";
		var mapping = new List<AttributeSchema>
		{
			new() { DestinationField = "UserName", SourceValue = "UserName", MappingType = MappingTypes.Direct },
			new() { DestinationField = "Email", SourceValue = "Email", MappingType = MappingTypes.Direct },
			new() { DestinationField = "IsActive", SourceValue = "IsActive", MappingType = MappingTypes.Direct }
		};
		var result = SOAPParserUtil<TestResource>.BuildPayload(template, mapping, resource);
		Assert.Contains("<UserName>TestUser</UserName>", result);
		Assert.Contains("<Email>test@example.com</Email>", result);
		Assert.Contains("<IsActive>True</IsActive>", result);
	}

	[Fact]
	public void BuildPayload_ConstantMapping_Works()
	{
		var resource = new TestResource();
		var template = "<User><Role>{{Role}}</Role></User>";
		var mapping = new List<AttributeSchema>
		{
			new() { DestinationField = "Role", SourceValue = "Admin", MappingType = MappingTypes.Constant }
		};
		var result = SOAPParserUtil<TestResource>.BuildPayload(template, mapping, resource);
		Assert.Contains("<Role>Admin</Role>", result);
	}

	[Fact]
	public void BuildPayload_NestedProperty_Works()
	{
		var resource = new TestResource();
		var template = "<User><Inner>{{Nested.InnerValue}}</Inner></User>";
		var mapping = new List<AttributeSchema>
		{
			new() { DestinationField = "Nested.InnerValue", SourceValue = "Nested.InnerValue", MappingType = MappingTypes.Direct }
		};
		var result = SOAPParserUtil<TestResource>.BuildPayload(template, mapping, resource);
		Assert.Contains("<Inner>Inner</Inner>", result);
	}

	[Fact]
	public void BuildPayload_ArrayIndexing_Works()
	{
		var resource = new TestResource();
		var template = "<User><PrimaryEmail>{{Emails[0].Value}}</PrimaryEmail><SecondaryEmail>{{Emails[1].Value}}</SecondaryEmail></User>";
		var mapping = new List<AttributeSchema>
		{
			new() { DestinationField = "Emails[0].Value", SourceValue = "Emails[0].Value", MappingType = MappingTypes.Direct },
			new() { DestinationField = "Emails[1].Value", SourceValue = "Emails[1].Value", MappingType = MappingTypes.Direct }
		};
		var result = SOAPParserUtil<TestResource>.BuildPayload(template, mapping, resource);
		Assert.Contains("<PrimaryEmail>primary@example.com</PrimaryEmail>", result);
		Assert.Contains("<SecondaryEmail>secondary@example.com</SecondaryEmail>", result);
	}

	[Fact]
	public void BuildPayload_MissingProperty_EmptyString()
	{
		var resource = new TestResource();
		var template = "<User><Missing>{{MissingProp}}</Missing></User>";
		var mapping = new List<AttributeSchema>
		{
			new() { DestinationField = "MissingProp", SourceValue = "MissingProp", MappingType = MappingTypes.Direct }
		};
		var result = SOAPParserUtil<TestResource>.BuildPayload(template, mapping, resource);
		Assert.Contains("<Missing></Missing>", result);
	}

	[Fact]
	public void BuildPayload_EmptyValue_EmptyString()
	{
		var resource = new TestResource { UserName = "" };
		var template = "<User><UserName>{{UserName}}</UserName></User>";
		var mapping = new List<AttributeSchema>
		{
			new() { DestinationField = "UserName", SourceValue = "UserName", MappingType = MappingTypes.Direct }
		};
		var result = SOAPParserUtil<TestResource>.BuildPayload(template, mapping, resource);
		Assert.Contains("<UserName></UserName>", result);
	}
}
