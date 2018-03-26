using CSPro.Dictionary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLServer2CSPro
{
    class RecordData
    {
        public Dictionary<string, string> Data { get; set; }

        public string[] LevelIds { get; set; }

        public UInt64 RecordOccurrenceNumber { get; set; }

        public RecordInfo RecordInfo { get; set; }
    }
}
