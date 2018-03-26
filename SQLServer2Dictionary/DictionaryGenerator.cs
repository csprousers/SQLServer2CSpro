using CSPro.Dictionary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SQLServer2Dictionary
{
    class DictionaryGenerator
    {
        public DataDictionary CreateDictionary(string label, string databaseName, IEnumerable<LevelSpec> levelSpecs)
        {
            var dictionary = new DataDictionary();
            dictionary.Label = label;
            dictionary.Name = MakeName(label + "_DICT");
            dictionary.RecordTypeLength = 1;
            dictionary.RecordTypeStart = 1;
            dictionary.Note = String.Format("#database {0}", databaseName);


            List<DatabaseColumn> usedIdItems = new List<DatabaseColumn>();

            foreach (var levelSpec in levelSpecs)
            {
                CreateLevel(dictionary.AddLevel(), levelSpec, usedIdItems);
            }

            AdjustItemStartPositions(dictionary);

            return dictionary;
        }

        private void CreateLevel(DictionaryLevel dictLevel, LevelSpec levelSpec, List<DatabaseColumn> usedIdItems)
        {
            dictLevel.Label = levelSpec.Name;
            dictLevel.Name = MakeName(levelSpec.Name, "_LVL");

            // Add id-items to level
            var idColumns = levelSpec.Records.SelectMany(r => r.DatabaseTable.Columns).Where(c => levelSpec.IdItems.Contains(c)).Distinct();
            foreach (var idCol in idColumns)
            {
                string itemName = MakeName(idCol.Name, "_" + levelSpec.Name);
                CreateItem(dictLevel.IdItems.AddItem(), itemName, idCol);
            }
            usedIdItems.AddRange(idColumns);

            // Add variables to each record
            foreach (var recSpec in levelSpec.Records)
            {
                // Only add non-id items as record variables
                var regularColumns = recSpec.DatabaseTable.Columns.Where(c => usedIdItems.FirstOrDefault(id => id.Name == c.Name) == null);

                string recordName = MakeName(recSpec.Name, "_" + levelSpec.Name);
                CreateRecord(dictLevel.AddRecord(), recordName, recSpec, regularColumns);
            }
        }

        private void CreateRecord(DictionaryRecord dictRecord, string recordName, RecordSpec recSpec, IEnumerable<DatabaseColumn> dbColumns)
        {
            dictRecord.Label = recSpec.Name;
            dictRecord.Name = recordName;
            dictRecord.RecordType = recSpec.Type.ToString();
            dictRecord.Note = String.Format("#table {0}", recSpec.DatabaseTable.FullName);
            foreach (var dbCol in dbColumns)
            {
                string itemName = MakeName(dbCol.Name, "_" + dictRecord.Name);
                CreateItem(dictRecord.AddItem(), itemName, dbCol);
            }
        }

        private void CreateItem(DictionaryItem dictItem, string itemName, DatabaseColumn column)
        {
            dictItem.Label = column.Name;
            dictItem.Name = itemName;
            dictItem.Note = String.Format("#column {0}:{1}", column.Name, column.Type.ToString()); //TODO - sqltype

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

        private HashSet<string> usedNames = new HashSet<string>();

        private string MakeName(string label, string postfixForDuplicates = null)
        {
            string name = Regex.Replace(label.ToUpper(), @"[^A-Z0-9_]+", "_");
            const int maxNameLength = 32;
            if (name.Length > maxNameLength)
                name = name.Substring(0, maxNameLength);

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
