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
    private readonly SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _databaseLock = new SemaphoreSlim(1, 1); // For thread-safe database access

    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _databasePath = Path.Combine(FileSystem.AppDataDirectory, "SyncOne.db");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes the database if it hasn't been initialized yet.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized && _database != null) return;

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized && _database != null) return;

            // Ensure the database file exists
            if (!File.Exists(_databasePath))
            {
                _logger.LogInformation("Database file does not exist. Creating a new one.");
            }

            _database = new SQLiteAsyncConnection(_databasePath);
            await _database.CreateTableAsync<SmsMessage>();
            _isInitialized = true;
            _logger.LogInformation("Database initialized successfully.");
        }
        catch (Exception ex)
        {
            _isInitialized = false;
            _database = null;
            _logger.LogError(ex, "Failed to initialize database.");
            throw;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Ensures the database is initialized before performing operations.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (!_isInitialized || _database == null)
        {
            await InitializeAsync();
        }
    }

    /// <summary>
    /// Retrieves all messages from the database.
    /// </summary>
    public async Task<List<SmsMessage>> GetMessagesAsync()
    {
        try
        {
            await EnsureInitializedAsync();

            await _databaseLock.WaitAsync();
            try
            {
                var messages = await _database.Table<SmsMessage>().ToListAsync();
                _logger.LogInformation("Retrieved {Count} messages from the database.", messages.Count);
                return messages ?? new List<SmsMessage>();
            }
            finally
            {
                _databaseLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve messages from the database.");
            throw;
        }
    }

    /// <summary>
    /// Retrieves a message by its ID.
    /// </summary>
    public async Task<SmsMessage> GetMessageByIdAsync(long messageId)
    {
        try
        {
            await EnsureInitializedAsync();

            await _databaseLock.WaitAsync();
            try
            {
                var message = await _database.Table<SmsMessage>()
                                             .FirstOrDefaultAsync(m => m.Id == messageId);
                _logger.LogInformation("Retrieved message by ID: {MessageId}", messageId);
                return message;
            }
            finally
            {
                _databaseLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve message by ID: {MessageId}", messageId);
            throw;
        }
    }

    /// <summary>
    /// Saves a message to the database. If the message already exists, it updates it.
    /// </summary>
    public async Task SaveMessageAsync(SmsMessage message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        // Validate the message
        if (string.IsNullOrEmpty(message.From) || string.IsNullOrEmpty(message.Body))
        {
            _logger.LogWarning("Invalid message: From or Body is null or empty.");
            throw new ArgumentException("From and Body cannot be null or empty.");
        }

        if (message.ReceivedAt == default)
        {
            _logger.LogWarning("Invalid message: ReceivedAt is not set.");
            throw new ArgumentException("ReceivedAt cannot be default.");
        }

        try
        {
            await EnsureInitializedAsync();

            await _databaseLock.WaitAsync();
            try
            {
                // Insert or replace the message
                await _database.InsertOrReplaceAsync(message);

                _logger.LogInformation("Message saved successfully: Id={MessageId}, From={From}, Body={Body}",
                    message.Id, message.From, message.Body);
            }
            finally
            {
                _databaseLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save message to the database: Id={MessageId}, From={From}, Body={Body}",
                message.Id, message.From, message.Body);
            throw;
        }
    }

    /// <summary>
    /// Retrieves all unprocessed messages from the database.
    /// </summary>
    public async Task<IEnumerable<SmsMessage>> GetUnprocessedMessagesAsync()
    {
        try
        {
            await EnsureInitializedAsync();

            await _databaseLock.WaitAsync();
            try
            {
                var cutoffTime = DateTime.UtcNow.AddMinutes(-5); // Example: Skip messages older than 5 minutes
                var unprocessedMessages = await _database.Table<SmsMessage>()
                    .Where(message => !message.IsProcessed && message.ReceivedAt >= cutoffTime)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} unprocessed messages.", unprocessedMessages.Count);
                return unprocessedMessages;
            }
            finally
            {
                _databaseLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve unprocessed messages from the database.");
            throw;
        }
    }
}