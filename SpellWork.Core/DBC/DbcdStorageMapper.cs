using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DBCD;

namespace SpellWork.DBC
{
    public static class DbcdStorageMapper
    {
        public static Storage<T> Map<T>(IDBCDStorage source) where T : class, new()
        {
            var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(f => f.MetadataToken).ToArray();
            var available = new HashSet<string>(source.AvailableColumns, StringComparer.Ordinal);

            var columns = new string[fields.Length];
            var isIndex = new bool[fields.Length];
            for (var i = 0; i < fields.Length; i++)
            {
                isIndex[i] = fields[i].GetCustomAttribute<IndexAttribute>() != null;
                columns[i] = isIndex[i] ? null : ResolveColumn(fields[i], available);
            }

            var result = new Storage<T>();
            foreach (KeyValuePair<int, DBCDRow> kv in (IDictionary<int, DBCDRow>)source)
            {
                var obj = new T();
                for (var i = 0; i < fields.Length; i++)
                {
                    if (isIndex[i])
                        fields[i].SetValue(obj, Coerce(kv.Key, fields[i].FieldType));
                    else if (available.Contains(columns[i]))
                        fields[i].SetValue(obj, Coerce(kv.Value[columns[i]], fields[i].FieldType));
                }
                result.Add(kv.Key, obj);
            }
            return result;
        }

        internal static object Coerce(object value, Type targetType)
        {
            if (value == null || value is DBNull)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType();
                if (value is Array src)
                {
                    var dst = Array.CreateInstance(elementType, src.Length);
                    for (var i = 0; i < src.Length; i++)
                        dst.SetValue(Coerce(src.GetValue(i), elementType), i);
                    return dst;
                }
                var single = Array.CreateInstance(elementType, 1);
                single.SetValue(Coerce(value, elementType), 0);
                return single;
            }

            if (targetType.IsInstanceOfType(value))
                return value;
            return Convert.ChangeType(value, targetType);
        }

        internal static string ResolveColumn(FieldInfo field, ICollection<string> available)
        {
            var attr = field.GetCustomAttribute<ColumnAttribute>();
            if (attr != null)
                return attr.Name;
            if (available.Contains(field.Name))
                return field.Name;
            if (available.Contains(field.Name + "_lang"))
                return field.Name + "_lang";
            return field.Name;
        }
    }
}
