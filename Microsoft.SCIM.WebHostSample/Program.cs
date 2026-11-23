//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Domain.Shared;
using KN.KloudIdentity.Mapper.Infrastructure.Persistence.SQLServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
            var host = Program.CreateHostBuilder(args).Build();

            if (Array.Exists(args, a => a == "migrate"))
            {
                using var scope = host.Services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<KNContext>();
                context.Database.Migrate();
                Console.WriteLine("Database migrated successfully.");
                return;
            }

            host.Run();
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
                        var credential = AzureCredentialHelper.CreateClientSecretCredential(settings);
                        options.Connect(settings["ConnectionStrings:AppConfig"])
                            .Select("KI:*", appConfigLabel)
                            .ConfigureKeyVault(kv => { kv.SetCredential(credential); })
                            .ConfigureRefresh(refresh =>
                            {
                                // #421: under the label the keys are selected with. Register(key, refreshAll) alone
                                // watches the unlabelled key, which the store does not have, so a bump of the labelled
                                // sentinel never reloaded. 5 minutes like the other services: with the request middleware
                                // now triggering it, 5 seconds would contact the store up to every 5 seconds.
                                refresh.Register("KI:RefreshOption", appConfigLabel, refreshAll: true)
                                    .SetCacheExpiration(TimeSpan.FromMinutes(5));
                            })
                            .UseFeatureFlags();
                    });
                })
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
    }
}
