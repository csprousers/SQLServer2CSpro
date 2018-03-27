using CSPro.Dictionary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SQLServer2Dictionary
{
    /// <summary>
    /// Generate CSPro data dictionary from SQL Server database
    /// </summary>
    class DictionaryGenerator
    {
        /// <summary>
        /// Construct CSPro data dictionary from dictionary specs.
        /// </summary>
        /// <param name="label">Text to use for CSPro dictionary label</param>
        /// <param name="databaseName">Name of SQL server database (saved in dictionary notes)</param>
        /// <param name="levelSpecs">List of level specifications that contain mappings from SQL database columns to CSPro levels and records.</param>
        /// <param name="valueSetRetriever">Utility class to get value sets for generated dictionary items</param>
        /// <returns>CSPro data dictionary</returns>
        public DataDictionary CreateDictionary(string label, string databaseName, IEnumerable<LevelSpec> levelSpecs, ValueSetRetriever valueSetRetriever)
        {
            var dictionary = new DataDictionary();
            dictionary.Label = label;
            dictionary.Name = MakeName(label + "_DICT");
            dictionary.RecordTypeLength = 1;
            dictionary.RecordTypeStart = 1;
            dictionary.Note = String.Format("#database {0}", databaseName);

            // Keep track of id-items used at this and previous levels so that they are not repeated
            List<DatabaseColumn> usedIdItems = new List<DatabaseColumn>();

            foreach (var levelSpec in levelSpecs)
            {
                CreateLevel(dictionary.AddLevel(), levelSpec, usedIdItems, valueSetRetriever);
            }

            AdjustItemStartPositions(dictionary);

            return dictionary;
        }

        /// <summary>
        /// Fill in a CSPro DictionaryLevel from a LevelSpec
        /// </summary>
        /// <param name="dictLevel">Empty level from CSPro dictionary to fill in</param>
        /// <param name="levelSpec">Level specification that contains mapping from tables to records</param>
        /// <param name="usedIdItems">List of id-items included in previous levels so that they will not be repeated. Updated by this routine.</param>
        /// <param name="valueSetRetriever">Utility class to get value sets for generated dictionary items</param>
        private void CreateLevel(DictionaryLevel dictLevel, LevelSpec levelSpec, List<DatabaseColumn> usedIdItems, ValueSetRetriever valueSetRetriever)
        {
            dictLevel.Label = levelSpec.Name;
            dictLevel.Name = MakeName(levelSpec.Name, "_LVL");

            // Add id-items to level
            var idColumns = levelSpec.Records.SelectMany(r => r.DatabaseTable.Columns).Where(c => levelSpec.IdItems.Contains(c)).Distinct();
            foreach (var idCol in idColumns)
            {
                string itemName = MakeName(idCol.Name, "_" + levelSpec.Name);
                CreateItem(dictLevel.IdItems.AddItem(), itemName, idCol, valueSetRetriever);
            }
            usedIdItems.AddRange(idColumns);

            // Add variables to each record
            foreach (var recSpec in levelSpec.Records)
            {
                // Only add non-id items as record variables
                var regularColumns = recSpec.DatabaseTable.Columns.Where(c => usedIdItems.FirstOrDefault(id => id.Name == c.Name) == null);

                string recordName = MakeName(recSpec.Name, "_" + levelSpec.Name);
                CreateRecord(dictLevel.AddRecord(), recordName, recSpec, regularColumns, valueSetRetriever);
            }
        }

        /// <summary>
        /// Fill in CSPro dictionary record a record specification
        /// </summary>
        /// <param name="dictRecord">Empty CSPro DictionaryRecord to fill in</param>
        /// <param name="recordName">Name to use for record</param>
        /// <param name="recSpec">RecordSpecification for the record to be generated</param>
        /// <param name="dbColumns">List of database columns to include the generated record</param>
        /// <param name="valueSetRetriever">Utility class to get value sets for generated dictionary items</param>
        private void CreateRecord(DictionaryRecord dictRecord, string recordName, RecordSpec recSpec, IEnumerable<DatabaseColumn> dbColumns, ValueSetRetriever valueSetRetriever)
        {
            dictRecord.Label = recSpec.Name;
            dictRecord.Name = recordName;
            dictRecord.RecordType = recSpec.Type.ToString();
            dictRecord.Note = String.Format("#table {0}", recSpec.DatabaseTable.FullName);
            foreach (var dbCol in dbColumns)
            {
                string itemName = MakeName(dbCol.Name, "_" + dictRecord.Name);
                CreateItem(dictRecord.AddItem(), itemName, dbCol, valueSetRetriever);
            }
        }

        /// <summary>
        /// Fill in CSPro data dictionary item from DatabaseColumn
        /// </summary>
        /// <param name="dictItem">Empty CSPro dictionary item to fill in</param>
        /// <param name="itemName">Name to use for generated item</param>
        /// <param name="column">DatabaseColumn to map to this dictionary item</param>
        /// <param name="valueSetRetriever">Utility class to get value sets for generated dictionary items</param>
        private void CreateItem(DictionaryItem dictItem, string itemName, DatabaseColumn column,  ValueSetRetriever valueSetRetriever)
        {
            dictItem.Label = column.Name;
            dictItem.Name = itemName;
            dictItem.Note = String.Format("#column {0}:{1}", column.Name, column.Type.ToString()); //TODO - sqltype

            SetItemTypeAndLength(dictItem, column);

            valueSetRetriever.GetValueSet(dictItem);

        }

        /// <summary>
        /// Set type and length of CSPro dictionary item based on type and length of SQL database column
        /// </summary>
        /// <param name="dictItem">CSPro dictionary item</param>
        /// <param name="column">SQL Server database column</param>
        private static void SetItemTypeAndLength(DictionaryItem dictItem, DatabaseColumn column)
        {
            switch (Type.GetTypeCode(column.Type))
            {
                case TypeCode.Int16:
                    dictItem.DataType = DataType.Numeric;
                    dictItem.Length = 6; // 2^15 = 5 digits plus sign
                    break;
                case TypeCode.UInt16:
                    dictItem.DataType = DataType.Numeric;
                    dictItem.Length = 5; // 2^16 = 6 digits
                    break;
                case TypeCode.Int32:
                    dictItem.DataType = DataType.Numeric;
                    dictItem.Length = 11; // 2^31 = 10 digits plus sign
                    break;
                case TypeCode.UInt32:
                    dictItem.DataType = DataType.Numeric;
                    dictItem.Length = 10; // 2^32 = 10 digits
                    break;
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    dictItem.DataType = DataType.Numeric;
                    dictItem.Length = Rules.MaxLengthNumeric; // 2^64 is more than max allowed in CSPro
                    break;
                case TypeCode.Boolean:
                    dictItem.DataType = DataType.Numeric;
                    dictItem.Length = 1;
                    break;
                case TypeCode.Byte:
                    dictItem.DataType = DataType.Numeric;
                    dictItem.Length = 3; // 2^8 = 3 digits
                    break;
                case TypeCode.SByte:
                    dictItem.DataType = DataType.Numeric;
                    dictItem.Length = 4; // 2^8 = 3 digits plus sign
                    break;
                case TypeCode.Double:
                case TypeCode.Decimal:
                    dictItem.DataType = DataType.Numeric;
                    dictItem.Length = Rules.MaxLengthNumeric; // Unsure how many decimal places so use max length
                    dictItem.DecimalChar = true;
                    dictItem.DecimalPlaces = 2; // No way to know this so making a guess
                    break;
                case TypeCode.String:
                    dictItem.DataType = DataType.Alpha;
                    dictItem.Length = Math.Min(column.Length, Rules.MaxLengthAlpha);
                    break;
                case TypeCode.DateTime:
                    dictItem.DataType = DataType.Alpha;
                    dictItem.Length = 21; // mm/dd/yyyy HH:MM:SS A
                    break;
                default:
                    throw new Exception("Type " + column.Type.ToString() + " is not supported for " + column.Name);
            }
        }

        /// <summary>
        /// Modify the start positions of items in the newly constructed CSPro dictionary so that no items overlap
        /// </summary>
        /// <param name="dictionary">CSPro data dictionary</param>
        private static void AdjustItemStartPositions(DataDictionary dictionary)
        {
            int idStart = 2; // type d'enregistrement dans pos 1
            foreach (var level in dictionary.Levels)
            {
                foreach (var item in level.IdItems.Items)
                {
                    item.Start = idStart;
                    idStart += item.Length;
                }
            }

            foreach (var level in dictionary.Levels)
            {
                foreach (var record in level.Records)
                {
                    int itemStart = idStart;

                    foreach (var item in record.Items)
                    {
                        item.Start = itemStart;
                        itemStart += item.Length;
                    }

                    record.Length = itemStart - 1;
                }
            }
        }

        /// <summary>
        /// Keep track of names in used in generated dictionary so that no names are duplicated.
        /// </summary>
        private HashSet<string> usedNames = new HashSet<string>();

        /// <summary>
        /// Create a valid, unique name from a string.
        /// </summary>
        /// <remarks>
        /// A valid name in a CSPro dictionary consists of only capital letters, numbers and underscore characters.
        /// It can not be more than 32 characters. Names cannot be duplicated which means that even if items are
        /// in different records they must have different names.
        /// </remarks>
        /// <param name="label">Arbitrary text to convert to valid name</param>
        /// <param name="postfixForDuplicates">If the generated is already used in the dictionary add this string to the end to make it unique. If no postfix is given an exception is thrown on duplicates.</param>
        /// <returns>Valid CSPro name</returns>
        private string MakeName(string label, string postfixForDuplicates = null)
        {
            // Replace invalid characters by uppercase or underscore
            string name = Regex.Replace(label.ToUpper(), @"[^A-Z0-9_]+", "_");

            // Limit length to 32
            const int maxNameLength = 32;
            if (name.Length > maxNameLength)
                name = name.Substring(0, maxNameLength);

            // Check for duplicates
            if (usedNames.Contains(name))
            {
                if (postfixForDuplicates == null)
                {
                    throw new Exception("Name " + name + " is used for multiple records/levels/dictionaries. Names must be unique.");
                }
                else
                {
                    if (name.Length > maxNameLength - postfixForDuplicates.Length)
                        name = name.Substring(0, maxNameLength - postfixForDuplicates.Length);
                    name += postfixForDuplicates;
                }
            }

            usedNames.Add(name);

            return name;
        }

    }
}
