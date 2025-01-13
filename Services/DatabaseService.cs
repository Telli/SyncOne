using Microsoft.Extensions.Logging;
using SQLite;
using SyncOne.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmsMessage = SyncOne.Models.SmsMessage;

public class DatabaseService
{
    private SQLiteAsyncConnection _database;
    private readonly string _databasePath;
    private readonly ILogger<DatabaseService> _logger;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1); // Thread-safe initialization

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _databasePath = Path.Combine(FileSystem.AppDataDirectory, "SyncOne.db");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized) return; // Double-check after acquiring the lock

            _database = new SQLiteAsyncConnection(_databasePath);
            await _database.CreateTableAsync<SmsMessage>();
            _isInitialized = true;

            _logger.LogInformation("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database.");
            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<List<SmsMessage>> GetMessagesAsync()
    {
        try
        {
            return await _database.Table<SmsMessage>().ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve messages from the database.");
            throw;
        }
    }

    public async Task SaveMessageAsync(SmsMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        try
        {
            if (message.Id == 0)
                await _database.InsertAsync(message);
            else
                await _database.UpdateAsync(message);

            _logger.LogInformation("Message saved successfully: {MessageId}", message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save message to the database.");
            throw;
        }
    }

    public async Task<IEnumerable<SmsMessage>> GetUnprocessedMessagesAsync()
    {
        try
        {
            // Assuming SmsMessage has a boolean property 'IsProcessed'
            var unprocessedMessages = await _database.Table<SmsMessage>()
                .Where(message => !message.IsProcessed)
                .ToListAsync();

            _logger.LogInformation("Retrieved {Count} unprocessed messages.", unprocessedMessages.Count);
            return unprocessedMessages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve unprocessed messages from the database.");
            throw;
        }
    }
}