using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Data.Common;
using System.Threading.Tasks;

namespace Persistence.SqlLite
{
    public partial class PersistenceContext : IDisposable
    {

        public int CommandTimeout { get; set; } = -1;

        private IEnumerable<T> Query<T>(string sql, object param = null, bool buffered = true, int? commandTimeout = default(int?), SqliteTransaction transaction = null, CommandType commandType = CommandType.StoredProcedure)
        {
            try
            {
                var sqlParameters = this.BuildParameters(sql, param, commandType);

                var command = this.connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = commandType;
                command.CommandTimeout = this.CommandTimeout;
                command.Transaction = transaction ?? this.transaction;
                if (sqlParameters.Count > 0)
                {
                    command.Parameters.AddRange(sqlParameters.ToArray());
                }

                using (var reader = command.ExecuteReader())
                {
                    var result = this.GetDataFromReader<T>(reader);

                    reader.Close();
                    return result;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private async Task<IEnumerable<T>> QueryAsync<T>(string sql, object param = null, SqliteTransaction transaction = null, CommandType commandType = CommandType.StoredProcedure)
        {
            if (sql == "sp_Orders_GetOrderList")
            {
                var st = "";
            }
            try
            {
                var sqlParameters = this.BuildParameters(sql, param, commandType);

                var command = this.connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = commandType;
                command.CommandTimeout = this.CommandTimeout;
                command.Transaction = transaction ?? this.transaction;
                if (sqlParameters.Count > 0)
                {
                    command.Parameters.AddRange(sqlParameters.ToArray());
                }

                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    var result = this.GetDataFromReader<T>(reader);

                    reader.Close();

                    return result;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private async Task<Tuple<IEnumerable<T1>, IEnumerable<T2>>> QueryAsync<T1, T2>(string sql, object param = null, SqliteTransaction transaction = null, CommandType commandType = CommandType.StoredProcedure)
        {
            try
            {
                var sqlParameters = this.BuildParameters(sql, param, commandType);

                var command = this.connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = commandType;
                command.CommandTimeout = this.CommandTimeout;
                command.Transaction = transaction ?? this.transaction;
                if (sqlParameters.Count > 0)
                {
                    command.Parameters.AddRange(sqlParameters.ToArray());
                }

                IEnumerable<T1> result1 = new List<T1>();
                IEnumerable<T2> result2 = new List<T2>();
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (reader.HasRows)
                    {
                        result1 = this.GetDataFromReader<T1>(reader);

                    }

                    reader.NextResult();

                    if (reader.HasRows)
                    {
                        result2 = this.GetDataFromReader<T2>(reader);
                    }

                    reader.Close();
                }

                return new Tuple<IEnumerable<T1>, IEnumerable<T2>>(result1, result2);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private Tuple<IEnumerable<T1>, IEnumerable<T2>> Query<T1, T2>(string sql, object param = null, SqliteTransaction transaction = null, CommandType commandType = CommandType.StoredProcedure)
        {
            try
            {
                var sqlParameters = this.BuildParameters(sql, param, commandType);

                var command = this.connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = commandType;
                command.CommandTimeout = this.CommandTimeout;
                command.Transaction = transaction ?? this.transaction;
                if (sqlParameters.Count > 0)
                {
                    command.Parameters.AddRange(sqlParameters.ToArray());
                }

                IEnumerable<T1> result1 = new List<T1>();
                IEnumerable<T2> result2 = new List<T2>();
                using (var reader = command.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        result1 = this.GetDataFromReader<T1>(reader);

                    }

                    reader.NextResult();

                    if (reader.HasRows)
                    {
                        result2 = this.GetDataFromReader<T2>(reader);
                    }
                }

                return new Tuple<IEnumerable<T1>, IEnumerable<T2>>(result1, result2);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public IEnumerable<T> Query<T>(string storeName, object param = null)
        {
            var items = this.Query<T>(storeName, param, commandType: CommandType.StoredProcedure);
            return items;
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string storeName, object param = null)
        {
            var items = await this.QueryAsync<T>(storeName, param, commandType: CommandType.StoredProcedure).ConfigureAwait(false);
            return items;
        }

        public async Task<Tuple<IEnumerable<string>, IEnumerable<T>>> QueryAsColumnsAndDataAsync<T>(string storeName, object param = null)
        {
            try
            {
                var sqlParameters = this.BuildParameters(storeName, param, commandType: CommandType.StoredProcedure);

                var command = this.connection.CreateCommand();
                command.CommandText = storeName;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = this.CommandTimeout;
                command.Transaction = transaction ?? this.transaction;
                if (sqlParameters.Count > 0)
                {
                    command.Parameters.AddRange(sqlParameters.ToArray());
                }
                var columns = new List<string>();
                IEnumerable<T> result;
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(reader.GetName(i));
                    }

                    result = this.GetDataFromReader<T>(reader);

                    reader.Close();

                    return new Tuple<IEnumerable<string>, IEnumerable<T>>(columns, result);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<Tuple<IEnumerable<T1>, IEnumerable<T2>>> QueryAsync<T1, T2>(string storeName, object param = null)
        {
            var result = await this.QueryAsync<T1, T2>(storeName, param, commandType: CommandType.StoredProcedure).ConfigureAwait(false);
            return result;
        }

        public Tuple<IEnumerable<T1>, IEnumerable<T2>> Query<T1, T2>(string storeName, object param = null)
        {
            var result = this.Query<T1, T2>(storeName, param, commandType: CommandType.StoredProcedure);
            return result;
        }

        private const string Const_Parentheses_Start = "(";
        private const string Const_Parentheses_End = ")";

        private string BuildQueryString<T>(Expression<Func<T, bool>> predicate, Type repoType, bool checkAlive = false)
        {
            var type = typeof(T);
            var configuration = this.GetInstanceConfiguration(repoType);
            var table = configuration.TableName;
            if (string.IsNullOrEmpty(table))
            {
                throw new Exception("The entity still not setup table name");
            }

            var condition = string.Empty;
            if (predicate != null)
            {
                condition = string.Concat(Const_Parentheses_Start, predicate.ToSql<T>(), Const_Parentheses_End);
            }

            string checkDeleteSql = null;
            var mappingColumns = this.GetMappingProperties<T>();
            if (checkAlive && mappingColumns.ContainsKey(IsDeletedPropertyName))
            {
                if (!string.IsNullOrEmpty(condition))
                {
                    checkDeleteSql = "IsDeleted = 0 AND";
                }
                else
                {
                    checkDeleteSql = "IsDeleted = 0";
                }
            }

            var whereCondition = string.Empty;
            if (!string.IsNullOrEmpty(condition) || !string.IsNullOrEmpty(checkDeleteSql))
            {
                whereCondition = $"WHERE {checkDeleteSql} {condition}";
            }
            var query = "";
            if (this.transaction == null)
            {
                query = $"FROM [{table}] (NOLOCK) {whereCondition}";
            }
            else
            {
                query = $"FROM [{table}] {whereCondition}";
            }

            return query;
        }

        public IEnumerable<T> Query<T>(Expression<Func<T, bool>> predicate, Type repoType, bool checkAlive = false) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);

            var sb = new StringBuilder();
            var columns = this.GetColumns<T>();

            var items = this.Query<T>($"SELECT {columns} {query}", commandType: CommandType.Text);

            return items;
        }

        public async Task<int?> CountAsync<T>(Expression<Func<T, bool>> predicate, Type repoType, bool checkAlive = false) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);

            var sb = new StringBuilder();
            var columns = this.GetColumns<T>();

            var items = await this.QueryAsync<int?>($"SELECT COUNT(0) {query}", commandType: CommandType.Text).ConfigureAwait(false);

            return items.FirstOrDefault();
        }

        public async Task<int?> SumAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> columnSum, Type repoType, bool checkAlive = false) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);
            var nameOfColumnSum = columnSum.GetFieldName();
            var sb = new StringBuilder();
            var columns = this.GetColumns<T>();

            var items = await this.QueryAsync<int?>($"SELECT SUM(IIF([{nameOfColumnSum}] IS NULL, 0 , [{nameOfColumnSum}])) {query}", commandType: CommandType.Text).ConfigureAwait(false);

            return items.FirstOrDefault();
        }

        public async Task<T1> MaxAsync<T, T1>(Expression<Func<T, bool>> predicate, Expression<Func<T, T1>> column, Type repoType, bool checkAlive = false) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);
            var nameOfColumn = column.GetFieldName<T, T1>();
            var sb = new StringBuilder();
            var columns = this.GetColumns<T>();

            var items = await this.QueryAsync<T1>($"SELECT MAX([{nameOfColumn}]) {query}", commandType: CommandType.Text).ConfigureAwait(false);

            return items.FirstOrDefault();
        }

        public async Task<T1> MinAsync<T, T1>(Expression<Func<T, bool>> predicate, Expression<Func<T, T1>> column, Type repoType, bool checkAlive = false) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);
            var nameOfColumn = column.GetFieldName<T, T1>();
            var sb = new StringBuilder();
            var columns = this.GetColumns<T>();

            var items = await this.QueryAsync<T1>($"SELECT MIN([{nameOfColumn}]) {query}", commandType: CommandType.Text).ConfigureAwait(false);

            return items.FirstOrDefault();
        }

        public async Task<IEnumerable<T1>> GroupByAsync<T, T1>(Expression<Func<T, bool>> predicate, Expression<Func<T, T1>> column, Type repoType, bool checkAlive = false) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);
            var nameOfColumn = column.GetFieldName<T, T1>();
            var sb = new StringBuilder();
            var columns = this.GetColumns<T>();

            var items = await this.QueryAsync<T1>($"SELECT [{nameOfColumn}] {query} GROUP BY [{nameOfColumn}]", commandType: CommandType.Text).ConfigureAwait(false);

            return items;
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(Expression<Func<T, bool>> predicate, Type repoType, bool checkAlive = false) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);

            var sb = new StringBuilder();
            var columns = this.GetColumns<T>();

            var items = await this.QueryAsync<T>($"SELECT {columns} {query}", commandType: CommandType.Text).ConfigureAwait(false);

            return items;
        }

        public async Task<IEnumerable<TOut>> QueryAsync<T, TOut>(Expression<Func<T, bool>> predicate, Type repoType, bool checkAlive = false) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);

            var sb = new StringBuilder();
            var columns = this.GetColumns<TOut>();

            var items = await this.QueryAsync<TOut>($"SELECT {columns} {query}", commandType: CommandType.Text).ConfigureAwait(false);

            return items;
        }

        public async Task<IEnumerable<TOut>> QueryAsync<T, TOut>(Expression<Func<T, bool>> predicate, Type repoType, bool checkAlive = false, Expression<Func<T, object>> column = null) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);

            var sb = new StringBuilder();
            var mappingProperties = this.GetMappingProperties<T>();
            var columnName = column.GetFieldName();
            if (!mappingProperties.ContainsKey(columnName))
            {
                throw new Exception("Not Found Column Name: " + columnName + " In the Type: " + typeof(T).FullName);
            }

            var items = await this.QueryAsync<TOut>($"SELECT {columnName} {query}", commandType: CommandType.Text).ConfigureAwait(false);

            return items;
        }

        public T FirstOrDefault<T>(Expression<Func<T, bool>> predicate, Type repoType, bool checkAlive = false) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);

            var sb = new StringBuilder();
            var columns = this.GetColumns<T>();

            var item = this.Query<T>($"SELECT TOP 1 {columns} {query}", commandType: CommandType.Text).FirstOrDefault();

            return item;
        }

        private const string IsDeletedPropertyName = "IsDeleted";

        public async Task<T> FirstOrDefaultAsync<T>(Expression<Func<T, bool>> predicate, Type repoType, bool checkAlive = false) where T : IEntity
        {
            var query = this.BuildQueryString(predicate, repoType, checkAlive);

            var sb = new StringBuilder();
            var columns = this.GetColumns<T>();

            var items = await this.QueryAsync<T>($"SELECT TOP 1 {columns} {query}", commandType: CommandType.Text).ConfigureAwait(false);

            return items.FirstOrDefault();
        }

        public T FirstOrDefault<T>(string storeProcedure, object param = null)
        {
            var sqlParameters = this.BuildParameters(storeProcedure, param, CommandType.StoredProcedure);

            var command = this.connection.CreateCommand();
            command.CommandText = storeProcedure;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = this.CommandTimeout;
            command.Transaction = this.transaction;
            if (sqlParameters.Count > 0)
            {
                command.Parameters.AddRange(sqlParameters.ToArray());
            }

            using (var reader = command.ExecuteReader())
            {
                var result = this.GetSingleRecordFromDataReader<T>(reader);

                return result;
            }
        }

        public async Task<T> FirstOrDefaultAsync<T>(string storeProcedure, object param = null)
        {
            var sqlParameters = this.BuildParameters(storeProcedure, param, CommandType.StoredProcedure);

            var command = this.connection.CreateCommand();
            command.CommandText = storeProcedure;
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = this.CommandTimeout;
            command.Transaction = this.transaction;
            if (sqlParameters.Count > 0)
            {
                command.Parameters.AddRange(sqlParameters.ToArray());
            }

            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                var result = this.GetSingleRecordFromDataReader<T>(reader);

                return result;
            }
        }

        private int Execute(string sql, object param = null, int? commandTimeout = default(int?), SqliteTransaction transaction = null, CommandType? commandType = CommandType.StoredProcedure)
        {
            int result = -1;
            try
            {
                var command = this.connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = commandType.Value;
                command.Transaction = transaction ?? this.transaction;
                command.CommandTimeout = this.CommandTimeout;
                if (param != null && commandType == CommandType.StoredProcedure)
                {
                    var sqlParameters = this.BuildParameters(sql, param, CommandType.StoredProcedure);
                    if (sqlParameters.Count > 0)
                    {
                        command.Parameters.AddRange(sqlParameters.ToArray());
                    }
                }
                result = command.ExecuteNonQuery();
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw ex;
            }
        }

        private async Task<int> ExecuteAsync(string sql, object param = null, int? commandTimeout = default(int?), SqliteTransaction transaction = null, CommandType? commandType = CommandType.StoredProcedure)
        {
            int result = -1;
            try
            {
                var sqlParameters = this.BuildParameters(sql, param, CommandType.StoredProcedure);
                var command = this.connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = commandType.Value;
                command.Transaction = transaction ?? this.transaction;
                command.CommandTimeout = this.CommandTimeout;
                if (sqlParameters.Count > 0)
                {
                    command.Parameters.AddRange(sqlParameters.ToArray());
                }
                result = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw ex;
            }
        }

        public int Execute(string sql, object param = null)
        {
            int result = -1;
            try
            {
                result = this.Execute(sql, param, commandType: CommandType.StoredProcedure);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw ex;
            }
        }

        public async Task<int> ExecuteAsync(string sql, object param = null)
        {
            int result = -1;
            try
            {
                result = await this.ExecuteAsync(sql, param, commandType: CommandType.StoredProcedure).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw ex;
            }
        }

        public void Remove<T>(Type repoType, params T[] items) where T : IEntity
        {
            if (items.Length == 0)
            {
                return;
            }
            var type = typeof(T);
            var configuration = this.GetInstanceConfiguration(repoType);
            var mappingProperties = GetMappingProperties<T>();
            var primaryAttribute = mappingProperties[configuration.PrimaryColumn];
            var mappingConfigurations = GetMappingProperties<T>();
            var ids = new List<string>();
            foreach (var item in items)
            {
                var value = primaryAttribute.GetValue(item);

                var primaryValueAsSql = GetColumnValueAsSql(value, primaryAttribute.PropertyType);

                ids.Add(primaryValueAsSql);
            }

            var idStr = string.Join(",", ids);

            this.Execute($"DELETE [{configuration.TableName}] WHERE [{primaryAttribute.Name}] IN ({idStr})", null, commandType: CommandType.Text);
        }

        public async Task RemoveAsync<T>(Type repoType, params T[] items) where T : IEntity
        {
            if (items.Length == 0)
            {
                return;
            }

            var type = typeof(T);

            var configuration = this.GetInstanceConfiguration(repoType);
            var mappingProperties = GetMappingProperties<T>();
            var primaryAttribute = mappingProperties[configuration.PrimaryColumn];
            var mappingConfigurations = GetMappingProperties<T>();
            var ids = new List<string>();
            foreach (var item in items)
            {
                var value = primaryAttribute.GetValue(item);

                var primaryValueAsSql = GetColumnValueAsSql(value, primaryAttribute.PropertyType);

                ids.Add(primaryValueAsSql);
            }

            var idStr = string.Join(",", ids);

            await this.ExecuteAsync($"DELETE [{configuration.TableName}] WHERE [{primaryAttribute.Name}] IN ({idStr})", null, commandType: CommandType.Text).ConfigureAwait(false);
        }

        public async Task BulkInsertAsync<T>(List<T> models, Type typeRepo) where T : IEntity
        {
            this.Initialize();

            var confuration = GetInstanceConfiguration(typeRepo);

            var table = confuration.TableName;
            var mappingProperties = GetMappingProperties<T>();

            List<string> listColumns = GetUpdatedColumns<T>().ToList();

            var dataTable = new DataTable();

            foreach (var columnName in listColumns)
            {
                var property = mappingProperties[columnName];

                var childType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

                dataTable.Columns.Add(columnName, childType);
            }
            var tmpTransaction = this.transaction == null ? this.connection.BeginTransaction() : this.transaction;
            try
            {
                foreach (var model in models)
                {
                    var setSqlColumns = new List<string>();
                    foreach (var columnName in listColumns)
                    {
                        var property = mappingProperties[columnName];
                        var value = property.GetValue(model);
                        var valueSql = GetColumnValueAsSql(value, property.PropertyType);
                        setSqlColumns.Add(valueSql);
                    }

                    var columnStr = "[" + string.Join("], [", listColumns) + "]";
                    var valueStr = string.Join(", ", setSqlColumns);

                    var insertQuery = $"INSERT INTO {table} ({columnStr}) VALUES ({valueStr})";
                    var command = this.connection.CreateCommand();
                    command.Transaction = tmpTransaction;
                    command.CommandText = insertQuery;
                    command.CommandType = CommandType.Text;
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                if (this.transaction == null)
                {
                    tmpTransaction.Commit();
                }
            }
            catch (Exception ex)
            {
                if (this.transaction == null)
                {
                    tmpTransaction.Rollback();
                }
                throw new PersistenException(ex);
            }
        }

        public void Insert<T>(T model, Type repoType) where T : IEntity
        {
            this.Initialize();
            var configuration = GetInstanceConfiguration(repoType);

            var tableName = configuration.TableName;

            var isAutoIncrement = configuration.IsAutoIncrement;

            var columns = this.GetUpdatedColumns<T>();

            var mappingProperties = this.GetMappingProperties<T>();

            var primaryAttribute = mappingProperties[configuration.PrimaryColumn];

            var setSqlColumns = new List<string>();

            if (isAutoIncrement)
            {
                columns = columns.Where(m => m != configuration.PrimaryColumn).ToArray();
            }

            foreach (var columnName in columns)
            {
                var property = mappingProperties[columnName];
                var value = property.GetValue(model);
                var valueSql = GetColumnValueAsSql(value, property.PropertyType);
                setSqlColumns.Add(valueSql);
            }

            var columnStr = "[" + string.Join("], [", columns) + "]";
            var valueStr = string.Join(", ", setSqlColumns);

            var insertQuery = $"INSERT INTO {tableName} ({columnStr}) VALUES ({valueStr})";

            if (isAutoIncrement)
            {
                var id = this.Query<long>(insertQuery, model, commandType: CommandType.Text).FirstOrDefault();
                primaryAttribute.SetValue(model, Convert.ChangeType(id, primaryAttribute.PropertyType));
            }
            else
            {
                this.Execute(insertQuery, null, commandType: CommandType.Text);
            }
        }

        public void Update<T>(T model, Type repoType, params Expression<Func<T, object>>[] updatedProperties) where T : IEntity
        {
            this.Initialize();
            List<string> columns = new List<string>();
            if (updatedProperties.Any())
            {
                foreach (var updatedProperty in updatedProperties)
                {
                    var fieldName = updatedProperty.GetFieldName();
                    columns.Add(fieldName);
                }
            }

            this.Update<T>(model, repoType, columns.ToArray());
        }

        public void Update<T>(T model, Type repoType, string[] updatedProperties, SqliteTransaction transaction = null) where T : IEntity
        {
            this.Initialize();
            var type = typeof(T);
            var configuration = this.GetInstanceConfiguration(repoType);
            var mappingProperties = this.GetMappingProperties<T>();
            var table = configuration.TableName;
            var primaryColumn = mappingProperties[configuration.PrimaryColumn];
            List<string> columns = updatedProperties.ToList();
            if (!updatedProperties.Any())
            {
                columns = this.GetUpdatedColumns<T>().ToList();
            }


            var setSqlColumns = new List<string>();
            foreach (var columnName in columns)
            {
                var property = mappingProperties[columnName];
                var value = property.GetValue(model);
                var valueSql = GetColumnValueAsSql(value, property.PropertyType);
                setSqlColumns.Add($"[{columnName}] = {valueSql}");
            }
            var valuePrimary = primaryColumn.GetValue(model);
            var valuePrimarySql = GetColumnValueAsSql(valuePrimary, primaryColumn.PropertyType);
            var querySetSqlColumn = string.Join(", ", setSqlColumns);
            var updatedQuery = $@"
                UPDATE {table}
                SET {querySetSqlColumn}
                WHERE [{primaryColumn.Name}] = {valuePrimarySql}
                ";

            this.Execute(updatedQuery, transaction: transaction, commandType: CommandType.Text);
        }

        protected int BatchSize = -1;

        public async Task BulkUpdate<T>(IEnumerable<T> models, Type repoType, params Expression<Func<T, object>>[] updatedProperties) where T : IEntity
        {
            this.Initialize();

            List<string> listColumns = new List<string>();

            foreach (var updatedProperty in updatedProperties)
            {
                var fieldName = updatedProperty.GetFieldName();
                listColumns.Add(fieldName);
            }

            await this.BulkUpdateAsync<T>(models, repoType, listColumns.ToArray()).ConfigureAwait(false);
        }

        private bool ReflectToSingleUpdate<T>(IEnumerable<T> models, Type repoType, string[] updatedProperties) where T : IEntity
        {
            if (models.Count() < 5)
            {
                var tmpTransaction = this.transaction == null ? this.connection.BeginTransaction() : this.transaction;
                try
                {

                    foreach (var model in models)
                    {
                        this.Update<T>(model, repoType, updatedProperties, tmpTransaction);
                    }

                    if (this.transaction == null && tmpTransaction != null)
                    {
                        tmpTransaction.Commit();
                    }

                    return true;
                }
                catch
                {
                    if (this.transaction == null && tmpTransaction != null)
                    {
                        tmpTransaction.Rollback();
                    }
                    throw;
                }
            }

            return false;
        }

        public async Task BulkUpdateAsync<T>(IEnumerable<T> models, Type repoType, string[] updatedProperties) where T : IEntity
        {

            if (ReflectToSingleUpdate(models, repoType, updatedProperties))
            {
                return;
            }

            this.Initialize();
            var type = typeof(T);
            var configuration = GetInstanceConfiguration(repoType);
            var table = configuration.TableName;
            var mappingProperties = GetMappingProperties<T>();
            var primaryAttribute = mappingProperties[configuration.PrimaryColumn];
            if (!updatedProperties.Any())
            {
                updatedProperties = this.GetUpdatedColumns<T>();
            }

            var temporaryTableName = "Temporary_Bulk_Updated_" + table + Guid.NewGuid();
            var listColumns = updatedProperties.ToList();
            foreach (var propertyName in updatedProperties)
            {
                if (!mappingProperties.ContainsKey(propertyName))
                {
                    throw new Exception($"Not Found: {propertyName} in {type.FullName}");
                }
            }

            if (!listColumns.Any(m => m == primaryAttribute.Name))
            {
                listColumns.Add(primaryAttribute.Name);
            }

            var tmpTransaction = this.transaction == null ? this.connection.BeginTransaction() : this.transaction;
            try
            {
                foreach (var model in models)
                {
                    var setSqlColumns = new List<string>();
                    foreach (var columnName in listColumns)
                    {
                        var property = mappingProperties[columnName];
                        var value = property.GetValue(model);
                        var valueSql = GetColumnValueAsSql(value, property.PropertyType);
                        setSqlColumns.Add($"[{columnName}] = {valueSql}");
                    }
                    var valuePrimary = primaryAttribute.GetValue(model);
                    var valuePrimarySql = GetColumnValueAsSql(valuePrimary, primaryAttribute.PropertyType);
                    var querySetSqlColumn = string.Join(", ", setSqlColumns);
                    var updatedQuery = $@"
                        UPDATE {table}
                        SET {querySetSqlColumn}
                        WHERE [{primaryAttribute.Name}] = {valuePrimarySql}
                    ";
                    var command = this.connection.CreateCommand();
                    command.Transaction = tmpTransaction;
                    command.CommandText = updatedQuery;
                    command.CommandType = CommandType.Text;
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                if (this.transaction == null)
                {
                    tmpTransaction.Commit();
                }
            }
            catch (Exception ex)
            {
                if (this.transaction == null)
                {
                    tmpTransaction.Rollback();
                }
                throw new PersistenException(ex);
            }
        }
    }
}
