using System;
using System.Collections.Generic;
using System.Text;

namespace Persistence.SqlLite
{
    public class ColumnInfo
    {
        public long cid { get; set; }
        
        public string name { get; set; }

        public string type { get; set; }
    }
}
