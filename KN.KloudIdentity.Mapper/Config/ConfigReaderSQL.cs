using KN.KloudIdentity.Mapper.Common.Encryption;
using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Config.Db;
using KN.KloudIdentity.Mapper.Config.Helper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace KN.KloudIdentity.Mapper.Config
{
    public class ConfigReaderSQL : IConfigReader
    {
        private readonly Context _context;
        private readonly ILogger<ConfigReaderSQL> _logger;
        private readonly IConfiguration _configuration;
        public ConfigReaderSQL(Context context, ILogger<ConfigReaderSQL> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task CreateConfigAsync(MapperConfig config, CancellationToken cancellationToken)
        {
            if (await ConfigExists(config.AppId, cancellationToken))
            {
                throw new ApplicationException($"Config already exists for appId: {config.AppId}");
            }
            try
            {
                ProcessAuthConfig(config.AuthConfig, true);

                using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
                {
                    var appConfigModel = ConfigReaderHelper.FormatAppConfigModel(config);
                    await _context.AppConfig.AddAsync(appConfigModel, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await _context.Database.RollbackTransactionAsync(cancellationToken);

                // Log the exception for debugging
                _logger.LogError(ex, $"Error creating config for appId: {config.AppId}");

                throw new ApplicationException($"Error creating config for appId: {config.AppId}", ex);
            }
        }

        public async Task<MapperConfig> GetConfigAsync(string appId, CancellationToken cancellationToken)
        {
            var res = await _context.AppConfig.Where(x => x.AppId == appId)
                       .Include(auth => auth.AuthConfig)
                       .Include(u => u.UserSchema)
                       .Include(g => g.GroupSchema)
                       .FirstOrDefaultAsync(cancellationToken);

            if (res == null)
            {
                throw new NotFoundException($"No config found for appId: {appId}");
            }

            var mapperConfig = ConfigReaderHelper.FormatMapperConfig(res);

            ProcessAuthConfig(mapperConfig.AuthConfig, false);

            return mapperConfig;

        }

        public async Task UpdateConfigAsync(MapperConfig config, CancellationToken cancellationToken)
        {
            var configModel = await _context.AppConfig.Where(x => x.AppId == config.AppId)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (configModel == null)
            {
                throw new NotFoundException($"No config found for appId: {config.AppId}");
            }

            try
            {
                ProcessAuthConfig(config.AuthConfig, true);

                using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
                {
                    await RemoveAuthConfig(config.AppId, cancellationToken);
                    await RemoveUserSchema(config.AppId, cancellationToken);
                    await RemoveGroupSchema(config.AppId, cancellationToken);

                    configModel.GroupProvisioningApiUrl = config.GroupProvisioningApiUrl;
                    configModel.UserProvisioningApiUrl = config.UserProvisioningApiUrl;

                    _context.AppConfig.Update(configModel);

                    _context.AuthConfig.Add(ConfigReaderHelper.FormatAuthConfigModel(configModel.AppId, config.AuthConfig));
                    _context.GroupSchema.AddRange(ConfigReaderHelper.FormatListGroupSchemaModel(configModel.AppId, config.GroupSchema));
                    _context.UserSchema.AddRange(ConfigReaderHelper.FormatListUserSchemaModel(configModel.AppId, config.UserSchema));

                    await _context.SaveChangesAsync(cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await _context.Database.RollbackTransactionAsync(cancellationToken);

                // Log the exception for debugging
                _logger.LogError(ex, $"Error updating config for appId: {config.AppId}");

                throw new ApplicationException($"Error updating config for appId: {config.AppId}", ex);
            }
        }

        private async Task<bool> ConfigExists(string appId, CancellationToken cancellationToken)
        {
            return await _context.AppConfig.AnyAsync(e => e.AppId == appId, cancellationToken);
        }

        private async Task RemoveAuthConfig(string appId, CancellationToken cancellationToken)
        {
            var authConfig = await _context.AuthConfig.Where(x => x.AppId == appId)
                .FirstOrDefaultAsync(cancellationToken);

            if (authConfig != null)
            {
                _context.AuthConfig.Remove(authConfig);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task RemoveUserSchema(string appId, CancellationToken cancellationToken)
        {
            var userSchema = await _context.UserSchema.Where(x => x.AppId == appId)
                .ToListAsync(cancellationToken);

            if (userSchema != null)
            {
                _context.UserSchema.RemoveRange(userSchema);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task RemoveGroupSchema(string appId, CancellationToken cancellationToken)
        {
            var groupSchema = await _context.GroupSchema.Where(x => x.AppId == appId)
                .ToListAsync(cancellationToken);

            if (groupSchema != null)
            {
                _context.GroupSchema.RemoveRange(groupSchema);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private void ProcessAuthConfig(AuthConfig authConfig, bool encrypt)
        {
            var encryptedKey = _configuration.GetSection("Encryption:Key").Value;
            var encryptedIV = _configuration.GetSection("Encryption:IV").Value;

            PropertyInfo[] properties = typeof(AuthConfig).GetProperties();

            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(string) && property.GetCustomAttribute<SensitiveFieldAttribute>() != null)
                {
                    var fieldValue = (string)property.GetValue(authConfig);

                    if (!string.IsNullOrEmpty(fieldValue))
                    {
                        if (encrypt)
                        {
                            fieldValue = EncryptionHelper.Encrypt(fieldValue, encryptedKey, encryptedIV);
                        }
                        else
                        {
                            fieldValue = EncryptionHelper.Decrypt(fieldValue, encryptedKey, encryptedIV);
                        }

                        property.SetValue(authConfig, fieldValue);
                    }
                }
            }
        }


    }
}
