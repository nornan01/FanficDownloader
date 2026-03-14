using Npgsql;
using Microsoft.Extensions.Configuration;
using FanficDownloader.Core.Models;

namespace FanficDownloader.Application.Services;

public class FanficCacheService
{
    private readonly string _connectionString;

    public FanficCacheService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Postgres")!;
    }


    public async Task<FanficCacheEntry?> GetAsync(string url, string format)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"SELECT url, title, object_key, created_at, format
          FROM fanfic_cache
          WHERE url = @url AND format = @format",
            connection
        );

        cmd.Parameters.AddWithValue("url", url);
        cmd.Parameters.AddWithValue("format", format);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return new FanficCacheEntry
        {
            Url = reader.GetString(0),
            Title = reader.GetString(1),
            ObjectKey = reader.GetString(2),
            CreatedAt = reader.GetDateTime(3),
            Format = reader.GetString(4)
        };
    }
    public async Task SaveAsync(string url, string title, string objectKey, string format)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var cmd = new NpgsqlCommand(
            @"INSERT INTO fanfic_cache (url, title, object_key, created_at, format)
          VALUES (@url, @title, @objectKey, @createdAt, @format)
          ON CONFLICT (url, format) DO NOTHING",
            connection
        );

        cmd.Parameters.AddWithValue("url", url);
        cmd.Parameters.AddWithValue("title", title);
        cmd.Parameters.AddWithValue("objectKey", objectKey);
        cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("format", format);

        await cmd.ExecuteNonQueryAsync();
    }

    
}