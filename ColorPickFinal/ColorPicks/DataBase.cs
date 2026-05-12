using System;
using MySql.Data.MySqlClient;  // or MySqlConnector

namespace ColorPicks
{
    public static class DatabaseHelper
    {
        private const string ConnectionString =
            "Server=localhost;Port=3306;Database=colorpicks_db;Uid=root;Pwd=;";

        public static MySqlConnection GetConnection()
        {
            var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        // Example: save a picked color to the DB
        public static void SaveColor(string hex, string rgb, string hsl)
        {
            using var conn = GetConnection();
            using var cmd = new MySqlCommand(
                "INSERT INTO color_history (hex, rgb, hsl, picked_at) VALUES (@hex, @rgb, @hsl, NOW())", conn);
            cmd.Parameters.AddWithValue("@hex", hex);
            cmd.Parameters.AddWithValue("@rgb", rgb);
            cmd.Parameters.AddWithValue("@hsl", hsl);
            cmd.ExecuteNonQuery();
        }
    }
}