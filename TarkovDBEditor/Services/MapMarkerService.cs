using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TarkovDBEditor.Models;

namespace TarkovDBEditor.Services;

/// <summary>
/// Map Marker DB 관리 서비스
/// </summary>
public class MapMarkerService
{
    private static MapMarkerService? _instance;
    public static MapMarkerService Instance => _instance ??= new MapMarkerService();

    private readonly DatabaseService _db = DatabaseService.Instance;
    private bool _tableInitialized = false;

    private MapMarkerService() { }

    /// <summary>
    /// MapMarkers 테이블이 없으면 생성
    /// </summary>
    public async Task EnsureTableExistsAsync()
    {
        if (!_db.IsConnected || _tableInitialized) return;

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = @"
            CREATE TABLE IF NOT EXISTS MapMarkers (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                NameKo TEXT,
                MarkerType TEXT NOT NULL,
                MapKey TEXT NOT NULL,
                X REAL NOT NULL DEFAULT 0,
                Y REAL NOT NULL DEFAULT 0,
                Z REAL NOT NULL DEFAULT 0,
                FloorId TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )";

        await using var cmd = new SqliteCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();

        // 인덱스 생성
        var indexSql = @"
            CREATE INDEX IF NOT EXISTS idx_mapmarkers_mapkey ON MapMarkers(MapKey);
            CREATE INDEX IF NOT EXISTS idx_mapmarkers_type ON MapMarkers(MarkerType)";
        await using var indexCmd = new SqliteCommand(indexSql, connection);
        await indexCmd.ExecuteNonQueryAsync();

        // 스키마 메타 등록 (Tables 목록에 표시하기 위함)
        await RegisterSchemaMetaAsync(connection);

        _tableInitialized = true;
    }

    private async Task RegisterSchemaMetaAsync(SqliteConnection connection)
    {
        var columns = new List<ColumnSchema>
        {
            new() { Name = "Id", DisplayName = "ID", Type = ColumnType.Text, IsPrimaryKey = true, SortOrder = 0 },
            new() { Name = "Name", DisplayName = "Name", Type = ColumnType.Text, IsRequired = true, SortOrder = 1 },
            new() { Name = "NameKo", DisplayName = "Name (KO)", Type = ColumnType.Text, SortOrder = 2 },
            new() { Name = "MarkerType", DisplayName = "Type", Type = ColumnType.Text, IsRequired = true, SortOrder = 3 },
            new() { Name = "MapKey", DisplayName = "Map", Type = ColumnType.Text, IsRequired = true, SortOrder = 4 },
            new() { Name = "X", DisplayName = "X", Type = ColumnType.Real, IsRequired = true, SortOrder = 5 },
            new() { Name = "Y", DisplayName = "Y", Type = ColumnType.Real, IsRequired = true, SortOrder = 6 },
            new() { Name = "Z", DisplayName = "Z", Type = ColumnType.Real, IsRequired = true, SortOrder = 7 },
            new() { Name = "FloorId", DisplayName = "Floor", Type = ColumnType.Text, SortOrder = 8 },
            new() { Name = "CreatedAt", DisplayName = "Created At", Type = ColumnType.DateTime, SortOrder = 9 },
            new() { Name = "UpdatedAt", DisplayName = "Updated At", Type = ColumnType.DateTime, SortOrder = 10 }
        };

        var schemaJson = JsonSerializer.Serialize(columns);

        // Check if exists
        var checkSql = "SELECT COUNT(*) FROM _schema_meta WHERE TableName = @TableName";
        await using var checkCmd = new SqliteCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue("@TableName", "MapMarkers");
        var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

        if (count == 0)
        {
            var insertSql = @"
                INSERT INTO _schema_meta (TableName, DisplayName, SchemaJson, CreatedAt, UpdatedAt)
                VALUES (@TableName, @DisplayName, @SchemaJson, @Now, @Now)";
            await using var insertCmd = new SqliteCommand(insertSql, connection);
            insertCmd.Parameters.AddWithValue("@TableName", "MapMarkers");
            insertCmd.Parameters.AddWithValue("@DisplayName", "Map Markers");
            insertCmd.Parameters.AddWithValue("@SchemaJson", schemaJson);
            insertCmd.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));
            await insertCmd.ExecuteNonQueryAsync();
        }
        else
        {
            var updateSql = @"
                UPDATE _schema_meta SET SchemaJson = @SchemaJson, UpdatedAt = @Now
                WHERE TableName = @TableName";
            await using var updateCmd = new SqliteCommand(updateSql, connection);
            updateCmd.Parameters.AddWithValue("@TableName", "MapMarkers");
            updateCmd.Parameters.AddWithValue("@SchemaJson", schemaJson);
            updateCmd.Parameters.AddWithValue("@Now", DateTime.UtcNow.ToString("o"));
            await updateCmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// 모든 마커 로드
    /// </summary>
    public async Task<List<MapMarker>> LoadAllMarkersAsync()
    {
        var markers = new List<MapMarker>();
        if (!_db.IsConnected) return markers;

        await EnsureTableExistsAsync();

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT Id, Name, NameKo, MarkerType, MapKey, X, Y, Z, FloorId FROM MapMarkers";
        await using var cmd = new SqliteCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var marker = new MapMarker
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                NameKo = reader.IsDBNull(2) ? null : reader.GetString(2),
                MarkerType = Enum.TryParse<MapMarkerType>(reader.GetString(3), out var type) ? type : MapMarkerType.PmcExtraction,
                MapKey = reader.GetString(4),
                X = reader.GetDouble(5),
                Y = reader.GetDouble(6),
                Z = reader.GetDouble(7),
                FloorId = reader.IsDBNull(8) ? null : reader.GetString(8)
            };
            markers.Add(marker);
        }

        return markers;
    }

    /// <summary>
    /// 특정 맵의 마커 로드
    /// </summary>
    public async Task<List<MapMarker>> LoadMarkersByMapAsync(string mapKey)
    {
        var markers = new List<MapMarker>();
        if (!_db.IsConnected) return markers;

        await EnsureTableExistsAsync();

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "SELECT Id, Name, NameKo, MarkerType, MapKey, X, Y, Z, FloorId FROM MapMarkers WHERE MapKey = @MapKey";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@MapKey", mapKey);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var marker = new MapMarker
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                NameKo = reader.IsDBNull(2) ? null : reader.GetString(2),
                MarkerType = Enum.TryParse<MapMarkerType>(reader.GetString(3), out var type) ? type : MapMarkerType.PmcExtraction,
                MapKey = reader.GetString(4),
                X = reader.GetDouble(5),
                Y = reader.GetDouble(6),
                Z = reader.GetDouble(7),
                FloorId = reader.IsDBNull(8) ? null : reader.GetString(8)
            };
            markers.Add(marker);
        }

        return markers;
    }

    /// <summary>
    /// 마커 추가 또는 업데이트
    /// </summary>
    public async Task SaveMarkerAsync(MapMarker marker)
    {
        if (!_db.IsConnected) return;

        await EnsureTableExistsAsync();

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("o");

        // UPSERT (INSERT OR REPLACE)
        var sql = @"
            INSERT INTO MapMarkers (Id, Name, NameKo, MarkerType, MapKey, X, Y, Z, FloorId, CreatedAt, UpdatedAt)
            VALUES (@Id, @Name, @NameKo, @MarkerType, @MapKey, @X, @Y, @Z, @FloorId, @CreatedAt, @UpdatedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Name = @Name,
                NameKo = @NameKo,
                MarkerType = @MarkerType,
                MapKey = @MapKey,
                X = @X,
                Y = @Y,
                Z = @Z,
                FloorId = @FloorId,
                UpdatedAt = @UpdatedAt";

        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", marker.Id);
        cmd.Parameters.AddWithValue("@Name", marker.Name);
        cmd.Parameters.AddWithValue("@NameKo", (object?)marker.NameKo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MarkerType", marker.MarkerType.ToString());
        cmd.Parameters.AddWithValue("@MapKey", marker.MapKey);
        cmd.Parameters.AddWithValue("@X", marker.X);
        cmd.Parameters.AddWithValue("@Y", marker.Y);
        cmd.Parameters.AddWithValue("@Z", marker.Z);
        cmd.Parameters.AddWithValue("@FloorId", (object?)marker.FloorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", now);
        cmd.Parameters.AddWithValue("@UpdatedAt", now);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 여러 마커 일괄 저장
    /// </summary>
    public async Task SaveMarkersAsync(IEnumerable<MapMarker> markers)
    {
        if (!_db.IsConnected) return;

        await EnsureTableExistsAsync();

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("o");

        await using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var marker in markers)
            {
                var sql = @"
                    INSERT INTO MapMarkers (Id, Name, NameKo, MarkerType, MapKey, X, Y, Z, FloorId, CreatedAt, UpdatedAt)
                    VALUES (@Id, @Name, @NameKo, @MarkerType, @MapKey, @X, @Y, @Z, @FloorId, @CreatedAt, @UpdatedAt)
                    ON CONFLICT(Id) DO UPDATE SET
                        Name = @Name,
                        NameKo = @NameKo,
                        MarkerType = @MarkerType,
                        MapKey = @MapKey,
                        X = @X,
                        Y = @Y,
                        Z = @Z,
                        FloorId = @FloorId,
                        UpdatedAt = @UpdatedAt";

                await using var cmd = new SqliteCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@Id", marker.Id);
                cmd.Parameters.AddWithValue("@Name", marker.Name);
                cmd.Parameters.AddWithValue("@NameKo", (object?)marker.NameKo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MarkerType", marker.MarkerType.ToString());
                cmd.Parameters.AddWithValue("@MapKey", marker.MapKey);
                cmd.Parameters.AddWithValue("@X", marker.X);
                cmd.Parameters.AddWithValue("@Y", marker.Y);
                cmd.Parameters.AddWithValue("@Z", marker.Z);
                cmd.Parameters.AddWithValue("@FloorId", (object?)marker.FloorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedAt", now);
                cmd.Parameters.AddWithValue("@UpdatedAt", now);

                await cmd.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 마커 삭제
    /// </summary>
    public async Task DeleteMarkerAsync(string markerId)
    {
        if (!_db.IsConnected) return;

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "DELETE FROM MapMarkers WHERE Id = @Id";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", markerId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 특정 맵의 모든 마커 삭제
    /// </summary>
    public async Task DeleteMarkersByMapAsync(string mapKey)
    {
        if (!_db.IsConnected) return;

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var sql = "DELETE FROM MapMarkers WHERE MapKey = @MapKey";
        await using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@MapKey", mapKey);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 기존 마커 목록과 동기화 (없는 것 삭제, 있는 것 업데이트)
    /// </summary>
    public async Task SyncMarkersAsync(IEnumerable<MapMarker> markers)
    {
        if (!_db.IsConnected) return;

        await EnsureTableExistsAsync();

        var connectionString = $"Data Source={_db.DatabasePath}";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("o");
        var markerIds = new HashSet<string>(markers.Select(m => m.Id));

        await using var transaction = connection.BeginTransaction();

        try
        {
            // 기존 ID 목록 조회
            var existingIds = new HashSet<string>();
            var selectSql = "SELECT Id FROM MapMarkers";
            await using (var selectCmd = new SqliteCommand(selectSql, connection, transaction))
            await using (var reader = await selectCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingIds.Add(reader.GetString(0));
                }
            }

            // 새 목록에 없는 마커 삭제
            var idsToDelete = existingIds.Except(markerIds);
            foreach (var id in idsToDelete)
            {
                var deleteSql = "DELETE FROM MapMarkers WHERE Id = @Id";
                await using var deleteCmd = new SqliteCommand(deleteSql, connection, transaction);
                deleteCmd.Parameters.AddWithValue("@Id", id);
                await deleteCmd.ExecuteNonQueryAsync();
            }

            // 마커 저장/업데이트
            foreach (var marker in markers)
            {
                var sql = @"
                    INSERT INTO MapMarkers (Id, Name, NameKo, MarkerType, MapKey, X, Y, Z, FloorId, CreatedAt, UpdatedAt)
                    VALUES (@Id, @Name, @NameKo, @MarkerType, @MapKey, @X, @Y, @Z, @FloorId, @CreatedAt, @UpdatedAt)
                    ON CONFLICT(Id) DO UPDATE SET
                        Name = @Name,
                        NameKo = @NameKo,
                        MarkerType = @MarkerType,
                        MapKey = @MapKey,
                        X = @X,
                        Y = @Y,
                        Z = @Z,
                        FloorId = @FloorId,
                        UpdatedAt = @UpdatedAt";

                await using var cmd = new SqliteCommand(sql, connection, transaction);
                cmd.Parameters.AddWithValue("@Id", marker.Id);
                cmd.Parameters.AddWithValue("@Name", marker.Name);
                cmd.Parameters.AddWithValue("@NameKo", (object?)marker.NameKo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@MarkerType", marker.MarkerType.ToString());
                cmd.Parameters.AddWithValue("@MapKey", marker.MapKey);
                cmd.Parameters.AddWithValue("@X", marker.X);
                cmd.Parameters.AddWithValue("@Y", marker.Y);
                cmd.Parameters.AddWithValue("@Z", marker.Z);
                cmd.Parameters.AddWithValue("@FloorId", (object?)marker.FloorId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedAt", now);
                cmd.Parameters.AddWithValue("@UpdatedAt", now);

                await cmd.ExecuteNonQueryAsync();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
