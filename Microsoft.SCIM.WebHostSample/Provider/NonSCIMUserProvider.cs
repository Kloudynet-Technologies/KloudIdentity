//------------------------------------------------------------
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
    private readonly ICreateResource<Core2EnterpriseUser> _createUser;
    private readonly IDeleteResource<Core2EnterpriseUser> _deleteUser;
    private readonly IReplaceResource<Core2EnterpriseUser> _replaceUser;
    private readonly IUpdateResource<Core2EnterpriseUser> _updateUser;

    public NonSCIMUserProvider(
        ICreateResource<Core2EnterpriseUser> createUser,
        IDeleteResource<Core2EnterpriseUser> deleteUser,
        IReplaceResource<Core2EnterpriseUser> replaceUser,
        IUpdateResource<Core2EnterpriseUser> updateUser
    )
    {
        _createUser = createUser;
        _deleteUser = deleteUser;
        _replaceUser = replaceUser;
        _updateUser = updateUser;
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
    /// <exception cref="HttpResponseException">Thrown when the resource identifier is null or empty, resulting in a Bad Request status code.</exception>
    public override async Task DeleteAsync(
        IResourceIdentifier resourceIdentifier,
        string correlationIdentifier
    )
    {
        if (string.IsNullOrWhiteSpace(resourceIdentifier?.Identifier))
        {
            throw new HttpResponseException(HttpStatusCode.BadRequest);
        }

        await _deleteUser.DeleteAsync(resourceIdentifier, "App-001", correlationIdentifier);
    }

    // @TODO: Implement retrieve
    public override Task<Resource> RetrieveAsync(
        IResourceRetrievalParameters parameters,
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

        if (string.IsNullOrEmpty(parameters?.ResourceIdentifier?.Identifier))
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        Resource result = null;
        string identifier = parameters.ResourceIdentifier.Identifier;

        // if (this.storage.Users.ContainsKey(identifier))
        // {
        //     if (this.storage.Users.TryGetValue(identifier, out Core2EnterpriseUser user))
        //     {
        //         result = user as Resource;
        //         return Task.FromResult(result);
        //     }
        // }

        throw new HttpResponseException(HttpStatusCode.NotFound);
    }

    public override async Task UpdateAsync(IPatch patch, string correlationIdentifier)
    {
        PatchRequest2 patchRequest =
               patch.PatchRequest as PatchRequest2;

        Core2EnterpriseUser user = new Core2EnterpriseUser();
        user.Apply(patchRequest);

        await _updateUser.UpdateAsync(user, "App-002", correlationIdentifier);
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

    public override async Task<Resource> ReplaceAsync(
        Resource resource,
        string correlationIdentifier
    )
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

        await _replaceUser.ReplaceAsync(user, "App-002", correlationIdentifier);

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
}
