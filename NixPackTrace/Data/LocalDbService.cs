using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NixPackTrace.Models;

namespace NixPackTrace.Data
{
    public class LocalDbService
    {
        private readonly string _connectionString;

        public LocalDbService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbDir = Path.Combine(appData, "NixPackTrace");
            if (!Directory.Exists(dbDir)) Directory.CreateDirectory(dbDir);
            string dbPath = Path.Combine(dbDir, "packtrace.db");
            _connectionString = $"Data Source={dbPath}";

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS PackingRecords (
                    ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                    MAC_ID      TEXT UNIQUE NOT NULL,
                    MAC_LENGTH  INTEGER,
                    LONG_QR     TEXT,
                    QR_LENGTH   INTEGER,
                    SHORT_QR    TEXT,
                    TESTING_QR  TEXT,
                    TESTING_QR_LENGTH INTEGER,
                    BOX_NO      INTEGER,
                    STATUS      TEXT DEFAULT 'OK',
                    TIMESTAMP   DATETIME,
                    PACKED_BY   TEXT,
                    Remarks     TEXT,
                    SYNC_STATUS TEXT DEFAULT 'Pending'
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Users (
                    Username TEXT PRIMARY KEY,
                    PasswordHash TEXT
                )");

            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS DispatchRecords (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    DispatchId TEXT UNIQUE NOT NULL,
                    FromBoxNo TEXT,
                    ToBoxNo TEXT,
                    DispatchDate DATETIME,
                    DispatchedBy TEXT,
                    Remarks TEXT,
                    SYNC_STATUS TEXT DEFAULT 'Pending'
                )");

            try
            {
                var existingColumns = connection.Query("PRAGMA table_info(PackingRecords)")
                    .Select(x => (string)((IDictionary<string, object>)x)["name"])
                    .ToList();

                if (!existingColumns.Contains("MAC_LENGTH", StringComparer.OrdinalIgnoreCase))
                    connection.Execute("ALTER TABLE PackingRecords ADD COLUMN MAC_LENGTH INTEGER DEFAULT 0;");

                if (!existingColumns.Contains("LONG_QR", StringComparer.OrdinalIgnoreCase))
                    connection.Execute("ALTER TABLE PackingRecords ADD COLUMN LONG_QR TEXT;");

                if (!existingColumns.Contains("QR_LENGTH", StringComparer.OrdinalIgnoreCase))
                    connection.Execute("ALTER TABLE PackingRecords ADD COLUMN QR_LENGTH INTEGER DEFAULT 0;");

                if (!existingColumns.Contains("SHORT_QR", StringComparer.OrdinalIgnoreCase))
                    connection.Execute("ALTER TABLE PackingRecords ADD COLUMN SHORT_QR TEXT;");

                if (!existingColumns.Contains("TESTING_QR", StringComparer.OrdinalIgnoreCase))
                    connection.Execute("ALTER TABLE PackingRecords ADD COLUMN TESTING_QR TEXT;");

                if (!existingColumns.Contains("TESTING_QR_LENGTH", StringComparer.OrdinalIgnoreCase))
                    connection.Execute("ALTER TABLE PackingRecords ADD COLUMN TESTING_QR_LENGTH INTEGER DEFAULT 0;");

                if (!existingColumns.Contains("PACKED_BY", StringComparer.OrdinalIgnoreCase))
                    connection.Execute("ALTER TABLE PackingRecords ADD COLUMN PACKED_BY TEXT;");

                if (!existingColumns.Contains("Remarks", StringComparer.OrdinalIgnoreCase))
                    connection.Execute("ALTER TABLE PackingRecords ADD COLUMN Remarks TEXT;");
            }
            catch { /* Ignore error on schema update */ }
            
            MigrateBoxNumbers();
        }
        
        private void MigrateBoxNumbers()
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                var records = connection.Query("SELECT MAC_ID, BOX_NO, TIMESTAMP FROM PackingRecords").ToList();
                foreach (var r in records)
                {
                    string boxNoStr = r.BOX_NO?.ToString() ?? "";
                    if (int.TryParse(boxNoStr, out int oldBoxNo))
                    {
                        DateTime ts = r.TIMESTAMP != null ? (DateTime)r.TIMESTAMP : DateTime.Now;
                        char monthChar = (char)('A' + ts.Month - 1);
                        string yearStr = ts.ToString("yy");
                        string newBoxNo = $"{monthChar}{yearStr}{oldBoxNo:D3}";
                        
                        connection.Execute("UPDATE PackingRecords SET BOX_NO = @newBoxNo WHERE MAC_ID = @macId", new { newBoxNo, macId = r.MAC_ID });
                    }
                }
                
                var dispatches = connection.Query("SELECT DispatchId, FromBoxNo, ToBoxNo, DispatchDate FROM DispatchRecords").ToList();
                foreach (var d in dispatches)
                {
                    string fromStr = d.FromBoxNo?.ToString() ?? "";
                    string toStr = d.ToBoxNo?.ToString() ?? "";
                    if (int.TryParse(fromStr, out int oldFrom) && int.TryParse(toStr, out int oldTo))
                    {
                        DateTime ts = d.DispatchDate != null ? (DateTime)d.DispatchDate : DateTime.Now;
                        char monthChar = (char)('A' + ts.Month - 1);
                        string yearStr = ts.ToString("yy");
                        string newFrom = $"{monthChar}{yearStr}{oldFrom:D3}";
                        string newTo = $"{monthChar}{yearStr}{oldTo:D3}";
                        
                        connection.Execute("UPDATE DispatchRecords SET FromBoxNo = @newFrom, ToBoxNo = @newTo WHERE DispatchId = @dId", new { newFrom, newTo, dId = d.DispatchId });
                    }
                }
            }
            catch { /* Ignore */ }
        }

        /// <summary>Inserts a new packing record. Returns false if MAC_ID already exists.</summary>
        public async Task<bool> InsertRecordAsync(PackingRecord record)
        {
            using var connection = new SqliteConnection(_connectionString);
            var sql = @"
                INSERT OR IGNORE INTO PackingRecords
                    (MAC_ID, MAC_LENGTH, LONG_QR, QR_LENGTH, SHORT_QR, TESTING_QR, TESTING_QR_LENGTH, BOX_NO, STATUS, TIMESTAMP, PACKED_BY, Remarks, SYNC_STATUS)
                VALUES
                    (@MAC_ID, @MAC_LENGTH, @LONG_QR, @QR_LENGTH, @SHORT_QR, @TESTING_QR, @TESTING_QR_LENGTH, @BOX_NO, @STATUS, @TIMESTAMP, @PACKED_BY, @Remarks, @SYNC_STATUS)";
            int affected = await connection.ExecuteAsync(sql, record);
            return affected > 0;
        }

        /// <summary>Checks whether a MAC_ID has already been packed locally.</summary>
        public async Task<bool> ExistsLocallyAsync(string macId)
        {
            using var connection = new SqliteConnection(_connectionString);
            int count = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PackingRecords WHERE MAC_ID = @macId", new { macId });
            return count > 0;
        }

        public async Task<string?> GetLocalBoxNoForMacAsync(string macId)
        {
            using var connection = new SqliteConnection(_connectionString);
            return await connection.ExecuteScalarAsync<string?>(
                "SELECT CAST(BOX_NO AS TEXT) FROM PackingRecords WHERE MAC_ID = @macId", new { macId });
        }

        /// <summary>Returns records with status Pending for cloud sync.</summary>
        public async Task<List<PackingRecord>> GetPendingSyncRecordsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            var result = await connection.QueryAsync<PackingRecord>(
                "SELECT ID, MAC_ID, MAC_LENGTH, LONG_QR, QR_LENGTH, SHORT_QR, TESTING_QR, TESTING_QR_LENGTH, CAST(BOX_NO AS TEXT) AS BOX_NO, STATUS, TIMESTAMP, PACKED_BY, Remarks, SYNC_STATUS FROM PackingRecords WHERE SYNC_STATUS = 'Pending'");
            return result.ToList();
        }

        /// <summary>Marks a record as Synced in local DB.</summary>
        public async Task MarkAsSyncedAsync(string macId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.ExecuteAsync(
                "UPDATE PackingRecords SET SYNC_STATUS = 'Synced' WHERE MAC_ID = @macId",
                new { macId });
        }

        /// <summary>Returns all records between two dates (inclusive) ordered newest first.</summary>
        public async Task<List<PackingRecord>> GetRecordsByDateRangeAsync(DateTime from, DateTime to)
        {
            using var connection = new SqliteConnection(_connectionString);
            var result = await connection.QueryAsync<PackingRecord>(@"
                SELECT p.ID, p.MAC_ID, p.MAC_LENGTH, p.LONG_QR, p.QR_LENGTH, p.SHORT_QR, p.TESTING_QR, p.TESTING_QR_LENGTH, CAST(p.BOX_NO AS TEXT) AS BOX_NO, p.STATUS, p.TIMESTAMP, p.PACKED_BY, p.Remarks, p.SYNC_STATUS, d.DispatchDate, d.DispatchedBy AS DispatchBy, d.Remarks AS DispatchRemarks 
                FROM PackingRecords p
                LEFT JOIN DispatchRecords d ON 
                    (UPPER(CAST(p.BOX_NO AS TEXT)) GLOB '[A-Z]*' AND CAST(p.BOX_NO AS TEXT) >= d.FromBoxNo AND CAST(p.BOX_NO AS TEXT) <= d.ToBoxNo)
                    OR
                    (UPPER(CAST(p.BOX_NO AS TEXT)) NOT GLOB '[A-Z]*' AND CAST(p.BOX_NO AS INTEGER) >= CAST(d.FromBoxNo AS INTEGER) AND CAST(p.BOX_NO AS INTEGER) <= CAST(d.ToBoxNo AS INTEGER))
                WHERE p.TIMESTAMP >= @from AND p.TIMESTAMP < @to
                ORDER BY p.TIMESTAMP DESC",
                new { from = from.Date, to = to.Date.AddDays(1) });
            return result.ToList();
        }

        public async Task<List<PackingRecord>> SearchRecordsAsync(string term)
        {
            using var connection = new SqliteConnection(_connectionString);
            string q = $"%{term}%";
            var result = await connection.QueryAsync<PackingRecord>(@"
                SELECT p.ID, p.MAC_ID, p.MAC_LENGTH, p.LONG_QR, p.QR_LENGTH, p.SHORT_QR, p.TESTING_QR, p.TESTING_QR_LENGTH, CAST(p.BOX_NO AS TEXT) AS BOX_NO, p.STATUS, p.TIMESTAMP, p.PACKED_BY, p.Remarks, p.SYNC_STATUS, d.DispatchDate, d.DispatchedBy AS DispatchBy, d.Remarks AS DispatchRemarks 
                FROM PackingRecords p
                LEFT JOIN DispatchRecords d ON 
                    (UPPER(CAST(p.BOX_NO AS TEXT)) GLOB '[A-Z]*' AND CAST(p.BOX_NO AS TEXT) >= d.FromBoxNo AND CAST(p.BOX_NO AS TEXT) <= d.ToBoxNo)
                    OR
                    (UPPER(CAST(p.BOX_NO AS TEXT)) NOT GLOB '[A-Z]*' AND CAST(p.BOX_NO AS INTEGER) >= CAST(d.FromBoxNo AS INTEGER) AND CAST(p.BOX_NO AS INTEGER) <= CAST(d.ToBoxNo AS INTEGER))
                WHERE p.MAC_ID LIKE @q OR p.LONG_QR LIKE @q OR p.SHORT_QR LIKE @q OR CAST(p.BOX_NO AS TEXT) LIKE @q
                ORDER BY p.TIMESTAMP DESC LIMIT 50", new { q });
            return result.ToList();
        }

        public async Task<bool> DeleteRecordAsync(string macId)
        {
            using var connection = new SqliteConnection(_connectionString);
            int affected = await connection.ExecuteAsync("DELETE FROM PackingRecords WHERE MAC_ID = @macId", new { macId });
            return affected > 0;
        }

        public async Task<bool> UpdateRecordAsync(PackingRecord record)
        {
            using var connection = new SqliteConnection(_connectionString);
            var sql = @"
                UPDATE PackingRecords SET 
                    BOX_NO = @BOX_NO, 
                    STATUS = @STATUS, 
                    SYNC_STATUS = @SYNC_STATUS
                WHERE MAC_ID = @MAC_ID";
            int affected = await connection.ExecuteAsync(sql, record);
            return affected > 0;
        }

        // ── Dispatch Operations ──────────────────────────────────────────────────

        public async Task<bool> InsertDispatchAsync(DispatchRecord record)
        {
            using var connection = new SqliteConnection(_connectionString);
            var sql = @"
                INSERT OR IGNORE INTO DispatchRecords
                    (DispatchId, FromBoxNo, ToBoxNo, DispatchDate, DispatchedBy, Remarks, SYNC_STATUS)
                VALUES
                    (@DispatchId, @FromBoxNo, @ToBoxNo, @DispatchDate, @DispatchedBy, @Remarks, @SYNC_STATUS)";
            int affected = await connection.ExecuteAsync(sql, record);
            return affected > 0;
        }

        public async Task<List<DispatchRecord>> GetPendingDispatchRecordsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            var result = await connection.QueryAsync<DispatchRecord>(
                "SELECT * FROM DispatchRecords WHERE SYNC_STATUS = 'Pending'");
            return result.ToList();
        }

        public async Task MarkDispatchAsSyncedAsync(string dispatchId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.ExecuteAsync(
                "UPDATE DispatchRecords SET SYNC_STATUS = 'Synced' WHERE DispatchId = @dispatchId",
                new { dispatchId });
        }

        public async Task<List<DispatchRecord>> GetRecentDispatchesAsync(int limit = 50)
        {
            using var connection = new SqliteConnection(_connectionString);
            var result = await connection.QueryAsync<DispatchRecord>(
                "SELECT * FROM DispatchRecords ORDER BY DispatchDate DESC LIMIT @limit",
                new { limit });
            return result.ToList();
        }

        public async Task<bool> DeleteDispatchAsync(string dispatchId)
        {
            using var connection = new SqliteConnection(_connectionString);
            int affected = await connection.ExecuteAsync(
                "DELETE FROM DispatchRecords WHERE DispatchId = @dispatchId",
                new { dispatchId });
            return affected > 0;
        }

        /// <summary>Returns the current item count inside a specific box for the current month.</summary>
        public async Task<int> GetBoxCountAsync(string boxNo)
        {
            using var connection = new SqliteConnection(_connectionString);
            var firstDayOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PackingRecords WHERE CAST(BOX_NO AS TEXT) = @boxNo", 
                new { boxNo });
        }

        /// <summary>Returns total records packed today.</summary>
        public async Task<int> GetTodayCountAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            return await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PackingRecords WHERE TIMESTAMP >= @startDate",
                new { startDate = DateTime.Today });
        }

        /// <summary>Returns the highest box sequence number used this month.</summary>
        public async Task<int> GetLastBoxSequenceAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            var firstDayOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            
            // Generate prefix for current month, e.g. A26
            char monthChar = (char)('A' + DateTime.Now.Month - 1);
            string yearStr = DateTime.Now.ToString("yy");
            string prefix = $"{monthChar}{yearStr}";

            var list = await connection.QueryAsync<string>(
                "SELECT CAST(BOX_NO AS TEXT) FROM PackingRecords WHERE TIMESTAMP >= @startOfMonth",
                new { startOfMonth = firstDayOfMonth });
                
            int maxSeq = 0;
            foreach (var b in list)
            {
                if (b != null && b.StartsWith(prefix) && b.Length > prefix.Length)
                {
                    if (int.TryParse(b.Substring(prefix.Length), out int seq))
                    {
                        if (seq > maxSeq) maxSeq = seq;
                    }
                }
            }
            return maxSeq;
        }

        // ── Authentication ──────────────────────────────────────────────────────

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public async Task<bool> HasAnyUserAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            int count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users");
            return count > 0;
        }

        public async Task<bool> CreateUserAsync(string username, string password)
        {
            using var connection = new SqliteConnection(_connectionString);
            try
            {
                var hash = HashPassword(password);
                int affected = await connection.ExecuteAsync(
                    "INSERT INTO Users (Username, PasswordHash) VALUES (@username, @hash)",
                    new { username, hash });
                return affected > 0;
            }
            catch
            {
                return false; // probably already exists
            }
        }

        public async Task<bool> ValidateUserAsync(string username, string password)
        {
            using var connection = new SqliteConnection(_connectionString);
            var hash = await connection.ExecuteScalarAsync<string>(
                "SELECT PasswordHash FROM Users WHERE Username = @username",
                new { username });

            if (hash == null) return false;
            return hash == HashPassword(password);
        }
    }
}
