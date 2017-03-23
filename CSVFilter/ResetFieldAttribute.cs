using System;
using System.Linq;
using System.Reflection;

namespace CSVFilter
{
    /// <summary>
    /// Indicates that the field should be cleared on reset
    /// </summary>
    public class ResetFieldAttribute: Attribute
    {
        /// <summary>
        /// Resets properties in an object marked with this attribute. Assigned null to
        /// everything but strings, which it sets to string.empty
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        public static void Reset<T>(T obj)
        {
            typeof(T).GetProperties()
                     .Where(p => p.GetCustomAttribute(typeof(ResetFieldAttribute)) != null).ToList()
                     .ForEach(p => {
                         if (p.PropertyType == typeof(string))
                             p.SetValue(obj, string.Empty);
                         else
                             p.SetValue(obj, null);
                     });
        }

        public ResetFieldAttribute() { }
    }
}