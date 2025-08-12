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
                        //   Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
                        options.Connect(settings["ConnectionStrings:AppConfig"])
                            .Select("KI:*",
                                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
                            .ConfigureKeyVault(kv => { kv.SetCredential(new DefaultAzureCredential()); })
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