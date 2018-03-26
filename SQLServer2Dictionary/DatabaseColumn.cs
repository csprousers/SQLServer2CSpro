using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLServer2Dictionary
{
    public class DatabaseColumn
    {
        public string Name { get; set; }
        public Type Type { get; set; }
        public int Length { get; set; }
    }
}
