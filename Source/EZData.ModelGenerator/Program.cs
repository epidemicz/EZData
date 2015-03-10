using System;
using System.Data.OleDb;
using System.IO;
using System.Reflection;
using System.Text;
using Devart.Data.Oracle;

namespace EZData.ModelGenerator
{
    class Program
    {        
        static OracleConnection cn;

        static void Main(string[] args)
        {
            try
            {
                string user = null;
                string pass = null;
                string db = null;

                var version = Assembly.GetExecutingAssembly().GetName().Version;

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("EZData Model Generator v" + version);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("--------------------------");

                Console.WriteLine(Directory.GetCurrentDirectory());

                Console.Write("DB User: "); user = Console.ReadLine();
                Console.Write("DB Pass: "); pass = Console.ReadLine();
                Console.Write("Database: "); db = Console.ReadLine();

                string connectionString = "Data Source=" + db + ";User Id=" + user + ";Password=" + pass + ";";

                cn = new OracleConnection(connectionString);
                cn.Open();

                Generate();
            }
            catch (Exception ex) { throw new Exception(ex.Message, ex); }
        }

        private static void Generate()
        {
            bool quit = false;

            while (!quit)
            {
                Console.WriteLine("--------------------------");
                Console.WriteLine("Enter a table name and press enter.");
                Console.WriteLine("Enter q to quit.");
                Console.WriteLine("--------------------------");

                Console.Write("Table Name: "); string input = Console.ReadLine();

                if (input == "q")
                {
                    quit = true;
                    break;
                }

                try
                {
                    // attempt to locate this table
                    using (var cmd = new OracleCommand("select * from " + input, cn))
                    {
                        using (var dr = cmd.ExecuteReader(System.Data.CommandBehavior.KeyInfo))
                        {

                            // getting schema table to be able to find primary keys
                            var schemaTable = dr.GetSchemaTable();

                            StringBuilder outputCSharp = new StringBuilder();
                            StringBuilder outputVB = new StringBuilder();

                            outputCSharp.Append("using System;" + Environment.NewLine + Environment.NewLine);
                            outputCSharp.Append("class " + input.ToPascalCase() + " : EZData.DBTable" + Environment.NewLine);
                            outputCSharp.Append("{" + Environment.NewLine);

                            outputVB.Append("Imports System" + Environment.NewLine + Environment.NewLine);
                            outputVB.Append("Class " + input.ToPascalCase() + Environment.NewLine);
                            outputVB.Append("\tInherits EZData.DBTable" + Environment.NewLine + Environment.NewLine);

                            // read table
                            dr.Read();

                            // loop through each field and generate properties
                            for (int i = 0; i < dr.FieldCount; i++)
                            {
                                // get property name
                                string column = dr.GetName(i).ToLower().ToPascalCase();

                                // skip rowid column, it is an artifact of the KeyInfo command behavior
                                if (column == "Rowid") continue;

                                // generates strings unless it is DateTime type
                                string fieldType = dr.GetFieldType(i).Name;

                                // check to see if this is marked as a key in the meta data
                                bool key = (bool)schemaTable.Rows[i][12];

                                // if this is a key, mark with the PrimaryKey attribute
                                if (key)
                                {
                                    outputCSharp.Append("\t[EZData.PrimaryKey]" + Environment.NewLine);

                                    outputVB.Append("\t<EZData.PrimaryKey>");
                                }

                                outputCSharp.Append("\tpublic " + fieldType + " " + column + " { get; set; }" + Environment.NewLine);

                                outputVB.Append("\tPublic Property " + column + " As " + fieldType + Environment.NewLine);
                            }

                            outputCSharp.Append("}");

                            outputVB.Append("End Class");

                            // dump file of output in this directory
                            string fileNameCSharp = input.ToPascalCase() + ".cs";
                            string fileNameVB = input.ToPascalCase() + ".vb";

                            File.WriteAllText(input.ToPascalCase() + ".cs", outputCSharp.ToString());
                            File.WriteAllText(input.ToPascalCase() + ".vb", outputVB.ToString());

                            if (File.Exists(fileNameCSharp))
                                Console.WriteLine("Model " + fileNameCSharp + " has been generated.");

                            if (File.Exists(fileNameVB))
                                Console.WriteLine("Model " + fileNameVB + " has been generated.");
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine(ex.Message);  }
            }
        }
    }
}
