//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.SCIM.WebHostSample
{
    using System.Text;
    using System.Threading.Tasks;
    using KN.KloudIdentity.Mapper.Utils;
    using KN.KloudIdentity.Mapper.Common.Exceptions;
    using KN.KloudIdentity.Mapper.Config.Db;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.SCIM.WebHostSample.Provider;
    using Newtonsoft.Json;
    using KN.KloudIdentity.Mapper.Infrastructure.Messaging;
    using KN.KI.LogAggregator.Library.Abstractions;
    using KN.KI.LogAggregator.Library;
    using Hangfire;
    using KN.KloudIdentity.Mapper.Domain;
    using Microsoft.Extensions.Options;
    using KN.KI.LogAggregator.Library.Implementations;
    using KN.KloudIdentity.Mapper.BackgroundJobs;
    using System.Threading;
    using System;
    using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Abstractions;
    using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPICalls.Queries;

    public class Startup
    {
        private readonly IWebHostEnvironment environment;
        private readonly IConfiguration configuration;

        public IMonitor MonitoringBehavior { get; set; }
        public IProvider ProviderBehavior { get; set; }

        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            this.environment = env;
            this.configuration = configuration;

            this.MonitoringBehavior = new ConsoleMonitor();
            this.ProviderBehavior = new InMemoryProvider();
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<Context>();

            void ConfigureMvcNewtonsoftJsonOptions(MvcNewtonsoftJsonOptions options) => options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;

            void ConfigureAuthenticationOptions(AuthenticationOptions options)
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }

            void ConfigureJwtBearerOptons(JwtBearerOptions options)
            {
                var section = this.configuration.GetSection("KI");

                if (this.environment.IsDevelopment())
                {
                    options.TokenValidationParameters =
                       new TokenValidationParameters
                       {
                           ValidateIssuer = false,
                           ValidateAudience = false,
                           ValidateLifetime = false,
                           ValidateIssuerSigningKey = false,
                           ValidIssuer = section["Token:TokenIssuer"],
                           ValidAudience = section["Token:TokenAudience"],
                           IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section["Token:IssuerSigningKey"]))
                       };
                }
                else
                {
                    options.Authority = section["Token:TokenIssuer"];
                    options.Audience = section["Token:TokenAudience"];
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            return Task.CompletedTask;
                        },
                        OnAuthenticationFailed = AuthenticationFailed
                    };
                }

            }

            services.AddOptions<AppSettings>().Bind(configuration.GetSection("KI"));

            services.AddAuthentication(ConfigureAuthenticationOptions).AddJwtBearer(ConfigureJwtBearerOptons);
            services.AddControllers().AddNewtonsoftJson(ConfigureMvcNewtonsoftJsonOptions);

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    builder =>
                    {
                        builder.AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    });
            });

            // services.AddSingleton(typeof(IProvider), this.ProviderBehavior);
            services.AddSingleton(typeof(IMonitor), this.MonitoringBehavior);

            services.ConfigureMapperServices(configuration);

            services.AddHttpClient();

            services.AddTransient<MessageBroker>(cfg =>
            {
                var options = cfg.GetRequiredService<IOptions<AppSettings>>().Value;

                return new MessageBroker(cfg.GetRequiredService<RabbitMQUtil>(),
                                        options.RabbitMQ.ExchangeName);
            });

            services.AddSingleton<IKloudIdentityLogger>(pub =>
            {
                var options = pub.GetRequiredService<IOptions<AppSettings>>().Value;

                return new RabbitMQPublisher(
                options.RabbitMQ.Hostname,
                options.RabbitMQ.UserName,
                options.RabbitMQ.Password,
                LogSeverities.Information);
            });

            services.AddHangfire(x => x.UseSqlServerStorage(configuration["ConnectionStrings:HangfireDBConnection"]));

            services.AddHangfireServer();

            services.AddScoped<NonSCIMGroupProvider>();
            services.AddScoped<NonSCIMUserProvider>();
            services.AddScoped<IProvider, NonSCIMAppProvider>();
            services.AddScoped<ExtractAppIdFilter>();

            services.AddHostedService<RabbitMQListner>(con =>
            {
                var options = con.GetRequiredService<IOptions<AppSettings>>().Value;

                return new RabbitMQListner(configuration["RabbitMQ:QueueName_In"],
                                        configuration["RabbitMQ:QueueName_Out"],
                                        options.RabbitMQ.ExchangeName,
                                        con.GetRequiredService<RabbitMQUtil>(),
                                        con.GetService<IServiceScopeFactory>());
            });

            services.AddHostedService<JobCreationService>(con =>
            {
                var options = con.GetRequiredService<IOptions<AppSettings>>().Value;

                return new JobCreationService(
                                        con.GetService<IServiceProvider>());
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            if (this.environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // Migrate database
            // using (var scope = app.ApplicationServices.CreateScope())
            // {
            //     var services = scope.ServiceProvider;

            //     var context = services.GetRequiredService<Context>();
            //     context.Database.Migrate();
            // }

            app.UseCors("AllowAllOrigins");

            app.UseHsts();
            app.UseRouting();
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHangfireDashboard("/hangfire/jobs");
            app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
          
         //   RecurringJob.AddOrUpdate<IBackgroundJobService>("jobId", x => x.RunSheduleJobAsybc(), Cron.Weekly);

            app.UseEndpoints(
                (IEndpointRouteBuilder endpoints) =>
                {
                    endpoints.MapDefaultControllerRoute();
                });
        }

        private Task AuthenticationFailed(AuthenticationFailedContext arg)
        {
            // For debugging purposes only!
            string authenticationExceptionMessage = $"{{AuthenticationFailed: '{arg.Exception.Message}'}}";

            arg.Response.ContentLength = authenticationExceptionMessage.Length;
            arg.Response.Body.WriteAsync(
                Encoding.UTF8.GetBytes(authenticationExceptionMessage),
                0,
                authenticationExceptionMessage.Length);

            return Task.FromException(arg.Exception);
        }
    }
}
