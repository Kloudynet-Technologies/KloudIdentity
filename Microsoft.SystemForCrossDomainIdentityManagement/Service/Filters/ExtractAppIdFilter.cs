//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using System;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Microsoft.SCIM;

/// <summary>
/// Extracts the application identifier from the route data and adds it to the request context.
/// </summary>
public class ExtractAppIdFilter : IActionFilter
{
    /// <summary>
    /// Extracts the application identifier from the route data and adds it to the request context.
    /// </summary>
    /// <param name="context"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.RouteData.Values["appId"] is string appId)
        {
            context.HttpContext.Items.Add("appId", appId);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
