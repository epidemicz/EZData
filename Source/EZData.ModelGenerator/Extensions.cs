using System.Text.RegularExpressions;

namespace EZData.ModelGenerator
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
            return Regex.Replace(name, @"^(\w)|_(\w)", m => m.Value.ToUpper()).Replace("_", "");
        }
    }
}
