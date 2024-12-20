using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyncOne.Models;
using SmsMessage = SyncOne.Models.SmsMessage;

public class DatabaseService
{
    private SQLiteAsyncConnection _database;
    private readonly string _databasePath;

    public DatabaseService()
    {
        _databasePath = Path.Combine(FileSystem.AppDataDirectory, "SyncOne.db");
    }

    public async Task InitializeAsync()
    {
        if (_database != null) return;

        _database = new SQLiteAsyncConnection(_databasePath);
        await _database.CreateTableAsync<SmsMessage>();
    }

    public async Task<List<SmsMessage>> GetMessagesAsync()
    {
        await InitializeAsync(); // Ensure database is initialized
        return await _database.Table<SmsMessage>().ToListAsync();
    }

    public async Task SaveMessageAsync(SmsMessage message)
    {
        await InitializeAsync(); // Ensure database is initialized
        if (message.Id == 0)
            await _database.InsertAsync(message);
        else
            await _database.UpdateAsync(message);
    }
}
