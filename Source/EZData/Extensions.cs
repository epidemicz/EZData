using System.Text.RegularExpressions;

namespace EZData
{
    public static class Extensions
    {
        /// <summary>
        /// Converts PascalCase to snake_case.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string ToSnakeCase(this string name)
        {
            return Regex.Replace(name, @"(\p{Ll}|\p{Lu}|[0-9])(\p{Lu})", "$1_$2").ToLower();
        }

        /// <summary>
        /// Converts snake_case to PascalCase.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string ToPascalCase(this string name)
        {
            // original ** SLOW, VERY SLOW **
            //return Regex.Replace(name, @"^(\w)|_(\w)", m => m.Value.ToUpper()).Replace("_", "");

            string result = string.Empty;

            // find the underscores
            string[] parts = name.Split('_');

            int partsCount = parts.Length;

            if (partsCount == 1)
            {
                result = CapFirstLetter(name);
            }
            else
            {
                for (int i = 0; i < partsCount; i++)
                    result += CapFirstLetter(parts[i]);   
            }

            return result;
        }

        private static string CapFirstLetter(string str)
        {
            var first = str.Substring(0, 1);
            var rest = str.Substring(1);
            return first.ToUpper() + rest;
        }
    }
}
