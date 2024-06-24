using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.SqlLite
{
    public class PersistenException : Exception
    {
        public PersistenException(string message, object data, Exception ex) : base(message, ex) {
            this.Data = data;
        }
        public PersistenException(string message, Exception ex) : base(message, ex)
        {
           
        }
        public PersistenException(Exception ex) : base(ex.Message, ex)
        {

        }

        public PersistenException(string message) : base(message)
        {

        }
        public object Data { get; set; }
    }
}
