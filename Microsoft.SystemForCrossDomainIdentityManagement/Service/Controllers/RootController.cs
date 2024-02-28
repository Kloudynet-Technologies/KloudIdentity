// Copyright (c) Microsoft Corporation.// Licensed under the MIT license.

namespace Microsoft.SCIM
{
    using KN.KI.LogAggregator.Library.Abstractions;
    using System;

    public sealed class RootController : ControllerTemplate<Resource>
    {
        public RootController(IProvider provider, IMonitor monitor, IKloudIdentityLogger logger)
            : base(provider, monitor, logger)
        {
        }

        protected override IProviderAdapter<Resource> AdaptProvider(IProvider provider)
        {
            if (null == provider)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            IProviderAdapter<Resource> result = new RootProviderAdapter(provider);
            return result;
        }
    }
}
