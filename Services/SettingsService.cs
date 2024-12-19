using Microsoft.Extensions.Logging;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyncOne.Models;

namespace SyncOne.Services
{
    public class SettingsService
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly ILogger<SettingsService> _logger;

        public SettingsService(SQLiteAsyncConnection database, ILogger<SettingsService> logger)
        {
            _database = database;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _database.CreateTableAsync<AppSettings>();
            await _database.CreateTableAsync<SyncEndpoint>();
            await _database.CreateTableAsync<KeywordFilter>();
            await _database.CreateTableAsync<ScheduledTask>();
            await _database.CreateTableAsync<LogEntry>();
            await _database.CreateTableAsync<PendingMessage>();
            await _database.CreateTableAsync<PhoneNumberFilter>();

            // Create default settings if none exist
            var settings = await _database.Table<AppSettings>().FirstOrDefaultAsync();
            if (settings == null)
            {
                await _database.InsertAsync(new AppSettings
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    AutoSyncFrequency = 15, // 15 minutes default
                    PendingMessageRetries = 3,
                    DefaultLanguage = "en",
                    EnableAutoSync = false,
                    EnableTaskChecking = false,
                    EnableMessageResults = false
                });
            }
        }

        public async Task AddLogEntryAsync(string type, string component, string message, string details = null)
        {
            await _database.InsertAsync(new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Component = component,
                Message = message,
                Details = details
            });

            _logger.LogInformation($"[{component}] {message}");
        }

        public async Task<List<LogEntry>> GetLogsAsync(DateTime? since = null, string type = null)
        {
            var query = _database.Table<LogEntry>().OrderByDescending(x => x.Timestamp);

            if (since.HasValue)
                query = query.Where(x => x.Timestamp >= since.Value);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(x => x.Type == type);

            return await query.ToListAsync();
        }

        // Add methods for managing settings, endpoints, filters, etc.
        // Implementation details will depend on your specific requirements
    }
}
