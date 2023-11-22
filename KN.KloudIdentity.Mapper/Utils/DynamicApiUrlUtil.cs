namespace KN.KloudIdentity.Mapper.Utils
{
    public class DynamicApiUrlUtil
    {
        public static string GetFullUrl(string baseUrl, params string[] parameterValues)
        {
            return string.Format(baseUrl, parameterValues);
        }
    }
}
