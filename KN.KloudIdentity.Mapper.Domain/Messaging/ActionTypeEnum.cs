namespace KN.KloudIdentity.Mapper.Domain.Messaging;

public enum ActionType
{
    None,
    GetFullApplication,
    GetApplicationSetting,
    ListInboundApplications,
    GetInboundConfigurations,
    ListAs400Groups,
    LicenseStatusCheck,
    ListApplicationConfigs,
    // SCIM user lifecycle actions
    GetUser,
    CreateUser,
    EditUser,
    DisableUser,
}
