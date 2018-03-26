/* Convert data from SQL Server Database to a CSPro data file.
 * 
 * Takes a CSPro data dictionary which describes the data and extracts
 * data from tables in the SQL Server database that correspond to records
 * in the CSPro dictionary. These records are written to stdout in CSPro
 * text file format.
 * 
 * Records in the CSPro dictionary are matched to tables in the SQL Server
 * database based on the record label in the dictionary. In other words the
 * label (not name) of the record in the CSPro dictionary must match the
 * name of the table in the database. In cases where all tables all have
 * a common prefix like tbl_ it is possible to supply that prefix when
 * matching records to table (see tablePrefix in the usage).
 * 
 * Similarly variables in the CSPro dictionary are matched to columns in the
 * database by taking the item label (not name) from the CSPro data item
 * and finding a column with a matching name in the database table. Columns
 * that do not match an item in the CSPro dictionary are ignored.
 * 
 * Typical usage:
 * 
 *      SQLServer2CSPro --dictionary Popstan.dcf -connection "Data Source=(local);Initial Catalog=popstan_db;Integrated Security=true"
 * 
 */

using CSPro.Dictionary;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SQLServer2CSPro
{
    /// <summary>
    /// Command line arguments
    /// </summary>
    public class ApplicationArguments
    {
        public string Dictionary { get; set; }
        public string ConnectionString { get; set; }
        public string TablePrefix { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var parsedArgs = ParseCommandLine(args);
            if (parsedArgs == null)
                return;

            DataDictionary dictionary;
            try
            {
                dictionary = new DataDictionary(parsedArgs.Dictionary);
            } catch (Exception)
            {
                Console.Error.WriteLine("Failed to open dictionary " + parsedArgs.Dictionary);
                return;
            }

            try
            {
                var readers = CreateReaders(dictionary, parsedArgs.ConnectionString, parsedArgs.TablePrefix);
                while (!readers.All(r => r.AtEnd))
                {
                    var nextCase = GetNextCase(readers);
                    var orderedRecords = OrderRecordsInCase(nextCase);
                    WriteRecords(orderedRecords, dictionary);
                }
            } catch (Exception e)
            {
                Console.Error.WriteLine("Error: " + e.Message);
            }
        }

        /// <summary>
        /// Parse command line arguments into ApplicationArguments and print any errors to stderr
        /// </summary>
        /// <param name="args">List of command line arguments from main</param>
        /// <returns>Parsed arguments or null if encountered error</returns>
        private static ApplicationArguments ParseCommandLine(string[] args)
        {
            const string usage =
    @"Usage: SQLServer2CSPro [-h] -d=<dictionary> -c=<connection-string> [-t=<table_prefix>]

-d --dictionary     CSPro data dictionary that describes data file format
-c --connection     connection string for SQL server database containing data
-t --tablePrefix    text prefixed to record label to generate database table name
-h --help           show this message

";

            ApplicationArguments parsedArgs = new ApplicationArguments();
            bool expectingDict = false;
            bool expectingConn = false;
            bool expectingTbl = false;

            foreach (var arg in args)
            {
                if (expectingDict)
                {
                    parsedArgs.Dictionary = arg;
                    expectingDict = false;
                }
                else if (expectingConn)
                {
                    parsedArgs.ConnectionString = arg;
                    expectingConn = false;
                }
                else if (expectingTbl)
                {
                    parsedArgs.TablePrefix = arg;
                    expectingTbl = false;
                }
                else
                {
                    switch (arg)
                    {
                        case "-h":
                        case "--help":
                        case "?":
                            Console.Error.WriteLine(usage);
                            return null;
                        case "-d":
                        case "--dictionary":
                            expectingDict = true;
                            break;
                        case "-c":
                        case "--connection":
                            expectingConn = true;
                            break;
                        case "-t":
                        case "--tablePrefix":
                            expectingTbl = true;
                            break;
                        default:
                            Console.Error.WriteLine("Error: unknown option " + arg);
                            Console.Error.WriteLine(usage);
                            return null;
                    }
                }
            }

            if (parsedArgs.Dictionary == null)
            {
                Console.Error.WriteLine("Missing required argument: dictionary");
                Console.Error.WriteLine(usage);
                return null;
            }

            if (parsedArgs.ConnectionString == null)
            {
                Console.Error.WriteLine("Missing required argument: connection");
                Console.Error.WriteLine(usage);
                return null;
            }

            return parsedArgs;
        }

        /// <summary>
        /// Create a data reader to retreive records from a table in SQL database for each record type in the CSPro dictionary
        /// </summary>
        /// <param name="dictionary">CSPro data dictionary</param>
        /// <param name="connectionString">SQL server database connection string</param>
        /// <param name="tablePrefix">Prefix added to CSPro record name to get SQL table name</param>
        /// <returns>List of readers, one for each record type in CSPro dictionary</returns>
        private static List<RecordReader> CreateReaders(DataDictionary dictionary, string connectionString, string tablePrefix)
        {
            List<RecordReader> readers = new List<RecordReader>();

            int recordNumber = 0;
            int levelNumber = 0;
            foreach (var level in dictionary.Levels)
            {
                foreach (var record in level.Records)
                {
                    var recordInfo = new RecordInfo
                    {
                        DictionaryOrder = recordNumber,
                        Level = level,
                        Record = record,
                        LevelNumber = levelNumber
                    };

                    var reader = new RecordReader(connectionString, tablePrefix + record.Label,
                        recordInfo, dictionary);

                    // Move reader to first record so that it has valid Current
                    reader.MoveNext();

                    readers.Add(reader);

                    ++recordNumber;
                }

                ++levelNumber;
            }

            return readers;
        }

        /// <summary>
        /// Extracts all records for the next case to write to the data file where a case are all the records that have the same first level ids.
        /// Readers must read records ordered by first level (case) ids.
        /// </summary>
        /// <param name="recordReaders">List of readers for different records types from SQL database</param>
        /// <returns>List of records all belonging to same case</returns>
        private static List<RecordData> GetNextCase(IEnumerable<RecordReader> recordReaders)
        {
            List<RecordData> nextCase = new List<RecordData>();

            // Get smallest caseid of all current records
            var nextCaseId = recordReaders.Where(r => !r.AtEnd).Select(r => r.Current.LevelIds[0]).Min();

            // Get all records that match this case id
            foreach (var reader in recordReaders)
            {
                while (!reader.AtEnd && reader.Current.LevelIds[0] == nextCaseId)
                {
                    nextCase.Add(reader.Current);
                    reader.MoveNext();
                }
            }
            return nextCase;
        }

        /// <summary>
        /// Comparator that sorts records inside a case into correct order
        /// for a CSPro data file. Lower level records must be nested
        /// inside parent level nodes i.e.:
        ///     First level records in dictionary order
        ///     Second level records for first second level node
        ///     Third level records for first third level node in first second level node
        ///     etc...
        /// </summary>
        private class CompareRecordsCaseOrder : IComparer<RecordData>
        {
            public int Compare(RecordData a, RecordData b)
            {
                for (int lvlNum = 0; lvlNum < a.LevelIds.Length; ++lvlNum)
                {
                    // Sort based on ids for current level
                    int idCompare = String.Compare(a.LevelIds[lvlNum], b.LevelIds[lvlNum]);
                    if (idCompare != 0)
                        return idCompare;

                    // Same ids at current level, records at this level go before records at lower level
                    if (a.RecordInfo.LevelNumber <= lvlNum && b.RecordInfo.LevelNumber > lvlNum)
                        return -1;
                    else if (b.RecordInfo.LevelNumber <= lvlNum && b.RecordInfo.LevelNumber > lvlNum)
                        return 1;
                    else if (a.RecordInfo.LevelNumber == lvlNum && b.RecordInfo.LevelNumber == lvlNum)
                    {
                        // Both at current level with same id, use dict order
                        if (a.RecordInfo.DictionaryOrder < b.RecordInfo.DictionaryOrder)
                            return -1;
                        else if (a.RecordInfo.DictionaryOrder > b.RecordInfo.DictionaryOrder)
                            return 1;
                        else
                        {
                            // Same dict order, use occurrence
                            if (a.RecordOccurrenceNumber < b.RecordOccurrenceNumber)
                                return -1;
                            else if (a.RecordOccurrenceNumber > b.RecordOccurrenceNumber)
                                return 1;
                        }
                    }

                    // Continue loop to check next level ids
                }

                return 0;
            }
        }

        /// <summary>
        /// Order records in a case in correct order for a CSPro data file
        /// </summary>
        /// <param name="records"></param>
        /// <returns></returns>
        private static IEnumerable<RecordData> OrderRecordsInCase(List<RecordData> records)
        {
            return records.OrderBy(r => r, new CompareRecordsCaseOrder());
        }

        /// <summary>
        /// Write a list of record data to stdout in CSPro data file format
        /// </summary>
        /// <param name="records">List of record data to write</param>
        /// <param name="dictionary">CSPro data dictionary</param>
        private static void WriteRecords(IEnumerable<RecordData> records, DataDictionary dictionary)
        {
            foreach (var record in records)
            {
                char[] line = CreateLineBuffer(record.RecordInfo.Record);
                CopyRecordType(line, dictionary, record.RecordInfo.Record);

                // Id items for all levels
                foreach (var level in dictionary.Levels)
                {
                    CopyItems(line, level.IdItems.Items, record.Data);
                }

                // Variables from record
                CopyItems(line, record.RecordInfo.Record.Items, record.Data);

                Console.WriteLine("{0}", new String(line));
            }
        }

        /// <summary>
        /// Create a character array with capacity to hold all data items in record
        /// </summary>
        /// <param name="record">CSPro dictionary record</param>
        /// <returns>Character array long enough for the record</returns>
        private static char[] CreateLineBuffer(DictionaryRecord record)
        {
            return Enumerable.Repeat(' ', record.Length).ToArray();
        }

        /// <summary>
        /// Insert record type in appropriate location in buffer
        /// </summary>
        /// <param name="lineBuffer">Buffer in which to write record type</param>
        /// <param name="dictionary">CSPro dictionary</param>
        /// <param name="record">CSPro dictionary record</param>
        static void CopyRecordType(char[] lineBuffer, DataDictionary dictionary, DictionaryRecord record)
        {
            record.RecordType.CopyTo(0, lineBuffer, dictionary.RecordTypeStart - 1, dictionary.RecordTypeLength);
        }

        /// <summary>
        /// Copy data into character array in CSPro data file format
        /// </summary>
        /// <param name="line">Buffer to write values to</param>
        /// <param name="items">Dictionary items to write</param>
        /// <param name="data">Values to write where keys are labels of items</param>
        private static void CopyItems(char[] line, DictionaryItem[] items, Dictionary<string, string> data)
        {
            foreach (var item in items)
            {
                if (data.ContainsKey(item.Label))
                {
                    data[item.Label].CopyTo(0, line, item.Start - 1, item.Length);
                }
            }
        }
    }
}
