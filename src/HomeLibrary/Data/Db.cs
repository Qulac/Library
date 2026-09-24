using Microsoft.Data.SqlClient;

namespace HomeLibrary.Data;

/// <summary>
/// Строка подключения к SQL Server (docker-compose, см. docker-compose.yml).
/// Можно переопределить переменной окружения HOME_LIBRARY_DB.
/// </summary>
public static class Db
{
    public const string DefaultConnectionString =
        "Server=localhost,1433;Database=HomeLibrary;User Id=sa;Password=YourStrong!Pass1;" +
        "TrustServerCertificate=True;Encrypt=False";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("HOME_LIBRARY_DB") ?? DefaultConnectionString;

    /// <summary>Открывает и возвращает новое подключение к SQL Server.</summary>
    public static SqlConnection OpenConnection()
    {
        var conn = new SqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }
}
