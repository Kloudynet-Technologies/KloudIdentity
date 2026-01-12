//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using Azure.Identity;

namespace Microsoft.SCIM.WebHostSample
{
    using System;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;

    public class Program
    {
        public static void Main(string[] args)
        {
            Program.CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddUserSecrets<Program>();
                    var settings = config.Build();
                    config.AddAzureAppConfiguration(options =>
                    {
                        var appConfigLabel = settings["AppConfigLabel"];
                        var tenantId = settings["TENANT_ID"];
                        var saClientId = settings["SA_CLIENT_ID"];
                        var saClientSecret = settings["SA_CLIENT_SECRET"];
                        var credential = new ClientSecretCredential(tenantId, saClientId, saClientSecret);
                        options.Connect(settings["ConnectionStrings:AppConfig"])
                            .Select("KI:*", appConfigLabel)
                            .ConfigureKeyVault(kv => { kv.SetCredential(credential); })
                            .ConfigureRefresh(refresh =>
                            {
                                refresh.Register("KI:RefreshOption", refreshAll: true)
                                    .SetCacheExpiration(TimeSpan.FromSeconds(5));
                            })
                            .UseFeatureFlags();
                    });
                })
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
    }
}
