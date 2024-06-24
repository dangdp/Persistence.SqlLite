using System;


namespace Persistence.SqlLite
{
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ConfigurationAttribute : Attribute
    {
        public string TableName { get; set; }

        public string PrimaryColumn { get; set; } = "Id";

        public bool IsAutoIncrement { get; private set; } = false;
    }
}
