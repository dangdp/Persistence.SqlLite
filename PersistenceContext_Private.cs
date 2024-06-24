using Microsoft.Data.Sqlite;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Persistence.SqlLite
{
    public partial class PersistenceContext : IDisposable
    {
        public class VariableModel
        {
            public string VariableName { get; set; }

            public string VariableType { get; set; }

            public SqliteType SqliteType { get { return (SqliteType)Enum.Parse(typeof(SqliteType), VariableType); } }
        }

        public class ColumnInformationModel
        {
            public string ColumnName { get; set; }

            public string DataType { get; set; }

            public int? Length { get; set; }

            public Byte? Precision { get; set; }

            public int? Scale { get; set; }
        }

        public static string ConnectionString { get; set; }
        private SqliteConnection iConnection = null;

        public PersistenceContext(SqliteConnection iConnection)
        {
            this.iConnection = iConnection;
            this.Initialize();
        }
        protected SqliteConnection connection
        {
            get
            {
                if (iConnection == null)
                {
                    throw new Exception("Connection is null");

                }

                while (iConnection.State == ConnectionState.Executing || iConnection.State == ConnectionState.Fetching || iConnection.State == ConnectionState.Connecting)
                {

                }
                if (iConnection.State == ConnectionState.Closed)
                {
                    iConnection.Open();
                }

                if (this.CommandTimeout < 0)
                {
                    this.CommandTimeout = iConnection.ConnectionTimeout;
                }

                return iConnection;
            }
        }



        private SqliteTransaction transaction = null;

        public SqliteTransaction BeginTransaction()
        {
            transaction = this.connection.BeginTransaction();

            return transaction;
        }

        public void Commit()
        {
            if (transaction != null)
            {
                transaction.Commit();
                transaction.Dispose();
            }
            transaction = null;
        }

        public void Rollback()
        {
            if (transaction != null)
            {
                transaction.Rollback();
            }
            transaction = null;
        }

        protected IDataReader reader = null;
        public void Dispose()
        {
            if (iConnection != null && iConnection.State != ConnectionState.Closed)
            {
                try
                {
                    iConnection.Dispose();
                }
                catch (Exception ex)
                {
                   
                }
            }
        }

        protected ConnectionState GetState()
        {
            return this.connection.State;
        }

        private static ConcurrentDictionary<string, string> MappingVariableTypeOfStoreProcedures = new ConcurrentDictionary<string, string>();

        private static object IsLocked = new object();
        private static ConcurrentDictionary<string, string> MappingValidTypes = new ConcurrentDictionary<string, string>()
        {

        };

        private static Dictionary<string, Dictionary<string, string>> MappingColumnTypes = new Dictionary<string, Dictionary<string, string>>();


        private string GetColumnSqlType(string columnName, string tableName)
        {
            if (!MappingColumnTypes.ContainsKey(tableName))
            {
                lock (IsLocked)
                {
                    if (MappingColumnTypes.ContainsKey(tableName))
                    {
                        if (MappingColumnTypes[tableName].ContainsKey(columnName))
                        {
                            return MappingColumnTypes[tableName][columnName];
                        }
                        throw new Exception($"Not Found Column Name: {columnName} in Table Name: {tableName}");
                    }
                    var query = $@"
                    SELECT t.COLUMN_NAME as [ColumnName], 
                    t.DATA_TYPE as [DataType], 
                    t.CHARACTER_MAXIMUM_LENGTH as [Length], 
                    t.NUMERIC_PRECISION as [Precision], 
                    t.NUMERIC_SCALE as [Scale],
                    t.TABLE_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS t
                    WHERE 
                         TABLE_NAME = '{tableName}'
                    ";
                    var columns = this.Query<ColumnInformationModel>(query, commandType: CommandType.Text);

                    MappingColumnTypes.Add(tableName, new Dictionary<string, string>());

                    foreach (var column in columns)
                    {
                        if (column.DataType == "decimal")
                        {
                            MappingColumnTypes[tableName].Add(column.ColumnName, $"{column.DataType.ToUpper()} ({column.Precision},{column.Scale})");
                            continue;
                        }

                        if (column.Length.HasValue)
                        {
                            MappingColumnTypes[tableName].Add(column.ColumnName, $"{column.DataType.ToUpper()} ({column.Length})");
                            continue;
                        }

                        MappingColumnTypes[tableName].Add(column.ColumnName, column.DataType.ToUpper());
                    }
                }
            }

            if (MappingColumnTypes[tableName].ContainsKey(columnName))
            {
                return MappingColumnTypes[tableName][columnName];
            }

            throw new Exception($"Not Found Column Name: {columnName} in Table Name: {tableName}");
        }

        internal static ConcurrentDictionary<string, string> MappingValueTypes = new ConcurrentDictionary<string, string>()
        {

        };

        private void Initialize()
        {
            if (MappingValidTypes.Count == 0)
            {
                lock (IsLocked)
                {
                    if (MappingValidTypes.Count == 0)
                    {

                        MappingValidTypes.TryAdd("System.String", "TEXT");
                        MappingValidTypes.TryAdd("System.DateTime", "INTEGER");
                        MappingValidTypes.TryAdd("System.Boolean", "INTEGER");
                        MappingValidTypes.TryAdd("System.Int", "INTEGER");
                        MappingValidTypes.TryAdd("System.Integer", "INTEGER");
                        MappingValidTypes.TryAdd("System.Byte", "INTEGER");
                        MappingValidTypes.TryAdd("System.SByte", "INTEGER");
                        MappingValidTypes.TryAdd("System.UInt16", "INTEGER");
                        MappingValidTypes.TryAdd("System.UInt32", "INTEGER");
                        MappingValidTypes.TryAdd("System.UInt64", "INTEGER");
                        MappingValidTypes.TryAdd("System.Int16", "INTEGER");
                        MappingValidTypes.TryAdd("System.Int32", "INTEGER");
                        MappingValidTypes.TryAdd("System.Int64", "INTEGER");
                        MappingValidTypes.TryAdd("System.Decimal", "REAL");
                        MappingValidTypes.TryAdd("System.Double", "REAL");
                        MappingValidTypes.TryAdd("System.Single", "INTEGER");
                        MappingValidTypes.TryAdd("System.Long", "INTEGER");
                        MappingValidTypes.TryAdd("System.Guid", "TEXT");
                    }
                }
            }
            //this.WaitAnotherConnectionFinish();
        }
        private string LastConvertColumn = null;
        private IEnumerable<T> GetDataFromReader<T>(SqliteDataReader reader)
        {
            try
            {
                var result = new List<T>();
                if (typeof(object) == typeof(T))
                {
                    while (reader.Read())
                    {
                        var dynamicObject = new ExpandoObject() as IDictionary<string, Object>;
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var value = reader[i];
                            var name = reader.GetName(i);
                            dynamicObject.Add(name, value);
                        }

                        result.Add((T)dynamicObject);
                    }
                    return result;
                }

                var mappingProperties = typeof(T).GetProperties().Where(m => m.CanWrite).ToDictionary(m => m.Name);

                var columns = new List<string>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                var type = typeof(T);
                var nullableType = Nullable.GetUnderlyingType(type);
                var originalType = nullableType ?? type;
                if (MappingValidTypes.ContainsKey(originalType.FullName))
                {
                    while (reader.Read())
                    {
                        var value = reader[columns[0]];
                        if (value != DBNull.Value)
                        {
                            result.Add((T)Convert.ChangeType(value, originalType));
                        } else
                        {
                            result.Add(default(T));
                        }
                           
                    }
                    return result;
                }

                while (reader.Read())
                {
                    var instance = Activator.CreateInstance<T>();
                    foreach (var name in columns)
                    {
                        LastConvertColumn = name;
                        if (mappingProperties.ContainsKey(name))
                        {
                            var property = mappingProperties[name];
                            var value = reader[property.Name];
                            if (value != DBNull.Value)
                            {
                                property.SetValue(instance, value);
                            }
                            else
                            {
                                property.SetValue(instance, null);
                            }
                        }

                    }
                    result.Add(instance);
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new PersistenException(typeof(T).FullName + " - Please check this column: " + LastConvertColumn, ex);
            }
        }

        private T GetSingleRecordFromDataReader<T>(SqliteDataReader reader)
        {
            try
            {
                var result = default(T);
                if (typeof(object) == typeof(T))
                {
                    while (reader.Read())
                    {
                        var dynamicObject = new ExpandoObject() as IDictionary<string, Object>;
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var value = reader[i];
                            var name = reader.GetName(i);
                            dynamicObject.Add(name, value);
                        }

                        result = (T)dynamicObject;
                    }

                    return result;
                }

                var mappingProperties = typeof(T).GetProperties().Where(m => m.CanWrite).ToDictionary(m => m.Name);

                var columns = new List<string>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                var type = typeof(T);
                var nullableType = Nullable.GetUnderlyingType(type);
                var originalType = nullableType ?? type;
                if (MappingValidTypes.ContainsKey(originalType.FullName))
                {
                    while (reader.Read())
                    {
                        var value = reader[columns[0]];
                        result = (T)Convert.ChangeType(value, originalType);
                    }
                    return result;
                }

                reader.Read();
                foreach (var name in columns)
                {
                    LastConvertColumn = name;
                    if (mappingProperties.ContainsKey(name))
                    {
                        var property = mappingProperties[name];
                        var value = reader[property.Name];
                        if (value != DBNull.Value)
                        {
                            property.SetValue(result, value);
                        }
                        else
                        {
                            property.SetValue(result, null);
                        }
                    }

                }
                return result;
            }
            catch (Exception ex)
            {
                throw new PersistenException(typeof(T).FullName + " - Please check this column: " + LastConvertColumn, ex);
            }
        }

        private string GetColumnValueAsSql(object value, Type propertyType)
        {
            var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (!MappingValidTypes.ContainsKey(type.FullName) || !MappingValueTypes.ContainsKey(type.FullName))
            {
                if (value == null || value == DBNull.Value)
                {
                    return "NULL";
                }
                return "'" + value.ToString().PreventSqlInjection() + "'";
            }

            var originSqlType = MappingValueTypes[type.FullName];

            if (originSqlType == "STRING")
            {
                if (value == null || value == DBNull.Value)
                {
                    return "NULL";
                }
                return "'" + value.ToString().PreventSqlInjection() + "'";
            }

            if (originSqlType == "NUMBER")
            {
                if (value == null || value == DBNull.Value)
                {
                    return "NULL";
                }
                return value.ToString();
            }

            if (originSqlType == "BIT")
            {
                if (value == null || value == DBNull.Value)
                {
                    return "NULL";
                }
                return Convert.ToInt32(value).ToString();
            }

            if (originSqlType == "TIME")
            {
                if (value == null || value == DBNull.Value)
                {
                    return "NULL";
                }
                return "'" + value + "'";
            }

            if (originSqlType == "DATETIME")
            {
                if (value == null || value == DBNull.Value)
                {
                    return "NULL";
                }
                return "'" + Convert.ToDateTime(value).ToString("yyyy-MM-dd HH:mm:ss.fff") + "'";
            }

            if (originSqlType == "UNIQUEIDENTIFIER")
            {
                if (value == null || value == DBNull.Value)
                {
                    return "NULL";
                }
                return "'" + value + "'";
            }

            if (value == null || value == DBNull.Value)
            {
                return "NULL";
            }
            return "'" + value.ToString().PreventSqlInjection() + "'";
        }

        private string GetPropertyType(string storeProcedure, string variableName)
        {
            var referenceName = storeProcedure + "_" + variableName;

            if (MappingVariableTypeOfStoreProcedures.ContainsKey(storeProcedure) && !MappingVariableTypeOfStoreProcedures.ContainsKey(referenceName))
            {
                return null;
            }

            if (MappingVariableTypeOfStoreProcedures.ContainsKey(referenceName))
            {
                return MappingVariableTypeOfStoreProcedures[referenceName];
            }
            else
            {
                lock (IsLocked)
                {
                    if (!MappingVariableTypeOfStoreProcedures.ContainsKey(storeProcedure))
                    {
                        var sqlQueryType = $@"
                        SELECT p.name as VariableName, t.name as VariableType
                        FROM sys.parameters p
                        inner join sys.types t on p.system_type_id = t.system_type_id AND p.user_type_id = t.user_type_id
                        WHERE object_id = OBJECT_ID('{storeProcedure}')
                    ";

                        var data = this.Query<VariableModel>(sqlQueryType, commandType: CommandType.Text).ToList();
                        if (data.Count == 0)
                        {
                            var isExisted = this.Query<int?>("IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND OBJECT_ID = OBJECT_ID('" + storeProcedure + "')) select 1 ", commandType: CommandType.Text).FirstOrDefault();
                            if (isExisted != 1)
                            {
                                throw new Exception("Store Procedure: " + storeProcedure + " does not exit");
                            }
                        }
                        
                        MappingVariableTypeOfStoreProcedures.TryAdd(storeProcedure, null);

                        foreach (var item in data)
                        {
                            var tmpRefererenceName = storeProcedure + "_" + item.VariableName.Substring(1);
                            if (!MappingVariableTypeOfStoreProcedures.ContainsKey(tmpRefererenceName))
                            {
                                MappingVariableTypeOfStoreProcedures.TryAdd(tmpRefererenceName, (string)item.VariableType);
                            }
                        }
                    }
                }

                if (!MappingVariableTypeOfStoreProcedures.ContainsKey(referenceName))
                {
                    return null;
                }

                return MappingVariableTypeOfStoreProcedures[referenceName];
            }
        }

        private VariableModel GetVariable(string storeProcedure, string variableName)
        {
            var referenceName = storeProcedure + "_" + variableName;

            if (MappingVariableTypeOfStoreProcedures.ContainsKey(storeProcedure) && !MappingVariableTypeOfStoreProcedures.ContainsKey(referenceName))
            {
                return null;
            }

            if (MappingVariableTypeOfStoreProcedures.ContainsKey(referenceName))
            {
                var variableType = MappingVariableTypeOfStoreProcedures[referenceName];
                if (variableType == null)
                {
                    return null;
                }
                return new VariableModel()
                {
                    VariableName = variableName,
                    VariableType = variableType
                };
            }
            else
            {
                lock (IsLocked)
                {
                    if (!MappingVariableTypeOfStoreProcedures.ContainsKey(storeProcedure))
                    {
                        var sqlQueryType = $@"
                        SELECT p.name as VariableName, t.name as VariableType
                        FROM sys.parameters p
                        inner join sys.types t on p.system_type_id = t.system_type_id AND p.user_type_id = t.user_type_id
                        WHERE object_id = OBJECT_ID('{storeProcedure}')
                    ";

                        var data = this.Query<VariableModel>(sqlQueryType, commandType: CommandType.Text).ToList();
                        if (data.Count == 0)
                        {
                            var isExisted = this.Query<int?>("IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND OBJECT_ID = OBJECT_ID('" + storeProcedure + "')) select 1 ", commandType: CommandType.Text).FirstOrDefault();
                            if (isExisted != 1)
                            {
                                throw new Exception("Store Procedure: " + storeProcedure + " does not exit");
                            }
                        }

                        MappingVariableTypeOfStoreProcedures.TryAdd(storeProcedure, null);

                        foreach (var item in data)
                        {
                            var tmpRefererenceName = storeProcedure + "_" + item.VariableName.Substring(1);
                            if (!MappingVariableTypeOfStoreProcedures.ContainsKey(tmpRefererenceName))
                            {
                                MappingVariableTypeOfStoreProcedures.TryAdd(tmpRefererenceName, (string)item.VariableType);
                            }
                        }
                    }
                }

                if (!MappingVariableTypeOfStoreProcedures.ContainsKey(referenceName))
                {
                    return null;
                }

                var variableType = MappingVariableTypeOfStoreProcedures[referenceName];
                if (variableType == null)
                {
                    return null;
                }

                return new VariableModel()
                {
                    VariableName = variableName,
                    VariableType = variableType
                };
            }
        }

        private bool IsDictionary(object o)
        {
            if (o == null) return false;
            return o is IDictionary &&
                   o.GetType().IsGenericType &&
                   o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(Dictionary<,>));
        }

        public bool IsList(object o)
        {
            if (o == null) return false;
            return o is IList &&
                   o.GetType().IsGenericType &&
                   o.GetType().GetGenericTypeDefinition().IsAssignableFrom(typeof(List<>));
        }

        private void AnalyzyValue(List<SqliteParameter> sqlParameters, VariableModel column, object valueModel)
        {
            var sqlDBType = GetSqlDbType(column);
            if (valueModel == null && sqlDBType == SqlDbType.Structured){
                var sqlParameter = new SqliteParameter("@" + column.VariableName, sqlDBType);
                sqlParameter.SqliteType = column.SqliteType;
                sqlParameters.Add(sqlParameter);
                return;
            }
            if (valueModel == null)
            {   var sqlParameter = new SqliteParameter("@" + column.VariableName, sqlDBType);
                sqlParameter.Value = DBNull.Value;
                //sqlParameter.TypeName = column.VariableType;
                sqlParameters.Add(sqlParameter);
                return;
            }
            if (IsList(valueModel))
            {
                var values = (IEnumerable)valueModel;
                var dataTable = new DataTable();
                var hasData = false;
                bool? hasProperties = null;
                foreach (var valueDynamic in values)
                {
                    var value = (object)valueDynamic;
                    var childProperties = value.GetType().GetProperties();
                    if (MappingValidTypes.ContainsKey(value.GetType().FullName))
                    {
                        hasProperties = false;
                    }
                    if (hasProperties == null)
                    {
                        hasProperties = childProperties.Any();
                    }
                    if (dataTable.Columns.Count == 0)
                    {
                        if (hasProperties == true)
                        {
                            foreach (var child in childProperties)
                            {
                                var childType = Nullable.GetUnderlyingType(child.PropertyType) ?? child.PropertyType;
                                if (!MappingValidTypes.ContainsKey(childType.FullName))
                                {
                                    throw new Exception("Not Accept Object In Parameters");
                                }

                                dataTable.Columns.Add(child.Name, childType.UnderlyingSystemType);
                            }
                        }
                        else
                        {
                            var type = Nullable.GetUnderlyingType(value.GetType()) ?? value.GetType();
                            dataTable.Columns.Add(column.VariableName, type);
                        }
                    }
                    var dataRow = dataTable.NewRow();
                    if (hasProperties == true)
                    {
                        foreach (var child in childProperties)
                        {
                            var childValue = child.GetValue(value);
                            if (childValue == null)
                            {
                                dataRow[child.Name] = DBNull.Value;
                            }
                            else
                            {
                                dataRow[child.Name] = childValue;
                            }
                        }
                        dataTable.Rows.Add(dataRow);
                    }
                    else
                    {
                        dataRow[column.VariableName] = value;
                        dataTable.Rows.Add(dataRow);
                    }

                    hasData = true;
                }

                var sqlParameter = new SqliteParameter("@" + column.VariableName, SqlDbType.Structured);
                if (hasData)
                {
                    sqlParameter.Value = dataTable;
                }
                sqlParameter.SqliteType = column.SqliteType;
                sqlParameters.Add(sqlParameter);
                return;
            }

            var typeModel = (Type)valueModel.GetType();
            var valueType = Nullable.GetUnderlyingType(typeModel) ?? typeModel;

            if (valueType == typeof(DataTable))
            {
                var sqlParameter = new SqliteParameter("@" + column.VariableName, SqlDbType.Structured);
                sqlParameter.Value = valueModel;
                sqlParameter.SqliteType = column.SqliteType;
                sqlParameters.Add(sqlParameter);
                return;
            }

            if (!MappingValidTypes.ContainsKey(valueType.ToString()))
            {
                throw new Exception("Not Accept Object In Parameters");
            }
            else
            {
                var sqlParameter = new SqliteParameter("@" + column.VariableName, sqlDBType);

                var value = valueModel;
                if (value == null)
                {
                    sqlParameter.Value = DBNull.Value;
                }
                else
                {
                    sqlParameter.Value = value;
                }
                sqlParameters.Add(sqlParameter);

                return;
            }
        }

        private SqlDbType GetSqlDbType(VariableModel column)
        {
            var decalreType = column.VariableType;
            SqlDbType sqlDBType = SqlDbType.NVarChar;
            if (Enum.TryParse(decalreType, true, out sqlDBType))
            {
                return sqlDBType;
            };

            return SqlDbType.Structured;
        }

        private List<SqliteParameter> BuildParameters(string procedureName, object param = null, CommandType commandType = CommandType.StoredProcedure)
        {
            
            Initialize();
            var sqlParameters = new List<SqliteParameter>();
            lock (IsLocked)
            {
                if (param != null)
                {
                    if (IsDictionary(param))
                    {
                        if (commandType == CommandType.StoredProcedure)
                        {
                            var pairs = (IDictionary<string, object>)param;
                            foreach (var pair in pairs)
                            {
                                var variableName = (string)pair.Key;
                                var column = this.GetVariable(procedureName, variableName);
                                if (column == null)
                                {
                                    continue;
                                }

                                var valueModel = (object)pair.Value;

                                AnalyzyValue(sqlParameters, column, valueModel);

                            }
                            return sqlParameters;
                        }
                        throw new Exception("Not Support Dictionary Paramaeter with Command Type: " + commandType);
                    }

                    var properties = param.GetType().GetProperties();

                    if (commandType == CommandType.StoredProcedure)
                    {
                        foreach (var property in properties)
                        {
                            var column = this.GetVariable(procedureName, property.Name);
                            if (column == null)
                            {
                                continue;
                            }
                            var valueModel = property.GetValue(param);

                            AnalyzyValue(sqlParameters, column, valueModel);

                        }

                        return sqlParameters;
                    }

                    if (commandType == CommandType.Text)
                    {
                        foreach (var property in properties)
                        {
                            var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                            if (propertyType.IsGenericType || propertyType.IsArray)
                            {
                                throw new Exception("Command Text not support parameter as list");
                            }

                            if (!MappingValidTypes.ContainsKey(propertyType.FullName))
                            {
                                throw new Exception("Not Accept Object In Parameters");
                            }
                            else
                            {

                                var sqlDBType = MappingValidTypes[propertyType.FullName];
                                var sqlParameter = new SqliteParameter("@" + property.Name, sqlDBType);
                                var value = property.GetValue(param);
                                if (value == null)
                                {
                                    sqlParameter.Value = DBNull.Value;
                                }
                                else
                                {
                                    sqlParameter.Value = property.GetValue(param);
                                }
                                sqlParameters.Add(sqlParameter);
                                continue;
                            }

                        }
                        return sqlParameters;
                    }
                }
            }
            

            return sqlParameters;
        }



        private static Dictionary<string, string> mappingSelect = new Dictionary<string, string>();
        private static Dictionary<string, string[]> mappingInsertOrUpdateColumn = new Dictionary<string, string[]>();
        private static Dictionary<string, string[]> mappingUpdateColumn = new Dictionary<string, string[]>();
        private static Dictionary<string, string> mappingInsert = new Dictionary<string, string>();
        private static Dictionary<string, string> mappingBulkInsert = new Dictionary<string, string>();
        private static Dictionary<string, Dictionary<string, PropertyInfo>> mappingProperties = new Dictionary<string, Dictionary<string, PropertyInfo>>();

        private static Dictionary<string, PropertyInfo> mappingPrimary = new Dictionary<string, PropertyInfo>();
        private static Dictionary<string, ConfigurationAttribute> mappingConfiguration = new Dictionary<string, ConfigurationAttribute>();
        private static Dictionary<string, string> mappingTableNames = new Dictionary<string, string>();

        private static Dictionary<string, bool> mappingAuditLogs = new Dictionary<string, bool>();

        private static Dictionary<string, string> mappingUpdate = new Dictionary<string, string>();

        private static bool STA = false;
        private string GetColumns<T>()
        {
            var type = typeof(T);
            if (mappingSelect.ContainsKey(type.FullName))
            {
                return mappingSelect[type.FullName];
            }

            lock (IsLocked)
            {
                if (!mappingSelect.ContainsKey(type.FullName))
                {
                    var selectQuery = "[" + string.Join("], [", type.GetProperties(BindingFlags.Public | BindingFlags.SetField | BindingFlags.SetProperty | BindingFlags.Instance)
                        .Where(m => m.GetCustomAttributes(typeof(IgnoreAttribute), true).FirstOrDefault() == null && this.CheckValidTypes(m.PropertyType))
                        .Select(m => m.Name).ToArray()) + "]";

                    mappingSelect.Add(type.FullName, selectQuery);
                    STA = false;
                    return selectQuery;
                }
            }

            return mappingSelect[type.FullName];
        }

        private bool CheckValidTypes(Type type)
        {
            var originType = Nullable.GetUnderlyingType(type) ?? type;
            return MappingValidTypes.ContainsKey(originType.FullName);
        }

        private Dictionary<string, PropertyInfo> GetMappingProperties<T>()
        {
            return GetMappingProperties(typeof(T));
        }

        private Dictionary<string, PropertyInfo> GetMappingProperties(Type type)
        {
            if (!mappingProperties.ContainsKey(type.FullName))
            {
                lock (IsLocked)
                {
                    if (mappingProperties.ContainsKey(type.FullName))
                    {
                        return mappingProperties[type.FullName];
                    }
                    var properties = type.GetProperties(BindingFlags.Public | BindingFlags.SetField | BindingFlags.SetProperty | BindingFlags.Instance)
                                   .Where(m => m.GetCustomAttributes(typeof(IgnoreAttribute), true).FirstOrDefault() == null
                                       && m.GetCustomAttributes(typeof(IgnoreUpdateAttribute), true).FirstOrDefault() == null
                                       && this.CheckValidTypes(m.PropertyType)
                                       )
                                   .ToDictionary(m => m.Name);

                    mappingProperties.Add(type.FullName, properties);

                    return properties;
                }
            }
            return mappingProperties[type.FullName];
        }

        private static object IsLockUpdatedColumn = new object();

        private string[] GetUpdatedColumns<T>()
        {
            var type = typeof(T);
            if (!mappingInsertOrUpdateColumn.ContainsKey(type.FullName))
            {
                lock (IsLockUpdatedColumn)
                {
                    if (mappingInsertOrUpdateColumn.ContainsKey(type.FullName))
                    {
                        return mappingInsertOrUpdateColumn[type.FullName];
                    }

                    var instanceProperties = type.GetProperties(BindingFlags.Public | BindingFlags.SetField | BindingFlags.SetProperty | BindingFlags.Instance)
                  .Where(m => m.GetCustomAttributes(typeof(IgnoreAttribute), true).FirstOrDefault() == null
                      && m.GetCustomAttributes(typeof(IgnoreUpdateAttribute), true).FirstOrDefault() == null);

                    var properties = new List<string>();
                    foreach (var property in instanceProperties)
                    {
                        var childType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                        if (!MappingValidTypes.ContainsKey(childType.FullName))
                        {
                            continue;
                        }

                        properties.Add(property.Name);
                    }

                    mappingInsertOrUpdateColumn.Add(type.FullName, properties.ToArray());

                    return properties.ToArray();
                }
            }
            return mappingInsertOrUpdateColumn[type.FullName];
        }

        private ConfigurationAttribute GetInstanceConfiguration(Type type)
        {
            return mappingConfiguration[type.FullName];
        }



        private static object IsLockedConfiguration = new object();

        public void InitializeConfiguration<TEntity>(Type typeofRepository, bool isAllowSyncStructure)
        {
            if (mappingConfiguration.ContainsKey(typeofRepository.FullName))
            {
                return;
            }

            lock (IsLockedConfiguration)
            {
                if (mappingConfiguration.ContainsKey(typeofRepository.FullName))
                {
                    return;
                }

                var configuration = typeofRepository.GetCustomAttribute<ConfigurationAttribute>();
                if (configuration == null)
                {
                    throw new Exception("The class: " + typeofRepository.FullName + " still not config");
                }

                if (string.IsNullOrEmpty(configuration.TableName))
                {
                    throw new Exception("The class: " + typeofRepository.FullName + " is missing table name");
                }

                if (string.IsNullOrEmpty(configuration.PrimaryColumn))
                {
                    throw new Exception("The class: " + typeofRepository.FullName + " is missing primary column");
                }

                mappingConfiguration.Add(typeofRepository.FullName, configuration);

                if (isAllowSyncStructure)
                {
                    InitTable<TEntity>(typeofRepository);
                }
            }
        }

        public void InitTable<TEntity>(Type typeofRepository) {
            this.Initialize();
            var updatedColumns = this.GetUpdatedColumns<TEntity>();
            var configuration = mappingConfiguration[typeofRepository.FullName];
            var insertedColumns = new List<string>();
            var mappingColumns = this.GetMappingProperties<TEntity>();
            foreach(var updateColumn in updatedColumns)
            {
                var property = mappingColumns[updateColumn];
                var typeOfProperty = property.Name;
                if (!CheckValidTypes(property.PropertyType))
                {
                    throw new Exception("Not Support Type: " + property.Name + " - " + property.Name);
                }
                var type = property.PropertyType;
                var originType = Nullable.GetUnderlyingType(type) ?? type;
                var typeOfDB = MappingValidTypes[originType.FullName];
                if (updateColumn == configuration.PrimaryColumn)
                {
                    insertedColumns.Add("[" + updateColumn + "] " + typeOfDB + " primary key");
                    continue;
                }
                insertedColumns.Add("[" + updateColumn + "] " + typeOfDB);
            }
            var insertedColumnStr = string.Join(",", insertedColumns);
            var tableStructure = "CREATE TABLE IF NOT EXISTS [" + configuration.TableName + "] (" + insertedColumnStr + ");";
            var effected = this.Execute(tableStructure, null, commandType: CommandType.Text);
            if (effected == 0)
            {
                var columns = this.Query<ColumnInfo>("PRAGMA table_info(" + configuration.TableName + ");", null, commandType: CommandType.Text);
                var mappingDBColumns = columns.ToDictionary(x => x.name);
                foreach (var mappingColumn in mappingColumns)
                {
                    if (!mappingDBColumns.ContainsKey(mappingColumn.Key))
                    {
                        var statementQuery = "ALTER TABLE [" + configuration.TableName + "]  ADD [" + mappingColumn.Key + "] TEXT;";
                        this.Execute(statementQuery, null, commandType: CommandType.Text);
                        continue;
                    }

                    //var property = mappingColumn.Value;
                    //var type = property.PropertyType;
                    //var originType = Nullable.GetUnderlyingType(type) ?? type;
                    //var typeOfDB = MappingValidTypes[originType.FullName];
                    //var columnDB = mappingDBColumns[mappingColumn.Key];
                   
                }
            }
        }
    }
}
