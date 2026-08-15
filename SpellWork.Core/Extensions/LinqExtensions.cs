using System;
using System.Linq;
using System.Reflection;

namespace SpellWork.Extensions
{
    public enum CompareType
    {
        [FullName("x != y")]
        NotEqual,
        [FullName("x == y")]
        Equal,

        [FullName("x > y")]
        GreaterThan,
        [FullName("x >= y")]
        GreaterOrEqual,
        [FullName("x < y")]
        LowerThan,
        [FullName("x <= y")]
        LowerOrEqual,

        [FullName("x & y == y")]
        AndStrict,
        [FullName("x & y != 0")]
        And,
        [FullName("x & y == 0")]
        NotAnd,

        [FullName("x Starts With y")]
        StartsWith,
        [FullName("x Ends With y")]
        EndsWith,
        [FullName("x Contains y")]
        Contains,
    }

    public static class LinqExtensions
    {
        /// <summary>
        /// Compares two values object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entry"></param>
        /// <param name="field">Value Type is MemberInfo</param>
        /// <param name="val"></param>
        /// <param name="compareType"></param>
        /// <returns></returns>
        public static bool CreateFilter<T>(this T entry, object field, object val, CompareType compareType)
        {
            var basicValue = GetValue(entry, (MemberInfo)field);

            switch (basicValue.GetType().Name)
            {
                case "UInt32":
                    return Compare(basicValue.ToUInt32(), val.ToUInt32(), compareType);
                case "Int32":
                    return Compare(basicValue.ToInt32(), val.ToInt32(), compareType);
                case "Single":
                    return Compare(basicValue.ToFloat(), val.ToFloat(), compareType);
                case "UInt64":
                    return Compare(basicValue.ToUlong(), val.ToUlong(), compareType);
                case "String":
                    return Compare(basicValue.ToString(), val.ToString(), compareType);
                case @"UInt32[]":
                {
                    var valUint = val.ToUInt32();
                    return ((uint[])basicValue).Any(el => Compare(el, valUint, compareType));
                }
                case @"Int32[]":
                {
                    var valInt = val.ToInt32();
                    return ((int[])basicValue).Any(el => Compare(el, valInt, compareType));
                }
                case @"Single[]":
                {
                    var valFloat = val.ToFloat();
                    return ((float[])basicValue).Any(el => Compare(el, valFloat, compareType));
                }
                case @"UInt64[]":
                {
                    var valUlong = val.ToUlong();
                    return ((ulong[])basicValue).Any(el => Compare(el, valUlong, compareType));
                }
                default:
                    return false;
            }
        }

        #region Specific Compares

        private static bool Compare(string baseValue, string value, CompareType compareType)
        {
            switch (compareType)
            {
                case CompareType.StartsWith:
                    return baseValue.StartsWith(value);
                case CompareType.EndsWith:
                    return baseValue.EndsWith(value);

                case CompareType.Contains:
                    return baseValue.ContainsText(value);

                case CompareType.NotEqual:
                    return !baseValue.Equals(value, StringComparison.CurrentCultureIgnoreCase);
                default:
                    return baseValue.Equals(value, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        private static bool Compare(float baseValue, float value, CompareType compareType)
        {
            switch (compareType)
            {
                case CompareType.GreaterOrEqual:
                    return baseValue >= value;
                case CompareType.GreaterThan:
                    return baseValue > value;
                case CompareType.LowerOrEqual:
                    return baseValue <= value;
                case CompareType.LowerThan:
                    return baseValue < value;

                case CompareType.NotEqual:
                    return baseValue != value;
                default:
                    return baseValue == value;
            }
        }

        private static bool Compare(ulong baseValue, ulong value, CompareType compareType)
        {
            switch (compareType)
            {
                case CompareType.GreaterOrEqual:
                    return baseValue >= value;
                case CompareType.GreaterThan:
                    return baseValue > value;
                case CompareType.LowerOrEqual:
                    return baseValue <= value;
                case CompareType.LowerThan:
                    return baseValue < value;

                case CompareType.AndStrict:
                    return (baseValue & value) == value;
                case CompareType.And:
                    return (baseValue & value) != 0;
                case CompareType.NotAnd:
                    return (baseValue & value) == 0;

                case CompareType.NotEqual:
                    return baseValue != value;
                default:
                    return baseValue == value;
            }
        }

        private static bool Compare(int baseValue, int value, CompareType compareType)
        {
            switch (compareType)
            {
                case CompareType.GreaterOrEqual:
                    return baseValue >= value;
                case CompareType.GreaterThan:
                    return baseValue > value;
                case CompareType.LowerOrEqual:
                    return baseValue <= value;
                case CompareType.LowerThan:
                    return baseValue < value;

                case CompareType.AndStrict:
                    return (baseValue & value) == value;
                case CompareType.And:
                    return (baseValue & value) != 0;
                case CompareType.NotAnd:
                    return (baseValue & value) == 0;

                case CompareType.NotEqual:
                    return baseValue != value;
                default:
                    return baseValue == value;
            }
        }

        private static bool Compare(uint baseValue, uint value, CompareType compareType)
        {
            switch (compareType)
            {
                case CompareType.GreaterOrEqual:
                    return baseValue >= value;
                case CompareType.GreaterThan:
                    return baseValue > value;
                case CompareType.LowerOrEqual:
                    return baseValue <= value;
                case CompareType.LowerThan:
                    return baseValue < value;

                case CompareType.AndStrict:
                    return (baseValue & value) == value;
                case CompareType.And:
                    return (baseValue & value) != 0;
                case CompareType.NotAnd:
                    return (baseValue & value) == 0;

                case CompareType.NotEqual:
                    return baseValue != value;
                default:
                    return baseValue == value;
            }
        }

        #endregion

        private static object GetValue<T>(T entry, MemberInfo field)
        {
            var fieldInfo = field as FieldInfo;
            if (fieldInfo != null)
                return fieldInfo.GetValue(entry);

            var propertyInfo = field as PropertyInfo;
            return propertyInfo?.GetValue(entry, null);
        }
    }
}
