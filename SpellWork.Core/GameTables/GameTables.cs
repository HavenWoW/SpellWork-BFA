using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace SpellWork.GameTables
{
    public static class GameTable<T> where T : class, GameTableRecord, new()
    {
        private static Dictionary<int, T> _records { get; } = new Dictionary<int, T>();

        public static T GetRecord(int key)
        {
            T instance;
            return _records.TryGetValue(key, out instance) ? instance : null;
        }

        public static void Open(string fileName)
        {
            using (var reader = new StreamReader(fileName))
            {
                var lineIndex = -1;
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                        break;

                    ++lineIndex;
                    if (lineIndex == 0)
                        continue;

                    var lineTokens = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    var record = new T();
                    var fieldIndex = 0;
                    foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        // ReSharper disable once SwitchStatementMissingSomeCases
                        switch (Type.GetTypeCode(field.FieldType))
                        {
                            case TypeCode.UInt32:
                                field.SetValue(record, uint.Parse(lineTokens[fieldIndex], CultureInfo.InvariantCulture));
                                break;
                            case TypeCode.Single:
                                field.SetValue(record, float.Parse(lineTokens[fieldIndex], CultureInfo.InvariantCulture));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        ++fieldIndex;
                    }

                    _records.Add(lineIndex, record);
                }
            }
        }
    }

    public interface GameTableRecord
    {
        float GetColumnForClass(int scalingClass);
    }
}
