using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace Final.Data
{
    public static class DbHelper
    {
        private static readonly string ConnectionString =
            ConfigurationManager.ConnectionStrings["ConcordDB"].ConnectionString;

        public static DataRow QuerySingleRow(string sql, params SqlParameter[] parameters)
        {
            DataTable table = QueryTable(sql, parameters);
            return table.Rows.Count > 0 ? table.Rows[0] : null;
        }

        public static DataTable QueryTable(string sql, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null && parameters.Length > 0)
                    command.Parameters.AddRange(parameters);

                var table = new DataTable();
                using (var adapter = new SqlDataAdapter(command))
                {
                    adapter.Fill(table);
                }
                return table;
            }
        }

        public static int Execute(string sql, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null && parameters.Length > 0)
                    command.Parameters.AddRange(parameters);

                connection.Open();
                return command.ExecuteNonQuery();
            }
        }

        public static object ExecuteScalar(string sql, params SqlParameter[] parameters)
        {
            using (var connection = new SqlConnection(ConnectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null && parameters.Length > 0)
                    command.Parameters.AddRange(parameters);

                connection.Open();
                return command.ExecuteScalar();
            }
        }
    }
}
