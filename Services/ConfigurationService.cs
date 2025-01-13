using Microsoft.Extensions.Logging;
using SQLite;
using SyncOne.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SyncOne.Services
{
    public class ConfigurationService
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly ILogger<ConfigurationService> _logger;

        public ConfigurationService(SQLiteAsyncConnection database, ILogger<ConfigurationService> logger)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _database.CreateTableAsync<AppConfig>();
                await _database.CreateTableAsync<PhoneNumberFilter>();
                await _database.CreateTableAsync<KeywordFilter>();  // Add new tables
                await _database.CreateTableAsync<ScheduledTask>();
                await _database.CreateTableAsync<LogEntry>();

                var config = await GetConfigAsync();
                if (config == null)
                {
                    await SaveConfigAsync(new AppConfig
                    {
                        ApiUrl = "http://localhost:5000",
                        UseAllowlist = false,
                        UseBlocklist = false,
                        DeviceId = Guid.NewGuid().ToString(),
                        EnableAutoSync = false,
                        // Add other default settings
                    });

                    _logger.LogInformation("Default configuration saved.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize database tables.");
                throw;
            }
        }

        public async Task<AppConfig> GetConfigAsync()
        {
            try
            {
                return await _database.Table<AppConfig>().FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve application configuration.");
                throw;
            }
        }

        public async Task SaveConfigAsync(AppConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                if (config.Id == 0)
                    await _database.InsertAsync(config);
                else
                    await _database.UpdateAsync(config);

                _logger.LogInformation("Configuration saved successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save application configuration.");
                throw;
            }
        }

        public async Task<List<PhoneNumberFilter>> GetPhoneNumberFiltersAsync(bool isAllowed)
        {
            try
            {
                return await _database.Table<PhoneNumberFilter>()
                    .Where(x => x.IsAllowed == isAllowed)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve phone number filters.");
                throw;
            }
        }

        public async Task AddPhoneNumberFilterAsync(string phoneNumber, bool isAllowed)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("Phone number cannot be null or empty.", nameof(phoneNumber));

            try
            {
                await _database.InsertAsync(new PhoneNumberFilter
                {
                    PhoneNumber = phoneNumber,
                    IsAllowed = isAllowed
                });

                _logger.LogInformation("Phone number filter added: {PhoneNumber}, IsAllowed: {IsAllowed}", phoneNumber, isAllowed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add phone number filter.");
                throw;
            }
        }

        public async Task RemovePhoneNumberFilterAsync(int id)
        {
            try
            {
                await _database.DeleteAsync<PhoneNumberFilter>(id);
                _logger.LogInformation("Phone number filter removed: {Id}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove phone number filter.");
                throw;
            }
        }

        public async Task<bool> IsPhoneNumberAllowedAsync(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new ArgumentException("Phone number cannot be null or empty.", nameof(phoneNumber));

            try
            {
                var config = await GetConfigAsync();

                if (config.UseAllowlist)
                {
                    // Check if number is in allowlist
                    return await _database.Table<PhoneNumberFilter>()
                        .Where(x => x.PhoneNumber == phoneNumber && x.IsAllowed)
                        .CountAsync() > 0;
                }

                if (config.UseBlocklist)
                {
                    // Check if number is NOT in blocklist
                    return await _database.Table<PhoneNumberFilter>()
                        .Where(x => x.PhoneNumber == phoneNumber && !x.IsAllowed)
                        .CountAsync() == 0;
                }

                // If no filtering is enabled, allow all numbers
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if phone number is allowed.");
                throw;
            }
        }

        public async Task AddLogEntryAsync(string type, string message)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Log type cannot be null or empty.", nameof(type));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Log message cannot be null or empty.", nameof(message));

            try
            {
                await _database.InsertAsync(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Type = type,
                    Message = message
                });

                _logger.LogInformation("Log entry added: {Type}, {Message}", type, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add log entry.");
                throw;
            }
        }

        public async Task<List<LogEntry>> GetLogsAsync()
        {
            try
            {
                return await _database.Table<LogEntry>()
                    .OrderByDescending(x => x.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve logs.");
                throw;
            }
        }
    }
}