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
                        var tenantId = settings["AZURE_TENANT_ID"];
                        var clientId = settings["AZURE_CLIENT_ID"];
                        var clientSecret = settings["AZURE_CLIENT_SECRET"];
                        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
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