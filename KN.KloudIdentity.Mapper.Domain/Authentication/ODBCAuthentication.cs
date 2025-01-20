namespace KN.KloudIdentity.Mapper.Domain.Authentication;

public record ODBCAuthentication
{
    /// <summary>
    /// ODBC Driver
    /// </summary>
    public string Driver { get; init; }

    /// <summary>
    /// Server Name
    /// </summary>
    public string Server { get; init; }

    /// <summary>
    /// Database
    /// </summary>
    public string Database { get; init; }

    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; init; }
}
