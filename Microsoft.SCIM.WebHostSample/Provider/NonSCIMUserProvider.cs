﻿//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using KN.KloudIdentity.Mapper.MapperCore;
using Microsoft.SCIM.WebHostSample.Provider;

namespace Microsoft.SCIM.WebHostSample;

/// <summary>
/// Represents a provider for non-SCIM application users.
/// </summary>
public class NonSCIMUserProvider : ProviderBase
{
    private readonly ICreateResourceV2 _createUser;
    private readonly IDeleteResourceV2 _deleteUser;
    private readonly IReplaceResourceV2 _replaceUser;
    private readonly IUpdateResourceV2 _updateUser;
    private readonly IGetResourceV2 _getUser;

    public NonSCIMUserProvider(
        ICreateResourceV2 createUser,
        IDeleteResourceV2 deleteUser,
        IReplaceResourceV2 replaceUser,
        IUpdateResourceV2 updateUser,
        IGetResourceV2 getUser
    )
    {
        _createUser = createUser;
        _deleteUser = deleteUser;
        _replaceUser = replaceUser;
        _updateUser = updateUser;
        _getUser = getUser;
    }

    /// <summary>
    /// Creates a new resource asynchronously.
    /// </summary>
    /// <param name="resource">The resource to create.</param>
    /// <param name="correlationIdentifier">The correlation identifier.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the created resource.</returns>
    public override async Task<Resource> CreateAsync(Resource resource, string correlationIdentifier, string appId)
    {
        if (resource.Identifier != null)
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        Core2EnterpriseUser user = resource as Core2EnterpriseUser;
        if (string.IsNullOrWhiteSpace(user.UserName))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // @TODO: Check if user already exists

        // IEnumerable<Core2EnterpriseUser> exisitingUsers = this.storage.Users.Values;
        // if
        // (
        //     exisitingUsers.Any(
        //         (Core2EnterpriseUser exisitingUser) =>
        //             string.Equals(exisitingUser.UserName, user.UserName, StringComparison.Ordinal))
        // )
        // {
        //     throw new HttpResponseException(HttpStatusCode.Conflict);
        // }

        // Update metadata
        DateTime created = DateTime.UtcNow;
        user.Metadata.Created = created;
        user.Metadata.LastModified = created;

        string resourceIdentifier = Guid.NewGuid().ToString();
        resource.Identifier = resourceIdentifier;

        await _createUser.ExecuteAsync(user, appId, correlationIdentifier);

        // this.storage.Users.Add(resourceIdentifier, user);

        return await Task.FromResult(resource);
    }

    /// <summary>
    /// Initiates the asynchronous deletion of a resource using the provided resource identifier and correlation identifier.
    /// </summary>
    /// <param name="resourceIdentifier">The identifier of the resource to be deleted.</param>
    /// <param name="correlationIdentifier">The correlation identifier associated with the operation.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    /// <exception cref="NotImplementedException"></exception>
    [Obsolete("This method is obsolete. Use DeleteAsync(IResourceIdentifier, string, string) instead.", true)]
    public override Task DeleteAsync(
        IResourceIdentifier resourceIdentifier,
        string correlationIdentifier
    )
    {
        throw new NotImplementedException();
    }

    [Obsolete("This method is obsolete. Use RetrieveAsync(IResourceRetrievalParameters, string, string) instead.", true)]
    public override async Task<Resource> RetrieveAsync(
        IResourceRetrievalParameters parameters,
        string correlationIdentifier
    )
    {
        throw new HttpResponseException(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Updates a resource asynchronously.
    /// This method is obsolete. Use UpdateAsync(IPatch, string, string) instead.
    /// </summary>
    /// <param name="patch"></param>
    /// <param name="correlationIdentifier"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    [Obsolete("This method is obsolete. Use UpdateAsync(IPatch, string, string) instead.", true)]
    public override Task UpdateAsync(IPatch patch, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    // @TODO: Implement query
    public override Task<Resource[]> QueryAsync(
        IQueryParameters parameters,
        string correlationIdentifier
    )
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (string.IsNullOrWhiteSpace(correlationIdentifier))
        {
            throw new ArgumentNullException(nameof(correlationIdentifier));
        }

        if (null == parameters.AlternateFilters)
        {
            throw new ArgumentException(
                SystemForCrossDomainIdentityManagementServiceResources.ExceptionInvalidParameters
            );
        }

        if (string.IsNullOrWhiteSpace(parameters.SchemaIdentifier))
        {
            throw new ArgumentException(
                SystemForCrossDomainIdentityManagementServiceResources.ExceptionInvalidParameters
            );
        }

        IEnumerable<Resource> results;
        var predicate = PredicateBuilder.False<Core2EnterpriseUser>();
        Expression<Func<Core2EnterpriseUser, bool>> predicateAnd;

        if (parameters.AlternateFilters.Count <= 0)
        {
            // results = this.storage.Users.Values.Select(
            //     (Core2EnterpriseUser user) => user as Resource);

            results = new List<Resource>();
        }
        else
        {
            foreach (IFilter queryFilter in parameters.AlternateFilters)
            {
                predicateAnd = PredicateBuilder.True<Core2EnterpriseUser>();

                IFilter andFilter = queryFilter;
                IFilter currentFilter = andFilter;
                do
                {
                    if (string.IsNullOrWhiteSpace(andFilter.AttributePath))
                    {
                        throw new ArgumentException(
                            SystemForCrossDomainIdentityManagementServiceResources.ExceptionInvalidParameters
                        );
                    }
                    else if (string.IsNullOrWhiteSpace(andFilter.ComparisonValue))
                    {
                        throw new ArgumentException(
                            SystemForCrossDomainIdentityManagementServiceResources.ExceptionInvalidParameters
                        );
                    }
                    // UserName filter
                    else if (
                        andFilter.AttributePath.Equals(
                            AttributeNames.UserName,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        if (andFilter.FilterOperator != ComparisonOperator.Equals)
                        {
                            throw new NotSupportedException(
                                string.Format(
                                    SystemForCrossDomainIdentityManagementServiceResources.ExceptionFilterOperatorNotSupportedTemplate,
                                    andFilter.FilterOperator
                                )
                            );
                        }

                        string userName = andFilter.ComparisonValue;
                        predicateAnd = predicateAnd.And(
                            p =>
                                string.Equals(
                                    p.UserName,
                                    userName,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        );
                    }
                    // ExternalId filter
                    else if (
                        andFilter.AttributePath.Equals(
                            AttributeNames.ExternalIdentifier,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        if (andFilter.FilterOperator != ComparisonOperator.Equals)
                        {
                            throw new NotSupportedException(
                                string.Format(
                                    SystemForCrossDomainIdentityManagementServiceResources.ExceptionFilterOperatorNotSupportedTemplate,
                                    andFilter.FilterOperator
                                )
                            );
                        }

                        string externalIdentifier = andFilter.ComparisonValue;
                        predicateAnd = predicateAnd.And(
                            p =>
                                string.Equals(
                                    p.ExternalIdentifier,
                                    externalIdentifier,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        );
                    }
                    //Active Filter
                    else if (
                        andFilter.AttributePath.Equals(
                            AttributeNames.Active,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        if (andFilter.FilterOperator != ComparisonOperator.Equals)
                        {
                            throw new NotSupportedException(
                                string.Format(
                                    SystemForCrossDomainIdentityManagementServiceResources.ExceptionFilterOperatorNotSupportedTemplate,
                                    andFilter.FilterOperator
                                )
                            );
                        }

                        bool active = bool.Parse(andFilter.ComparisonValue);
                        predicateAnd = predicateAnd.And(p => p.Active == active);
                    }
                    //LastModified filter
                    else if (
                        andFilter.AttributePath.Equals(
                            $"{AttributeNames.Metadata}.{AttributeNames.LastModified}",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        if (andFilter.FilterOperator == ComparisonOperator.EqualOrGreaterThan)
                        {
                            DateTime comparisonValue = DateTime
                                .Parse(andFilter.ComparisonValue)
                                .ToUniversalTime();
                            predicateAnd = predicateAnd.And(
                                p => p.Metadata.LastModified >= comparisonValue
                            );
                        }
                        else if (andFilter.FilterOperator == ComparisonOperator.EqualOrLessThan)
                        {
                            DateTime comparisonValue = DateTime
                                .Parse(andFilter.ComparisonValue)
                                .ToUniversalTime();
                            predicateAnd = predicateAnd.And(
                                p => p.Metadata.LastModified <= comparisonValue
                            );
                        }
                        else
                            throw new NotSupportedException(
                                string.Format(
                                    SystemForCrossDomainIdentityManagementServiceResources.ExceptionFilterOperatorNotSupportedTemplate,
                                    andFilter.FilterOperator
                                )
                            );
                    }
                    else
                        throw new NotSupportedException(
                            string.Format(
                                SystemForCrossDomainIdentityManagementServiceResources.ExceptionFilterAttributePathNotSupportedTemplate,
                                andFilter.AttributePath
                            )
                        );

                    currentFilter = andFilter;
                    andFilter = andFilter.AdditionalFilter;
                } while (currentFilter.AdditionalFilter != null);

                predicate = predicate.Or(predicateAnd);
            }

            // results = this.storage.Users.Values.Where(predicate.Compile());
            results = new List<Resource>();
        }

        if (parameters.PaginationParameters != null)
        {
            int count = parameters.PaginationParameters.Count.HasValue
                ? parameters.PaginationParameters.Count.Value
                : 0;
            return Task.FromResult(results.Take(count).ToArray());
        }
        else
            return Task.FromResult(results.ToArray());
    }

    [Obsolete("This method is obsolete. Use ReplaceAsync(Resource, string, string) instead.", true)]
    public override async Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Update whole user asynchronously.
    /// </summary>
    /// <param name="resource">User resource to be updated.</param>
    /// <param name="correlationIdentifier">Correlation identifier for tracking the operation.</param>
    /// <param name="appId">Application ID associated with the user.</param>
    /// <returns>Updated user resource.</returns>
    /// <exception cref="HttpResponseException"></exception>
    public override async Task<Resource> ReplaceAsync(Resource resource, string correlationIdentifier, string appId = null)
    {
        if (resource.Identifier == null)
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        Core2EnterpriseUser user = resource as Core2EnterpriseUser;

        if (string.IsNullOrWhiteSpace(user.UserName))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        // Update metadata
        user.Metadata.LastModified = DateTime.UtcNow;

        await _replaceUser.ReplaceAsync(user, appId, correlationIdentifier);

        return await Task.FromResult(resource);
    }

    /// <summary>
    /// Creates a new user asynchronously.
    /// </summary>
    /// <param name="resource"></param>
    /// <param name="correlationIdentifier"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    [Obsolete("This method is obsolete. Use CreateAsync(Resource, string, string) instead.", true)]
    public sealed override Task<Resource> CreateAsync(Resource resource, string correlationIdentifier)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Deletes a user asynchronously.
    /// </summary>
    /// <param name="resourceIdentifier">Identifier of the user to be deleted.</param>
    /// <param name="correlationIdentifier">Correlation identifier for tracking the operation.</param>
    /// <param name="appId">Application ID associated with the user.</param>
    /// <exception cref="HttpResponseException"Exception thrown if the resource identifier is null or empty.</exception>
    public async override Task DeleteAsync(IResourceIdentifier resourceIdentifier, string correlationIdentifier, string appId = null)
    {
        if (string.IsNullOrWhiteSpace(resourceIdentifier?.Identifier))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        await _deleteUser.DeleteAsync(resourceIdentifier, appId, correlationIdentifier);
    }

    /// <summary>
    /// Performs a partial update on a user asynchronously.
    /// </summary>
    /// <param name="patch">Patch request containing the patch operations to be performed on the user.</param>
    /// <param name="correlationIdentifier">Correlation identifier for tracking the operation.</param>
    /// <param name="appId">Application ID associated with the user.</param>
    public async override Task UpdateAsync(IPatch patch, string correlationIdentifier, string appId = null)
    {
        if (patch == null)
        {
            throw new ArgumentNullException(nameof(patch));
        }

        if (string.IsNullOrWhiteSpace(correlationIdentifier))
        {
            throw new ArgumentNullException(nameof(correlationIdentifier));
        }

        if (string.IsNullOrWhiteSpace(patch.ResourceIdentifier?.Identifier))
        {
            throw new ArgumentNullException(nameof(patch));
        }

        await _updateUser.UpdateAsync(patch, appId, correlationIdentifier);
    }

    public async override Task<Resource> RetrieveAsync(IResourceRetrievalParameters parameters, string correlationIdentifier, string appId = null)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (string.IsNullOrWhiteSpace(correlationIdentifier))
        {
            throw new ArgumentNullException(nameof(correlationIdentifier));
        }

        if (string.IsNullOrEmpty(parameters?.ResourceIdentifier?.Identifier))
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        Resource result = null;
        string identifier = parameters.ResourceIdentifier.Identifier;

        result = await _getUser.GetAsync(identifier, appId, correlationIdentifier) as Resource;
        return result;
    }
}
