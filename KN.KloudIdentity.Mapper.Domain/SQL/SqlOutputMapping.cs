using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.Domain.SQL;

/// <summary>
/// A stored procedure OUTPUT parameter declared in the attribute mapping through an
/// output-marker row (SourceValue = "Output" or "Output:&lt;Role&gt;").
/// </summary>
/// <param name="ParameterName">SP parameter name (DestinationField without the URN prefix), e.g. "@ResponseCode".</param>
/// <param name="Role">Marker role, e.g. "ResponseCode" / "ResponseMessage"; null for a plain "Output" marker.</param>
/// <param name="DestinationType">Declared data type of the parameter.</param>
/// <param name="SqlType">T-SQL type used to declare the local variable that receives the output value.</param>
public record SqlOutputMapping(string ParameterName, string? Role, AttributeDataTypes DestinationType, string SqlType);
