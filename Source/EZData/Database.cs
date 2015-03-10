using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Data.Common;

using Devart.Data.Oracle;


namespace EZData
{

    public enum ConnectionType
    {
        DevartOracle,
        DevartMySql,
        DevartSqlServer
    }

    /// <summary>
    /// Handles the database connection and interaction.
    /// </summary>
    public static class Database
    {
        #region "Public Properties"
        public static DbConnection Connection { get; set; }
        public static DbProviderFactory DbProviderFactory
        {
            get
            {
                DbProviderFactory providerFactory = null;

                switch (ConnectionType)
                {
                    case EZData.ConnectionType.DevartOracle:
                        providerFactory = DbProviderFactories.GetFactory("Devart.Data.Oracle");
                        break;

                    case EZData.ConnectionType.DevartMySql:
                        providerFactory = DbProviderFactories.GetFactory("Devart.Data.MySql");
                        break;

                    default:
                        throw new Exception("Connection type not supported or implemented yet.");
                }

                return providerFactory;
            }
        }
        #endregion

        static ConnectionType ConnectionType { get; set; }
        static string ConnectionString { get; set; }

        #region "Public Methods"
        /*
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
        */

        /// <summary>
        /// Open a connection to the database.
        /// </summary>
        public static void OpenConnection(string user, string password, string database, ConnectionType connectionType)
        {
            try
            {
                ConnectionType = connectionType;

                string connectionString = string.Empty;

                switch (ConnectionType)
                {
                    case EZData.ConnectionType.DevartOracle:
                        connectionString = string.Format("Data Source={0};User Id={1};Password={2};", database, user, password);
                        break;

                    case EZData.ConnectionType.DevartMySql:
                        connectionString = string.Format("Database={0};User Id={1};Password={2};", database, user, password);
                        break;
                }

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new Exception("Unable to generate connection string for this connection type.");
                }

                ConnectionString = connectionString;

                Connection = DbProviderFactory.CreateConnection();
                Connection.ConnectionString = ConnectionString;
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
                var cmd = DbProviderFactory.CreateCommand();

                using (cmd)
                {
                    cmd.CommandText = sqlQuery;
                    cmd.Connection = Connection;

                    using (var dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            // create new instance of our table
                            T tmp = (T)Activator.CreateInstance(tableType);

                            // set updatable flag so we know this instance should 
                            // be updated instead of inserted when saving
                            tmp.Updateable = true;

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
                                {
                                    // query returned a column that was not in the model
                                    Console.WriteLine("The query returned a column (" + columnNameDb +
                                                      ") that was not present in the model (" + tableType.Name + ")");
                                    continue;
                                }

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

        /// <summary>
        /// Query data from the database.
        /// </summary>
        /// <typeparam name="T">Must be a class derived from DBTable</typeparam>
        /// <returns>A List (of T) of the query results</returns>
        public static List<T> Query<T>() where T : DBTable
        {
            return Query<T>("select * from " + typeof(T).Name.ToSnakeCase());
        }
        #endregion
    }
}
