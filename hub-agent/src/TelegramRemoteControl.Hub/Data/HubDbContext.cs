using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace TelegramRemoteControl.Hub.Data;

public class HubDbContext
{
    private readonly string _connectionString;
    private readonly ILogger<HubDbContext> _logger;

    public HubDbContext(IOptions<HubSettings> settings, ILogger<HubDbContext> logger)
    {
        var dbPath = settings.Value.DatabasePath;
        if (string.IsNullOrEmpty(dbPath))
            dbPath = "hub.db";

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
                RegisteredAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS PairingRequests (
                Code TEXT PRIMARY KEY,
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
            """;
        await cmd.ExecuteNonQueryAsync();

        await EnsureUsersColumnsAsync(conn);
        _logger.LogInformation("Database initialized");
    }

    // --- Agents ---

    public async Task<AgentRegistration?> GetAgentByToken(string token)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AgentId, AgentToken, OwnerUserId, MachineName, FriendlyName, RegisteredAt FROM Agents WHERE AgentToken = @token";
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
        cmd.CommandText = "SELECT AgentId, AgentToken, OwnerUserId, MachineName, FriendlyName, RegisteredAt FROM Agents WHERE AgentId = @id";
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
        cmd.CommandText = "SELECT AgentId, AgentToken, OwnerUserId, MachineName, FriendlyName, RegisteredAt FROM Agents WHERE OwnerUserId = @userId";
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
            INSERT OR REPLACE INTO PairingRequests (Code, UserId, ExpiresAt)
            VALUES (@code, @userId, @expires)
            """;
        cmd.Parameters.AddWithValue("@code", request.Code);
        cmd.Parameters.AddWithValue("@userId", request.UserId);
        cmd.Parameters.AddWithValue("@expires", request.ExpiresAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<PairingRequest?> GetPairingRequest(string code)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code, UserId, ExpiresAt FROM PairingRequests WHERE Code = @code";
        cmd.Parameters.AddWithValue("@code", code);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new PairingRequest
        {
            Code = reader.GetString(0),
            UserId = reader.GetInt64(1),
            ExpiresAt = DateTime.Parse(reader.GetString(2))
        };
    }

    public async Task DeletePairingRequest(string code)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM PairingRequests WHERE Code = @code";
        cmd.Parameters.AddWithValue("@code", code);

        await cmd.ExecuteNonQueryAsync();
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
            RegisteredAt = DateTime.Parse(reader.GetString(5))
        };
    }
}
