namespace KN.KloudIdentity.Mapper.Domain.Mapping
{
    public class VerifyMappingRequest
    {
        public required string AppId { get; set; } 
        public ObjectTypes Type { get; set; } 
        public SCIMDirections Direction { get; set; }
        public HttpRequestTypes HttpRequestType { get; set; }
    }
}
