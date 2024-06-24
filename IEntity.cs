using System;
using System.Collections.Generic;
using System.Text;

namespace Persistence.SqlLite
{
    public interface IEntity
    {
        Guid Id { get; set; }
    }
}
