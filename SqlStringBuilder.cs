using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.SqlLite
{
    public class SqlStringBuilder
    {
        public SqlStringBuilder()
        {
            this.Current = this;
        }
        public string Content { get; set; }

        public SqlStringBuilder Current { get; set; } = null;

        public SqlStringBuilder Previous { get; set; } = null;

        public SqlStringBuilder Next { get; set; } = null;

        public void Append(string content)
        {
            var next = new SqlStringBuilder()
            {
                Content = content
            };

            next.Previous = this.Current;

            this.Current.Next = next;

            this.Current = next;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            var current = this;
            while (current != null)
            {
                sb.Append(current.Content);
                current = current.Next;
            }

            return sb.ToString();
        }
    }
}
