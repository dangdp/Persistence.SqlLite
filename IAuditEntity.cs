using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.SqlLite
{
    public interface IAuditEntity
    {
        bool IsDeleted { get; set; }
    }
}
