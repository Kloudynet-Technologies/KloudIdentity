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
    using KN.KI.LogAggregator.Library.Abstractions;
    using KN.KI.LogAggregator.Library;
    using KN.KloudIdentity.Mapper.Domain;
    using Microsoft.Extensions.Options;
    using KN.KI.LogAggregator.Library.Implementations;
    using System;
    using MassTransit;
    using KN.KI.RabbitMQ.MessageContracts;
    using KN.KloudIdentity.Mapper.Masstransit;
    using Hangfire;

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

                    options.TokenValidationParameters =
                       new TokenValidationParameters
                       {
                           ValidateIssuer = true,
                           ValidateAudience = true,
                           ValidateLifetime = false,
                           ValidateIssuerSigningKey = true,
                           ValidIssuer = section["Token:TokenIssuer"],
                           ValidAudience = section["Token:TokenAudience"],
                           IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section["Token:IssuerSigningKey"]))
                       };

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

            services.AddApplicationInsightsTelemetry();

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

            services.AddMassTransit(x =>
            {
                x.SetKebabCaseEndpointNameFormatter();
                x.AddRequestClient<IMgtPortalServiceRequestMsg>(new Uri("queue:mgtportal_in"));
                x.AddConsumer<InterserviceConsumer>();
                x.UsingRabbitMq((context, cfg) =>
                {
                    var options = context.GetRequiredService<IOptions<AppSettings>>().Value;

                    cfg.Host(options.RabbitMQ.Hostname, "/", h =>
                    {
                        h.Username(options.RabbitMQ.UserName);
                        h.Password(options.RabbitMQ.Password);
                    });
                    cfg.ReceiveEndpoint("scimservice_in", e =>
                    {
                        e.ConfigureConsumer<InterserviceConsumer>(context);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            });

            services.AddSingleton<IKloudIdentityLogger>(pub =>
            {
                var scope = pub.CreateScope();
                var endpointProvider = scope.ServiceProvider.GetService<ISendEndpointProvider>();

                return new KloudIdentityLogger(
                endpointProvider!,
                LogSeverities.Information);
            });

            services.AddHangfire(x => x.UseSqlServerStorage(configuration["ConnectionStrings:HangfireDBConnection"]));

            services.AddHangfireServer();
            services.AddHealthChecks();

            services.AddScoped<NonSCIMGroupProvider>();
            services.AddScoped<NonSCIMUserProvider>();
            services.AddScoped<IProvider, NonSCIMAppProvider>();
            services.AddScoped<ExtractAppIdFilter>();
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
          
            app.UseEndpoints(
                (IEndpointRouteBuilder endpoints) =>
                {
                    endpoints.MapDefaultControllerRoute();
                    endpoints.MapHealthChecks("/api/healthz");
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
