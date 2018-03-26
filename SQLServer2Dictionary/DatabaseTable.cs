using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLServer2Dictionary
{
    public class DatabaseTable
    {
        public string Name { get; set; }

        public string Schema { get; set; }

        public string FullName { get { return String.IsNullOrEmpty(Schema) ? Name : Schema + "." + Name; } }

        public List<DatabaseColumn> Columns { get; set; }

    }
}
