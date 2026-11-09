// Работа с базой данных SQL Server: подключение, запросы, хранимые процедуры, запуск скрипта.
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;

namespace PhotoCenterApp
{
    static class Db
    {
        public const string DatabaseName = "PhotoCenter";

        // Имя сервера SQL Server. Меняется в окне подключения при запуске.
        public static string Server = @".\SQLEXPRESS";

        public static string ConnectionString(string database)
        {
            return "Data Source=" + Server + ";Initial Catalog=" + database +
                   ";Integrated Security=True;Connect Timeout=5";
        }

        /// <summary>Параметр запроса. Пустая строка превращается в NULL.</summary>
        public static SqlParameter P(string name, object value)
        {
            string s = value as string;
            if (value == null || (s != null && s.Trim().Length == 0))
                return new SqlParameter(name, DBNull.Value);
            return new SqlParameter(name, s != null ? s.Trim() : value);
        }

        /// <summary>SELECT-запрос, результат — таблица.</summary>
        public static DataTable Query(string sql, params SqlParameter[] parameters)
        {
            using (var con = new SqlConnection(ConnectionString(DatabaseName)))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddRange(parameters);
                var table = new DataTable();
                new SqlDataAdapter(cmd).Fill(table);
                return table;
            }
        }

        /// <summary>Вызов хранимой процедуры. Если процедура что-то возвращает — это будет в таблице.</summary>
        public static DataTable Proc(string name, params SqlParameter[] parameters)
        {
            using (var con = new SqlConnection(ConnectionString(DatabaseName)))
            using (var cmd = new SqlCommand(name, con))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddRange(parameters);
                var table = new DataTable();
                new SqlDataAdapter(cmd).Fill(table);
                return table;
            }
        }

        /// <summary>Проверка, что сервер доступен. Возвращает текст ошибки или null.</summary>
        public static string CheckServer()
        {
            try
            {
                using (var con = new SqlConnection(ConnectionString("master")))
                    con.Open();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static bool DatabaseExists()
        {
            using (var con = new SqlConnection(ConnectionString("master")))
            using (var cmd = new SqlCommand("SELECT DB_ID(@n)", con))
            {
                cmd.Parameters.AddWithValue("@n", DatabaseName);
                con.Open();
                return cmd.ExecuteScalar() != DBNull.Value;
            }
        }

        /// <summary>Выполняет SQL-скрипт (блоки разделены строками GO). Старая база удаляется и создаётся заново.</summary>
        public static void RunScript(string path)
        {
            SqlConnection.ClearAllPools(); // закрываем свои соединения со старой базой
            string script = File.ReadAllText(path);
            string[] batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            using (var con = new SqlConnection(ConnectionString("master")))
            {
                con.Open();
                foreach (string batch in batches)
                {
                    if (batch.Trim().Length == 0) continue;
                    using (var cmd = new SqlCommand(batch, con))
                    {
                        cmd.CommandTimeout = 120;
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            SqlConnection.ClearAllPools();
        }

        /// <summary>Понятный текст ошибки SQL Server.</summary>
        public static string ErrorText(Exception ex)
        {
            var sql = ex as SqlException;
            if (sql == null) return ex.Message;
            switch (sql.Number)
            {
                case 2627:
                case 2601:
                    return "Такая запись уже есть (повторяется телефон или название).";
                case 547:
                    return "Данные не прошли проверку базы (например, цена или количество должны быть больше нуля).\n\n" + sql.Message;
                case 50000:
                    return sql.Message; // ошибка из RAISERROR в процедуре
                default:
                    return sql.Message;
            }
        }
    }
}
