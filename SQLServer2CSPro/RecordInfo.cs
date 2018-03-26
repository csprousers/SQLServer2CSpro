using CSPro.Dictionary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLServer2CSPro
{
    class RecordInfo
    {
        public DictionaryRecord Record { get; set; }

        public int DictionaryOrder { get; set; }

        public DictionaryLevel Level { get; set; }

        public int LevelNumber { get; set; }
    }
}
