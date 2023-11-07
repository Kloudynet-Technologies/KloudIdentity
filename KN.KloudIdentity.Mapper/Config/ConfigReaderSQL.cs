//------------------------------------------------------------
// Copyright (c) Kloudynet Technologies Sdn Bhd.  All rights reserved.
//------------------------------------------------------------

using KN.KloudIdentity.Mapper.Common.Exceptions;
using KN.KloudIdentity.Mapper.Config.Db;
using KN.KloudIdentity.Mapper.Config.Helper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KN.KloudIdentity.Mapper.Config
{
    /// <summary>
    /// This class implements the IConfigReader interface for SQL Server.
    /// All the configs for thrid party apps are stored in the database.
    /// The database is created using the EF Core Code First approach.
    /// </summary>
    public class ConfigReaderSQL : IConfigReader
    {
        private readonly Context _context;

        private readonly ILogger<ConfigReaderSQL> _logger;

        public ConfigReaderSQL(Context context, ILogger<ConfigReaderSQL> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// This method creates a new config in the database.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="ApplicationException"></exception>
        public async Task CreateConfigAsync(MapperConfig config, CancellationToken cancellationToken)
        {
            if (await IsConfigExists(config.AppId, cancellationToken))
            {
                throw new ApplicationException($"Config already exists for appId: {config.AppId}");
            }

            try
            {
                using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
                {
                    var appConfigModel = config.TransformToAppConfigModel();
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

        /// <summary>
        /// This method reads the config from the database.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
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

            return res.TransformToMapperConfig();
        }

        /// <summary>
        /// This method updates the config in the database.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="NotFoundException"></exception>
        /// <exception cref="ApplicationException"></exception>
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

                    _context.AuthConfig.Add(config.AuthConfig.TransformToAuthConfigModel(configModel.AppId));
                    _context.GroupSchema.AddRange(config.GroupSchema.TransformToGroupSchemaModel(configModel.AppId));
                    _context.UserSchema.AddRange(config.UserSchema.TransformToUserSchemaModel(configModel.AppId));

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

        /// <summary>
        /// This method returns true if the config exists in the database.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<bool> IsConfigExists(string appId, CancellationToken cancellationToken)
        {
            return await _context.AppConfig.AnyAsync(e => e.AppId == appId, cancellationToken);
        }

        /// <summary>
        /// This method removes the auth config from the database.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// This method removes the user schema from the database.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// This method removes the group schema from the database.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
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
