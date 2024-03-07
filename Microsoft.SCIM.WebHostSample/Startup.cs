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
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.SCIM.WebHostSample.Provider;
    using Newtonsoft.Json;
    using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;
    using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Queries;
    using System;
    using KN.KloudIdentity.Mapper.Infrastructure.Messaging;
    using KN.KI.LogAggregator.Library.Abstractions;

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
                if (this.environment.IsDevelopment())
                {
                    options.TokenValidationParameters =
                       new TokenValidationParameters
                       {
                           ValidateIssuer = false,
                           ValidateAudience = false,
                           ValidateLifetime = false,
                           ValidateIssuerSigningKey = false,
                           ValidIssuer = this.configuration["Token:TokenIssuer"],
                           ValidAudience = this.configuration["Token:TokenAudience"],
                           IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.configuration["Token:IssuerSigningKey"]))
                       };
                }
                else
                {
                    options.Authority = this.configuration["Token:TokenIssuer"];
                    options.Audience = this.configuration["Token:TokenAudience"];
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

            services.ConfigureMapperServices();

            services.AddHttpClient();

            services.AddScoped<RabbitMQPublisher>(pub =>
            {
                return new RabbitMQPublisher(
                    this.configuration["RabbitMQ:Username"],
                    this.configuration["RabbitMQ:Password"],
                    this.configuration["RabbitMQ:Host"],
                    this.configuration["RabbitMQ:ExchangeName"],
                    this.configuration["RabbitMQ:QueueName_Out"],
                    this.configuration["RabbitMQ:QueueName_In"]
                );
            });

            services.AddTransient<IKloudIdentityLogger>(pub => new KN.KI.LogAggregator.Library.Implementations.RabbitMQPublisher(
                     this.configuration["RabbitMQ:Host"],
                     this.configuration["RabbitMQ:UserName"],
                     this.configuration["RabbitMQ:Password"]));

            services.AddScoped<IGetFullAppConfigQuery, GetFullAppConfigQuery>();

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
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var services = scope.ServiceProvider;

                var context = services.GetRequiredService<Context>();
                context.Database.Migrate();
            }
            app.UseCors(builder =>
            {
                builder.AllowAnyOrigin()
                     .AllowAnyMethod()
                     .AllowAnyHeader();
            });
            app.UseHsts();
            app.UseRouting();
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseCors("AllowAllOrigins");

            app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

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
