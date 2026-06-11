namespace Explorador_de_Archivo
{
    /// <summary>
    /// Configuración de las bases de datos soportadas: SQLite (local) y SQL Server.
    /// Para activar SQL Server, llena los campos Host, User y Password.
    /// </summary>
    public static class DatabaseConfig
    {
        public static class SqlServer
        {
            public const string Host     = "";          // ej "localhost\\SQLEXPRESS"
            public const string Port     = "1433";
            public const string Database = "ExploradorDB";
            public const string User     = "";
            public const string Password = "";

            public static bool IsConfigured =>
                !string.IsNullOrEmpty(Host) &&
                !string.IsNullOrEmpty(User);

            /// <summary>
            /// Cadena de conexión para Microsoft.Data.SqlClient.
            /// Formato: Server=HOST,PORT;Database=DB;User Id=USER;Password=PASS;TrustServerCertificate=True;
            /// </summary>
            public static string ConnectionString =>
                $"Server={Host},{Port};Database={Database};User Id={User};Password={Password};TrustServerCertificate=True;";
        }
    }
}
