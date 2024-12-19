using SQLite;
using SyncOne.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncOne.Services
{
    public class ConfigurationService
    {
        private readonly SQLiteAsyncConnection _database;

        public ConfigurationService(SQLiteAsyncConnection database)
        {
            _database = database;
        }

        public async Task InitializeAsync()
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
            }
        }






        public async Task<AppConfig> GetConfigAsync()
        {
            return await _database.Table<AppConfig>().FirstOrDefaultAsync();
        }

        public async Task SaveConfigAsync(AppConfig config)
        {
            if (config.Id == 0)
                await _database.InsertAsync(config);
            else
                await _database.UpdateAsync(config);
        }

        public async Task<List<PhoneNumberFilter>> GetPhoneNumberFiltersAsync(bool isAllowed)
        {
            return await _database.Table<PhoneNumberFilter>()
                .Where(x => x.IsAllowed == isAllowed)
                .ToListAsync();
        }

        public async Task AddPhoneNumberFilterAsync(string phoneNumber, bool isAllowed)
        {
            await _database.InsertAsync(new PhoneNumberFilter
            {
                PhoneNumber = phoneNumber,
                IsAllowed = isAllowed
            });
        }

        public async Task RemovePhoneNumberFilterAsync(int id)
        {
            await _database.DeleteAsync<PhoneNumberFilter>(id);
        }

        public async Task<bool> IsPhoneNumberAllowedAsync(string phoneNumber)
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

        public async Task AddLogEntryAsync(string type, string message)
        {
            await _database.InsertAsync(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Message = message
            });
        }

        public async Task<List<LogEntry>> GetLogsAsync()
        {
            return await _database.Table<LogEntry>()
                .OrderByDescending(x => x.Timestamp)
                .ToListAsync();
        }
    }

}
