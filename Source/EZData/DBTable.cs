using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Devart.Data.Oracle;

namespace EZData
{
    /// <summary>
    /// Base class for a database table model.  Generates sql and does fancy things.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class DBTable
    {

        #region "Private Fields"
        private string TableName;
        #endregion

        #region "Protected Internal Fields"
        protected internal bool Updateable = false;

        protected internal int Max;
        protected internal int Count;

        // caches initial values, used to check for changes
        protected internal Dictionary<string, object> InitialValuesByColumn = new Dictionary<string, object>();
        #endregion

        #region "Constructors"
        public DBTable()
        {
            // set table name
            TableName = GetType().Name.ToSnakeCase();
        }

        /// <summary>
        /// Explicitly specify the table name
        /// </summary>
        /// <param name="tableName"></param>
        public DBTable(string tableName)
        {
            // set table name
            TableName = tableName;
        }
        #endregion

        #region "Private Methods"
        /// <summary>
        /// Finds the Properties tagged with the [PrimaryKey] attribute.
        /// </summary>
        private List<PropertyInfo> GetPrimaryKeys()
        {
            return GetType().
                GetProperties().Where(p =>
                    p.GetCustomAttributes(typeof(PrimaryKey), false).Length > 0).ToList();
        }

        /// <summary>
        /// Generates a where statement for each primary key in the DBTable
        /// </summary>
        /// <param name="primaryKeys"></param>
        /// <returns></returns>
        private string GetWhereFromPrimaryKeys(List<PropertyInfo> primaryKeys)
        {
            StringBuilder where = new StringBuilder();

            // use all primary keys in the where clause
            for (int i = 0; i < primaryKeys.Count; i++)
            {
                var currentName = primaryKeys[i].Name.ToSnakeCase();
                var currentValue = primaryKeys[i].GetValue(this, null);

                if (i == 0)
                {
                    where.AppendFormat("where {0} = '{1}' ",
                        currentName,
                        currentValue);
                }
                else
                {
                    where.AppendFormat("and {0} = '{1}'",
                        currentName,
                        currentValue);
                }
            }

            return where.ToString();
        }

        /// <summary>
        /// Generate insert statement.
        /// </summary>
        /// <returns></returns>
        private string GenerateInsertStatement()
        {
            // generate a sql statement based off of our columns and table name
            StringBuilder statement = new StringBuilder();

            try
            {
                // get list of properties on this DBTable
                var properties = GetType().GetProperties();

                // start generating insert
                statement.Append("insert into " + TableName + "(");

                // generate column list
                string columns = string.Join(
                    ",",
                    properties.Select(p => p.Name.ToSnakeCase()).ToArray());

                statement.Append(columns);
                statement.Append(") values (");

                // generate values, be sure to account for DateTime(s)
                for (int i = 0; i < properties.Length; i++)
                {
                    // this property's value
                    var value = properties[i].GetValue(this, null);

                    if (properties[i].PropertyType == typeof(DateTime))
                    {
                        // if empty DateTime then do nothing
                        if (value.Equals(DateTime.MinValue))
                            statement.Append("''");
                        else
                            statement.AppendFormat("to_date('{0}','mm/dd/yyyy hh:mi:ss PM')", value);
                    }
                    else
                    {
                        // replace any ticks if not null
                        if (value != null)
                            value = value.ToString().Replace("'", "''");

                        statement.AppendFormat("'{0}'", value);
                    }

                    // append comma to all but the last item
                    if (i != properties.Length - 1)
                        statement.Append(",");
                }

                // done
                statement.Append(")");
            }
            catch (Exception ex) { throw new Exception(ex.Message, ex); }

            return statement.ToString();
        }

        /// <summary>
        /// Generate update statement.
        /// </summary>
        /// <returns></returns>
        private string GenerateUpdateStatement()
        {
            // generate a sql statement based off of our columns and table name
            StringBuilder statement = new StringBuilder();

            try
            {
                // get list of properties on this DBTable
                var properties = GetType().GetProperties();

                // start generating update
                statement.Append("update " + TableName + " ");

                // keeps track of updatable columns
                int numColumnsUpdated = 0;

                // iterate each property 
                for (int i = 0; i < properties.Length; i++)
                {
                    var key = properties[i].Name.ToSnakeCase();

                    object initialValue;
                    InitialValuesByColumn.TryGetValue(key, out initialValue);
                    //initialValue = InitialValuesByColum[properties[i].Name.ToSnakeCase()];

                    // this property's value
                    var value = properties[i].GetValue(this, null);

                    // If initial value is null and the property value is DateTime.MinValue
                    // there was no change, skip it.
                    if (properties[i].PropertyType == typeof(DateTime))
                        if (initialValue == null && value.Equals(DateTime.MinValue)) continue;

                    // If value is null or value equals initial value, there was no change.
                    if (value == null || value.Equals(initialValue)) continue;

                    // updates are dumb, must say "set [column] .."  on the first one
                    if (numColumnsUpdated == 0) statement.Append("set ");

                    // name of column
                    string columnName = properties[i].Name.ToSnakeCase();

                    // deal with DateTime
                    if (properties[i].PropertyType == typeof(DateTime))
                    {
                        if (value.Equals(DateTime.MinValue))
                            statement.Append(columnName + " = ''");
                        else
                            statement.AppendFormat(columnName + " = to_date('{0}','mm/dd/yyyy hh:mi:ss PM')", value);
                    }
                    else
                    {
                        if (value != null)
                            value = value.ToString().Replace("'", "''");

                        // not a DateTime type
                        statement.AppendFormat(columnName + " = '{0}'", value);
                    }

                    // append comma to all but the last item
                    if (i != properties.Length - 1)
                        statement.Append(",");

                    numColumnsUpdated++;
                }

                // get primary key
                List<PropertyInfo> primaryKeys = GetPrimaryKeys();

                if (primaryKeys == null)
                    throw new Exception("Updates to tables without a primary key defined are not yet supported.");

                statement.Append(GetWhereFromPrimaryKeys(primaryKeys));

                //statement.AppendFormat(" where " + primaryKey.Name.ToSnakeCase() + " = '{0}'", primaryKey.GetValue(this, null));

                // bit hacky here, a stray comma gets generated before the where, because we dont know what the last column will be
                // just replace ", where" with " where" and it will be good enough for now. 
                // to fix better, maybe: do a first pass over properties to get list of updatable ones
                // then only loop through those properties to generate the update statement.
                statement.Replace(",where", " where");
            }
            catch (Exception ex) { throw new Exception(ex.Message, ex); }

            return statement.ToString();
        }
        #endregion

        #region "Public Methods"
        /// <summary>
        /// Save the record to the database.
        /// </summary>
        public int Save()
        {
            // determine whether we need and update or insert..
            string statement = Updateable ? GenerateUpdateStatement() : GenerateInsertStatement();

            // Rows affected.
            int rows = 0;

            try
            {
                //new OracleCommand(statement, Database.Connection)
                var cmd = Database.DbProviderFactory.CreateCommand();

                using (cmd)
                {
                    cmd.CommandText = statement;
                    cmd.Connection = Database.Connection;

                    rows = cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { throw new Exception(ex.Message + Environment.NewLine + statement, ex); }

            return rows;
        }

        /// <summary>
        /// Delete record from the database (WARNING: Cannot undo yet)
        /// </summary>
        /// <returns></returns>
        public int Delete()
        {
            // Generate delete sql.
            StringBuilder statement = new StringBuilder();

            // Rows affected.
            int rows = 0;

            // Because you can't delete something that doesn't exist yet.
            if (Updateable)
            {
                try
                {
                    List<PropertyInfo> primaryKeys = GetPrimaryKeys();

                    if (primaryKeys == null)
                        throw new Exception("Unable to locate primary key when generating delete statement.");

                    statement.AppendFormat("delete from {0} ", TableName);
                    statement.Append(GetWhereFromPrimaryKeys(primaryKeys));

                    var cmd = Database.DbProviderFactory.CreateCommand();

                    using (cmd)
                    {
                        cmd.CommandText = statement.ToString();
                        cmd.Connection = Database.Connection;

                        rows = cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex) { throw new Exception(ex.Message + Environment.NewLine + statement, ex); }
            }

            return rows;
        }
        #endregion

    }
}
