using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SyncOne.Models;
using SmsMessage = SyncOne.Models.SmsMessage;

namespace SyncOne.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;
        public DatabaseService(SQLiteAsyncConnection database)
        {
            _database = database;
        }
        public async Task InitializeAsync()
        {
            if (_database != null) return;

            var databasePath = Path.Combine(FileSystem.AppDataDirectory, "SyncOne.db");
            _database = new SQLiteAsyncConnection(databasePath);
            await _database.CreateTableAsync<SmsMessage>();
        }

        public async Task<List<SmsMessage>> GetMessagesAsync()
        {
            return await _database.Table<SmsMessage>().ToListAsync();
        }

        public async Task SaveMessageAsync(SmsMessage message)
        {
            if (message.Id == 0)
                await _database.InsertAsync(message);
            else
                await _database.UpdateAsync(message);
        }
    }
}
