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
    /// Handles the database connection and interaction.
    /// </summary>
    public class Database
    {
        #region "Public Properties"
        public static OracleConnection Connection { get; set; }
        #endregion

        #region "Public Methods"
        /// <summary>
        /// Open a connection to the database.
        /// </summary>
        /// <param name="connectionString"></param>
        public static void OpenConnection(string connectionString)
        {
            try
            {
                Connection = new OracleConnection(connectionString);
                Connection.Open();
            }
            catch (Exception ex) { throw new Exception(ex.Message, ex); }
        }

        /// <summary>
        /// Open a connection to the database.
        /// </summary>
        public static void OpenConnection(string user, string password, string database)
        {
            try
            {
                string connectionString = string.Format("Data Source={0};User Id={1};Password={2};", database, user, password);
                Connection = new OracleConnection(connectionString);
                Connection.Open();
            }
            catch (Exception ex) { throw new Exception(ex.Message, ex); }
        }

        /// <summary>
        /// Query data from the database.
        /// </summary>
        /// <typeparam name="T">Must be a class derived from DBTable</typeparam>
        /// <param name="sqlQuery"></param>
        /// <returns>A List (of T) of the query results</returns>
        public static List<T> Query<T>(string sqlQuery) where T : DBTable
        {
            // setup return value
            List<T> result = new List<T>();

            // Get the type of the table we're working with
            Type tableType = typeof(T);

            // Get the properties available on this table
            PropertyInfo[] properties = tableType.GetProperties();

            // Lookup of PropertyInfo by column name
            Dictionary<string, PropertyInfo> propertiesByColumn = new Dictionary<string, PropertyInfo>();

            try
            {
                using (OracleCommand cmd = new OracleCommand(sqlQuery, Connection))
                {
                    using (OracleDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            // create new instance of our table
                            T tmp = (T)Activator.CreateInstance(tableType);

                            // set updatable flag so we know this instance should 
                            // be updated instead of inserted when saving
                            tmp.Updatable = true;

                            // determines if we have cached the columns yet
                            bool columnsAreCached = propertiesByColumn.Count > 0;

                            // loop through each field returned
                            for (int i = 0; i < dr.FieldCount; i++)
                            {
                                PropertyInfo prop = null;
                                string propertyName = string.Empty;
                                string columnNameDb = string.Empty;

                                if (!columnsAreCached)
                                {
                                    // get the name of the column as it appears in the database
                                    // and translate it into the property name
                                    columnNameDb = dr.GetName(i).ToLower();
                                    propertyName = columnNameDb.ToPascalCase();

                                    // this is for the count and max reserved columns, think about changing this...
                                    if (columnNameDb == "count") { tmp.Count = int.Parse(dr[i].ToString()); continue; }
                                    if (columnNameDb == "max") { tmp.Max = int.Parse(dr[i].ToString()); continue; }

                                    // find the property for this column
                                    prop = properties.Where(p => p.Name == propertyName).FirstOrDefault();

                                    if (prop != null) propertiesByColumn.Add(columnNameDb, prop);
                                }
                                else
                                {
                                    // column name as it appears in the database
                                    columnNameDb = dr.GetName(i).ToLower();

                                    // lookup the PropertyInfo if this key exists in the cache
                                    if (propertiesByColumn.ContainsKey(columnNameDb))
                                    {
                                        propertyName = propertiesByColumn[columnNameDb].Name;
                                        prop = propertiesByColumn[columnNameDb];
                                    }
                                }

                                // ignore DBNull
                                if (dr[i] is DBNull) continue;

                                if (prop == null)
                                    throw new Exception("The property " + propertyName + " was not found in class " + tableType.Name + ".");

                                // setting the property value (the magic happens here!)
                                prop.SetValue(tmp, Convert.ChangeType(dr[i], prop.PropertyType), null);

                                // cache original value
                                tmp.InitialValuesByColumn.Add(columnNameDb, prop.GetValue(tmp, null));
                            }

                            // add to result
                            result.Add(tmp);
                        }
                    }
                }
            }
            catch (Exception ex) { throw new Exception(ex.Message, ex); }

            return result;
        }
        #endregion
    }

    /// <summary>
    /// Base class for a database table model.  Generates sql and does fancy things.
    /// </summary>
    public class DBTable
    {

        #region "Private Fields"
        private string TableName;
        #endregion

        #region "Protected Internal Fields"
        protected internal bool Updatable = false;

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
            string statement = Updatable ? GenerateUpdateStatement() : GenerateInsertStatement();

            // Rows affected.
            int rows = 0;

            try
            {
                using (OracleCommand cmd = new OracleCommand(statement, (OracleConnection)Database.Connection))
                {
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
            if (Updatable)
            {
                try
                {
                    List<PropertyInfo> primaryKeys = GetPrimaryKeys();

                    if (primaryKeys == null)
                        throw new Exception("Unable to locate primary key when generating delete statement.");

                    statement.AppendFormat("delete from {0} ", TableName);
                    statement.Append(GetWhereFromPrimaryKeys(primaryKeys));

                    using (OracleCommand cmd = new OracleCommand(statement.ToString(), Database.Connection))
                    {
                        rows = cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex) { throw new Exception(ex.Message + Environment.NewLine + statement, ex); }
            }

            return rows;
        }
        #endregion

    }

    /// <summary>
    /// Used to mark primary key columns in DBTable
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class PrimaryKey : System.Attribute { }
}
