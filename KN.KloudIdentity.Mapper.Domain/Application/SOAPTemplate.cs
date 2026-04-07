using KN.KloudIdentity.Mapper.Domain.Mapping;

namespace KN.KloudIdentity.Mapper.Domain.Application;

public record SOAPTemplate(
    string Template,
    SOAPActions Action
);