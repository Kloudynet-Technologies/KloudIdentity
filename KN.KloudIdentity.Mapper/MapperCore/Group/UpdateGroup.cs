//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Config;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
using Microsoft.SCIM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KN.KloudIdentity.Mapper.MapperCore.Group
{
    /// <summary>
    /// Represents a class for partaily updating group information.
    /// </summary>
    public class UpdateGroup : OperationsBase<Core2Group>, IUpdateResource<Core2Group>
    {
        private AppConfig _appConfig;
        private readonly IAddGroupMembers _addGroupMembers;
        private readonly IRemoveGroupMembers _removeGroupMembers;
        private readonly IRemoveAllGroupMembers _removeAllGroupMembers;

        /// <summary>
        /// Initializes a new instance of the UpdateGroup class.
        /// </summary>
        /// <param name="configReader">An implementation of IConfigReader for reading configuration settings.</param>
        /// <param name="authContext">An implementation of IAuthContext for handling authentication.</param>
        /// <param name="addGroupMembers">An implementation of IAddGroupMembers for adding members to a group.</param>
        /// <param name="removeGroupMembers">An implementation of IRemoveGroupMembers for removing specific members from a group.</param>
        /// <param name="removeAllGroupMembers">An implementation of IRemoveAllGroupMembers for removing all members from a group.</param>
        public UpdateGroup(
            IAuthContext authContext,
            IAddGroupMembers addGroupMembers,
            IRemoveGroupMembers removeGroupMembers,
            IRemoveAllGroupMembers removeAllGroupMembers,
            IGetFullAppConfigQuery getFullAppConfigQuery)
            : base(authContext, getFullAppConfigQuery)
        {
            _addGroupMembers = addGroupMembers;
            _removeGroupMembers = removeGroupMembers;
            _removeAllGroupMembers = removeAllGroupMembers;
        }

        /// <summary>
        /// Updates a group asynchronously based on the provided patch operation.
        /// </summary>
        /// <param name="patch">The patch operation to apply to the group.</param>
        /// <param name="appId">The ID of the application.</param>
        /// <param name="correlationID">The correlation ID for tracking the operation.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public async Task UpdateAsync(IPatch patch, string appId, string correlationID)
        {
            PatchRequest2 patchRequest = patch.PatchRequest as PatchRequest2;

            var addMembers = new List<string>();
            var removeMembers = new List<string>();

            var groupId = patch.ResourceIdentifier.Identifier;

            foreach (PatchOperation2Combined operation in patchRequest.Operations)
            {
                switch (operation.OperationName)
                {
                    case "Add":
                        ProcessAddOperation(operation.Value, addMembers);
                        break;

                    case "Remove":
                        ProcessRemoveOperation(operation.Value, removeMembers);
                        break;

                    default:
                        break;
                }
            }

            if (addMembers.Count > 0)
            {
                await _addGroupMembers.AddAsync(groupId, addMembers, appId, correlationID);
            }

            if (removeMembers.Count > 0)
            {
                if (removeMembers[0] == "all")
                {
                    await _removeAllGroupMembers.RemoveAsync(groupId, appId, correlationID);
                }
                else
                {
                    await _removeGroupMembers.RemoveAsync(groupId, removeMembers, appId, correlationID);
                }
            }
        }

        /// <summary>
        /// Processes the "add" operation and populates the list of members to be added.
        /// </summary>
        /// <param name="value">The value associated with the "add" operation.</param>
        /// <param name="addMembers">List to store members to be added.</param>
        private void ProcessAddOperation(string value, List<string> addMembers)
        {
            dynamic data = JsonConvert.DeserializeObject(value);

            if (data is JArray)
            {
                foreach (var item in data)
                {
                    string member = item.value;
                    addMembers.Add(member);
                }
            }

        }

        /// <summary>
        /// Processes the "remove" operation and populates the list of members to be removed.
        /// </summary>
        /// <param name="value">The value associated with the "remove" operation.</param>
        /// <param name="removeMembers">List to store members to be removed.</param>
        private void ProcessRemoveOperation(string value, List<string> removeMembers)
        {
            if (string.IsNullOrEmpty(value))
            {
                removeMembers.Add("all");
            }
            else
            {
                dynamic data = JsonConvert.DeserializeObject(value);
                removeMembers.Add(data);
            }
        }
    }

}
