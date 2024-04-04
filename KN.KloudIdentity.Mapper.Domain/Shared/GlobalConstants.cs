namespace KN.KloudIdentity.Mapper.Domain;

public class GlobalConstants
{
    public const string QUEUE_NAME_IN = "scimsvc-in";
    public const string QUEUE_NAME_OUT = "scimsvc-out";
    public const string MGTPORTAL_IN = "mgtportal-in";
    public const string MGTPORTAL_OUT = "mgtportal-out";
    public const string LOGS_IN = "logs-in";
    public const string LOGS_OUT = "logs-out";

    public readonly Dictionary<string, string> QUEUE_NAMES = new Dictionary<string, string>
    {
        { "QUEUE_NAME_IN", QUEUE_NAME_IN },
        { "QUEUE_NAME_OUT", QUEUE_NAME_OUT },
        { "MGTPORTAL_IN", MGTPORTAL_IN },
        { "MGTPORTAL_OUT", MGTPORTAL_OUT },
        { "LOGS_IN", LOGS_IN },
        { "LOGS_OUT", LOGS_OUT }
    };
}
