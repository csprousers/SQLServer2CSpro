using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSPro.Dictionary;
using System.Data;
using System.Collections;

namespace SQLServer2CSPro
{
    /// <summary>
    /// Read consecutive readers from a database table as RecordData
    /// </summary>
    class RecordReader : IDisposable
    {
        private readonly SqlDataReader reader;
        private readonly SqlConnection connection;
        private readonly RecordInfo recordInfo;
        private readonly DataDictionary dictionary;
        private UInt64 occurrrence = 0;

        private struct ItemMapping
        {
            public DictionaryItem item;
            public int columnIndex;
        }

        private readonly ItemMapping[] itemToColumnMap;

        /// <summary>
        /// Construct record reader
        /// </summary>
        /// <param name="connectionString">Connection string for SQL Server database</param>
        /// <param name="tableName">Name of table in SQL Server database to read records from</param>
        /// <param name="recordInfo">Parameters of record in CSPro dictionary corresponding to the database table</param>
        /// <param name="dictionary">CSPro data dictionary containing the record</param>
        public RecordReader(string connectionString, 
            string tableName,
            RecordInfo recordInfo,
            DataDictionary dictionary)
        {
            this.recordInfo = recordInfo;
            this.dictionary = dictionary;

            connection = new SqlConnection(connectionString);
            connection.Open();

            var allLevelIds = dictionary.Levels.SelectMany(l => l.IdItems.Items);

            var columns = GetTableColumns(tableName, connection);
            itemToColumnMap = allLevelIds.Concat(recordInfo.Record.Items).
                Select(i => new ItemMapping { item = i, columnIndex = columns.IndexOf(i.Label) }).
                ToArray();

            var previousAndCurrentLevelIds = dictionary.Levels.Where((l, i) => i <= recordInfo.LevelNumber).SelectMany(l => l.IdItems.Items);
            var idsInTable = previousAndCurrentLevelIds.Select(i => i.Label).Intersect(columns);

            SqlCommand cmd =
                new SqlCommand("SELECT * FROM " + tableName + " ORDER BY " +
                                String.Join(",", idsInTable),
                                connection);

            reader = cmd.ExecuteReader();
        }

        public void Dispose()
        {
            reader.Dispose();
            connection.Dispose();
        }

        public RecordData Current { get; private set; }

        public bool AtEnd { get; private set; }

        public bool MoveNext()
        {
            if (!reader.Read())
            {
                AtEnd = true;
                return false;
            }

            Dictionary<string, string> values = new Dictionary<string, string>();
            foreach (var itemMapping in itemToColumnMap)
            {
                if (itemMapping.columnIndex != -1)
                {
                    var val = reader[itemMapping.columnIndex];

                    // Make sure data length matches length in CSPro dictionarys
                    values[itemMapping.item.Label] = String.Format("{0," + itemMapping.item.Length + "}", val);
                }
                else
                {
                    // Not in database table so leave blank
                    values[itemMapping.item.Label] = new string(' ', itemMapping.item.Length);
                }
            }

            Current = new RecordData
            {
                Data = values,
                LevelIds = dictionary.Levels.Select(l => String.Join("", l.IdItems.Items.Select(i => values[i.Label]))).ToArray(),
                RecordOccurrenceNumber = occurrrence++,
                RecordInfo = recordInfo
            };

            return true;
        }

        /// <summary>
        /// Get list of columns from a database table
        /// </summary>
        /// <param name="table">Name of database table in SQL Server database</param>
        /// <param name="connection">Open database connection</param>
        /// <returns>List of names of table columns</returns>
        private static List<string> GetTableColumns(string table, SqlConnection connection)
        {
            var columns = new List<string>();

            using (var cmd = new SqlCommand("SELECT * FROM " + table, connection))
            {
                using (var reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
                {
                    var schemaTable = reader.GetSchemaTable();

                    foreach (DataRow field in schemaTable.Rows)
                    {
                        columns.Add((string)field["ColumnName"]);
                    }
                }
            }

            return columns;
        }
    }
}
