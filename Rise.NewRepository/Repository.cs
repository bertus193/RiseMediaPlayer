using Rise.Common.Extensions;
using Rise.Models;
using SQLite;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;

namespace Rise.NewRepository
{
    public static class Repository
    {
        private static string _dbPath;
        public static string DbPath
        {
            get
            {
                if (_dbPath == null)
                {
                    try
                    {
                        // Packaged: usar ApplicationData.Current
                        _dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Lists.db");
                    }
                    catch (InvalidOperationException)
                    {
                        // Unpackaged: fallback a %LOCALAPPDATA%\Rise Media Player
                        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        var appFolder = Path.Combine(localAppData, "Rise Media Player");
                        Directory.CreateDirectory(appFolder);
                        _dbPath = Path.Combine(appFolder, "Lists.db");
                    }
                }
                return _dbPath;
            }
        }

        private static SQLiteConnection _db;
        private static SQLiteAsyncConnection _asyncDb;

        private static ConcurrentQueue<DbObject> _upsertQueue;
        private static ConcurrentQueue<DbObject> _removeQueue;

        private static bool _initialized = false;

        /// <summary>
        /// Initializes the database and its tables.
        /// </summary>
        public static async Task InitializeDatabaseAsync()
        {
            if (_initialized)
                return;
            _initialized = true;

            // Crear el archivo de la base de datos (packaged o unpackaged)
            try
            {
                var folder = ApplicationData.Current.LocalFolder;
                _ = await folder.CreateFileAsync("Lists.db", CreationCollisionOption.OpenIfExists);
            }
            catch (InvalidOperationException)
            {
                // Unpackaged: el archivo ya se crea implícitamente al abrir la conexión SQLite
                // Solo nos aseguramos de que el directorio exista
                var dir = Path.GetDirectoryName(DbPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            _db ??= new SQLiteConnection(DbPath);
            _asyncDb ??= new SQLiteAsyncConnection(DbPath);

            await _asyncDb.EnableWriteAheadLoggingAsync();

            await Task.WhenAll(
                _asyncDb.CreateTableAsync<Song>(),
                _asyncDb.CreateTableAsync<Artist>(),
                _asyncDb.CreateTableAsync<Album>(),
                _asyncDb.CreateTableAsync<Genre>(),
                _asyncDb.CreateTableAsync<Video>()
            );

            // Schema migration
            await TryAddColumnAsync("Songs", "PlayCount", "INTEGER NOT NULL DEFAULT 0");
            await TryAddColumnAsync("Songs", "SkipCount", "INTEGER NOT NULL DEFAULT 0");
            await TryAddColumnAsync("Songs", "LastPlayed", "TEXT NULL");
            await TryAddColumnAsync("Songs", "DateAdded", "TEXT NULL");

            _upsertQueue ??= new();
            _removeQueue ??= new();
        }

        // ... resto de métodos sin cambios ...
        public static List<T> GetItems<T>() where T : DbObject, new()
            => _db.Table<T>().ToList();

        public static Task<List<T>> GetItemsAsync<T>() where T : DbObject, new()
            => _asyncDb.Table<T>().ToListAsync();

        public static int Upsert(DbObject item) => _db.InsertOrReplace(item);
        public static Task<int> UpsertAsync(DbObject item) => _asyncDb.InsertOrReplaceAsync(item);

        public static bool QueueUpsert(DbObject item)
        {
            if (!_upsertQueue.Contains(item))
            {
                _upsertQueue.Enqueue(item);
                return true;
            }
            return false;
        }

        public static bool QueueRemove(DbObject item)
        {
            if (!_removeQueue.Contains(item))
            {
                _removeQueue.Enqueue(item);
                return true;
            }
            return false;
        }

        public static async Task UpsertQueuedAsync()
        {
            _ = await _asyncDb.InsertOrReplaceAllAsync(_upsertQueue);
            _upsertQueue.Clear();
        }

        public static async Task DeleteQueuedAsync()
        {
            _ = await _asyncDb.RemoveAllAsync(_removeQueue);
            _removeQueue.Clear();
        }

        public static int Delete(DbObject item) => _db.Delete(item);
        public static Task<int> DeleteAsync(DbObject item) => _asyncDb.DeleteAsync(item);

        private static async Task TryAddColumnAsync(string table, string column, string definition)
        {
            try
            {
                await _asyncDb.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
            }
            catch (SQLiteException ex) when (ex.Message.Contains("duplicate column"))
            {
                // Column already exists
            }
        }

        public static T GetItem<T>(Guid id) where T : DbObject, new()
        {
            var mapping = _db.GetMapping<T>();
            return _db.Query<T>(mapping.GetByPrimaryKeySql, new object[1] { id }).FirstOrDefault();
        }

        public static async Task<T> GetItemAsync<T>(Guid id) where T : DbObject, new()
        {
            var mapping = await _asyncDb.GetMappingAsync<T>().ConfigureAwait(false);
            return _db.Query<T>(mapping.GetByPrimaryKeySql, new object[1] { id }).FirstOrDefault();
        }
    }
}