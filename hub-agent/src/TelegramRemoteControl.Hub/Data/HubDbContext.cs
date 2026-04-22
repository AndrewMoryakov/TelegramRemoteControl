using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace TelegramRemoteControl.Hub.Data;

public class HubDbContext
{
    private readonly string _connectionString;
    private readonly ILogger<HubDbContext> _logger;

    public string DbPath { get; }

    public HubDbContext(IOptions<HubSettings> settings, ILogger<HubDbContext> logger)
    {
        var dbPath = settings.Value.DatabasePath;
        if (string.IsNullOrEmpty(dbPath))
            dbPath = "hub.db";

        DbPath = dbPath;
        _connectionString = $"Data Source={dbPath}";
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Agents (
                AgentId TEXT PRIMARY KEY,
                AgentToken TEXT NOT NULL UNIQUE,
                OwnerUserId INTEGER NOT NULL,
                MachineName TEXT NOT NULL,
                FriendlyName TEXT,
                RegisteredAt TEXT NOT NULL,
                LastSeenAt TEXT
            );

            CREATE TABLE IF NOT EXISTS PairingRequests (
                CodeHash TEXT PRIMARY KEY,
                UserId INTEGER NOT NULL,
                ExpiresAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Users (
                UserId INTEGER PRIMARY KEY,
                Username TEXT,
                FirstName TEXT,
                FirstSeen TEXT NOT NULL,
                LastSeen TEXT NOT NULL,
                NotifyDeviceStatus INTEGER NOT NULL DEFAULT 0,
                IsAuthorized INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS UserSelections (
                UserId INTEGER PRIMARY KEY,
                AgentId TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                UserId INTEGER NOT NULL,
                AgentId TEXT NOT NULL,
                CommandType TEXT NOT NULL,
                Arguments TEXT,
                Success INTEGER NOT NULL,
                DurationMs INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_AuditLog_Timestamp ON AuditLog(Timestamp);
            CREATE INDEX IF NOT EXISTS IX_AuditLog_UserId ON AuditLog(UserId);
            """;
        await cmd.ExecuteNonQueryAsync();

        await EnsureAgentsColumnsAsync(conn);
        await EnsureUsersColumnsAsync(conn);
        await EnsurePairingSchemaAsync(conn, _logger);
        _logger.LogInformation("Database initialized");
    }

    // --- Agents ---

    public async Task<AgentRegistration?> GetAgentByToken(string token)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AgentId, AgentToken, OwnerUserId, MachineName, FriendlyName, RegisteredAt, LastSeenAt FROM Agents WHERE AgentToken = @token";
        cmd.Parameters.AddWithValue("@token", token);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return ReadAgent(reader);
    }

    public async Task<AgentRegistration?> GetAgentById(string agentId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AgentId, AgentToken, OwnerUserId, MachineName, FriendlyName, RegisteredAt, LastSeenAt FROM Agents WHERE AgentId = @id";
        cmd.Parameters.AddWithValue("@id", agentId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return ReadAgent(reader);
    }

    public async Task<List<AgentRegistration>> GetAgentsByUser(long userId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AgentId, AgentToken, OwnerUserId, MachineName, FriendlyName, RegisteredAt, LastSeenAt FROM Agents WHERE OwnerUserId = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);

        var result = new List<AgentRegistration>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(ReadAgent(reader));

        return result;
    }

    public async Task AddAgent(AgentRegistration agent)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Agents (AgentId, AgentToken, OwnerUserId, MachineName, FriendlyName, RegisteredAt)
            VALUES (@id, @token, @userId, @machine, @friendly, @registered)
            """;
        cmd.Parameters.AddWithValue("@id", agent.AgentId);
        cmd.Parameters.AddWithValue("@token", agent.AgentToken);
        cmd.Parameters.AddWithValue("@userId", agent.OwnerUserId);
        cmd.Parameters.AddWithValue("@machine", agent.MachineName);
        cmd.Parameters.AddWithValue("@friendly", (object?)agent.FriendlyName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@registered", agent.RegisteredAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> GetAgentCountByUser(long userId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Agents WHERE OwnerUserId = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    // --- Users ---

    public async Task UpsertUser(long userId, string? username, string? firstName)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Users (UserId, Username, FirstName, FirstSeen, LastSeen, NotifyDeviceStatus, IsAuthorized)
            VALUES (@userId, @username, @firstName, @now, @now, 0, 0)
            ON CONFLICT(UserId) DO UPDATE SET
                Username = COALESCE(@username, Users.Username),
                FirstName = COALESCE(@firstName, Users.FirstName),
                LastSeen = @now
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@username", (object?)username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@firstName", (object?)firstName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetUserNotifyStatus(long userId, bool enabled)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Users (UserId, FirstSeen, LastSeen, NotifyDeviceStatus)
            VALUES (@userId, @now, @now, @notify)
            ON CONFLICT(UserId) DO UPDATE SET
                NotifyDeviceStatus = @notify,
                LastSeen = @now
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@notify", enabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> GetUserNotifyStatus(long userId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT NotifyDeviceStatus FROM Users WHERE UserId = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);

        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
            return false;

        return Convert.ToInt32(result) != 0;
    }

    public async Task<List<long>> GetNotifiedUsers()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT UserId FROM Users WHERE NotifyDeviceStatus = 1 AND IsAuthorized = 1";

        var result = new List<long>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(reader.GetInt64(0));

        return result;
    }

    public async Task<bool> GetUserAuthorized(long userId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT IsAuthorized FROM Users WHERE UserId = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);

        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
            return false;

        return Convert.ToInt32(result) != 0;
    }

    public async Task SetUserAuthorized(long userId, bool authorized)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Users (UserId, FirstSeen, LastSeen, NotifyDeviceStatus, IsAuthorized)
            VALUES (@userId, @now, @now, 0, @authorized)
            ON CONFLICT(UserId) DO UPDATE SET
                IsAuthorized = @authorized,
                LastSeen = @now
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@authorized", authorized ? 1 : 0);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    // --- User selections ---

    public async Task SetSelectedAgent(long userId, string agentId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO UserSelections (UserId, AgentId, UpdatedAt)
            VALUES (@userId, @agentId, @now)
            ON CONFLICT(UserId) DO UPDATE SET
                AgentId = @agentId,
                UpdatedAt = @now
            """;
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@agentId", agentId);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetSelectedAgent(long userId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AgentId FROM UserSelections WHERE UserId = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);

        var result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public async Task UpdateAgentLastSeenAsync(string agentId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Agents SET LastSeenAt = @now WHERE AgentId = @id";
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", agentId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<UserRecord>> GetAllUsersAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT UserId, Username, FirstName, FirstSeen, LastSeen, IsAuthorized FROM Users ORDER BY LastSeen DESC";
        var result = new List<UserRecord>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new UserRecord
            {
                UserId = reader.GetInt64(0),
                Username = reader.IsDBNull(1) ? null : reader.GetString(1),
                FirstName = reader.IsDBNull(2) ? null : reader.GetString(2),
                FirstSeen = DateTime.Parse(reader.GetString(3)),
                LastSeen = DateTime.Parse(reader.GetString(4)),
                IsAuthorized = reader.GetInt32(5) != 0
            });
        }
        return result;
    }

    private static async Task EnsureAgentsColumnsAsync(SqliteConnection conn)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(Agents)";
        await using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));

        if (!columns.Contains("LastSeenAt"))
        {
            var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Agents ADD COLUMN LastSeenAt TEXT";
            await alter.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsurePairingSchemaAsync(SqliteConnection conn, ILogger logger)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(PairingRequests)";
        await using (var reader = await pragma.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(1));
        }

        if (columns.Count == 0)
            return; // freshly created with new schema

        if (columns.Contains("CodeHash"))
            return; // already migrated

        // Legacy schema with plain `Code` column — drop existing (all plain-text codes are compromised anyway)
        // and recreate with the hashed-code schema. Any in-flight pairing codes must be regenerated.
        logger.LogWarning("PairingRequests legacy schema detected. Dropping existing pairing codes and recreating table with CodeHash.");

        var drop = conn.CreateCommand();
        drop.CommandText = """
            DROP TABLE PairingRequests;
            CREATE TABLE PairingRequests (
                CodeHash TEXT PRIMARY KEY,
                UserId INTEGER NOT NULL,
                ExpiresAt TEXT NOT NULL
            );
            """;
        await drop.ExecuteNonQueryAsync();
    }

    private static async Task EnsureUsersColumnsAsync(SqliteConnection conn)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(Users)";
        await using var reader = await pragma.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        if (!columns.Contains("IsAuthorized"))
        {
            var alter = conn.CreateCommand();
            alter.CommandText = "ALTER TABLE Users ADD COLUMN IsAuthorized INTEGER NOT NULL DEFAULT 1";
            await alter.ExecuteNonQueryAsync();
        }
    }
    // --- Pairing ---

    public async Task AddPairingRequest(PairingRequest request)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO PairingRequests (CodeHash, UserId, ExpiresAt)
            VALUES (@hash, @userId, @expires)
            """;
        cmd.Parameters.AddWithValue("@hash", request.CodeHash);
        cmd.Parameters.AddWithValue("@userId", request.UserId);
        cmd.Parameters.AddWithValue("@expires", request.ExpiresAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<PairingRequest?> GetPairingRequestByCode(string code)
    {
        var hash = PairingRequest.HashCode(code);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CodeHash, UserId, ExpiresAt FROM PairingRequests WHERE CodeHash = @hash";
        cmd.Parameters.AddWithValue("@hash", hash);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new PairingRequest
        {
            CodeHash = reader.GetString(0),
            UserId = reader.GetInt64(1),
            ExpiresAt = DateTime.Parse(reader.GetString(2))
        };
    }

    public async Task DeletePairingRequestByCode(string code)
    {
        var hash = PairingRequest.HashCode(code);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM PairingRequests WHERE CodeHash = @hash";
        cmd.Parameters.AddWithValue("@hash", hash);

        await cmd.ExecuteNonQueryAsync();
    }

    // --- Stats ---

    public async Task<(int totalAgents, int commandsToday)> GetStatsAsync()
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM Agents) AS TotalAgents,
                (SELECT COUNT(*) FROM AuditLog WHERE date(Timestamp) = date('now')) AS CommandsToday
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return (reader.GetInt32(0), reader.GetInt32(1));

        return (0, 0);
    }

    public async Task<List<(DateTime Timestamp, string CommandType, string AgentId, bool Success, long DurationMs)>> GetRecentCommandsAsync(int limit = 20)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Timestamp, CommandType, AgentId, Success, DurationMs FROM AuditLog ORDER BY Id DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);

        var result = new List<(DateTime, string, string, bool, long)>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add((
                DateTime.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3) != 0,
                reader.GetInt64(4)
            ));
        }
        return result;
    }

    // --- Audit log ---

    public async Task AddAuditLog(long userId, string agentId, string commandType, string? arguments, bool success, long durationMs)
    {
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO AuditLog (Timestamp, UserId, AgentId, CommandType, Arguments, Success, DurationMs)
                VALUES (@ts, @userId, @agentId, @type, @args, @success, @duration)
                """;
            cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("O"));
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@agentId", agentId);
            cmd.Parameters.AddWithValue("@type", commandType);
            cmd.Parameters.AddWithValue("@args", arguments != null ? (object)arguments[..Math.Min(500, arguments.Length)] : DBNull.Value);
            cmd.Parameters.AddWithValue("@success", success ? 1 : 0);
            cmd.Parameters.AddWithValue("@duration", durationMs);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write audit log");
        }
    }

    private static AgentRegistration ReadAgent(SqliteDataReader reader)
    {
        return new AgentRegistration
        {
            AgentId = reader.GetString(0),
            AgentToken = reader.GetString(1),
            OwnerUserId = reader.GetInt64(2),
            MachineName = reader.GetString(3),
            FriendlyName = reader.IsDBNull(4) ? null : reader.GetString(4),
            RegisteredAt = DateTime.Parse(reader.GetString(5)),
            LastSeenAt = reader.FieldCount > 6 && !reader.IsDBNull(6) ? DateTime.Parse(reader.GetString(6)) : null
        };
    }
}

public class UserRecord
{
    public long UserId { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsAuthorized { get; set; }
}
