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
                    {
                        try
                        {
                            fields[i].SetValue(obj, Coerce(kv.Value[columns[i]], fields[i].FieldType));
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new InvalidOperationException(
                                typeof(T).Name + "." + fields[i].Name + " <- column '" + columns[i] + "': " + ex.Message, ex);
                        }
                    }
                }
                result.Add(kv.Key, obj);
            }
            return result;
        }

        internal static object Coerce(object value, Type targetType)
        {
            if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType();
                if (value == null || value is DBNull)
                    return Array.CreateInstance(elementType, 0);
                if (value is Array src)
                {
                    var dst = Array.CreateInstance(elementType, src.Length);
                    for (var i = 0; i < src.Length; i++)
                        dst.SetValue(Coerce(src.GetValue(i), elementType), i);
                    return dst;
                }
                throw new InvalidOperationException(
                    "Layout mismatch: target is " + targetType.Name + " but column value is " + value.GetType().Name + ".");
            }

            if (value == null || value is DBNull)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType.IsInstanceOfType(value))
                return value;

            if (targetType.IsEnum)
                return Enum.ToObject(targetType, Coerce(value, Enum.GetUnderlyingType(targetType)));

            if (IsIntegral(targetType) && IsIntegral(value.GetType()))
            {
                unchecked
                {
                    long wide = value is ulong u ? (long)u : Convert.ToInt64(value is char c ? (int)c : value);
                    switch (Type.GetTypeCode(targetType))
                    {
                        case TypeCode.SByte: return (sbyte)wide;
                        case TypeCode.Byte: return (byte)wide;
                        case TypeCode.Int16: return (short)wide;
                        case TypeCode.UInt16: return (ushort)wide;
                        case TypeCode.Int32: return (int)wide;
                        case TypeCode.UInt32: return (uint)wide;
                        case TypeCode.Int64: return wide;
                        case TypeCode.UInt64: return (ulong)wide;
                    }
                }
            }

            return Convert.ChangeType(value, targetType);
        }

        private static bool IsIntegral(Type t)
        {
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.SByte: case TypeCode.Byte:
                case TypeCode.Int16: case TypeCode.UInt16:
                case TypeCode.Int32: case TypeCode.UInt32:
                case TypeCode.Int64: case TypeCode.UInt64:
                    return !t.IsEnum;
                default:
                    return false;
            }
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
