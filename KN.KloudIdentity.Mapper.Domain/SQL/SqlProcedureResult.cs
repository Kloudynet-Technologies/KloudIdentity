namespace KN.KloudIdentity.Mapper.Domain.SQL;

/// <summary>
/// Outcome of a named stored procedure call.
/// </summary>
/// <param name="ResponseCode">Value of the "Output:ResponseCode" parameter; null when no such output is mapped.</param>
/// <param name="ResponseMessage">Value of the "Output:ResponseMessage" parameter; null when not mapped or not set.</param>
/// <param name="Row">First row of the result set returned by the procedure (column name → value, DBNull → null); null when none.</param>
/// <param name="Outputs">All captured output values keyed by SP parameter name (e.g. "@ResponseCode").</param>
public record SqlProcedureResult(
    int? ResponseCode,
    string? ResponseMessage,
    IReadOnlyDictionary<string, object?>? Row,
    IReadOnlyDictionary<string, object?> Outputs);

/// <summary>
/// Default status-code convention of stored procedures that report their outcome through an
/// "Output:ResponseCode" parameter instead of raising errors.
/// </summary>
public enum SqlProcedureResponseCode
{
    Success = 0,
    ValidationError = 1,
    NotFound = 2,
    Conflict = 3,
    SystemError = 4
}
