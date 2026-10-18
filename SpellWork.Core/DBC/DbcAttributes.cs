using System;

namespace SpellWork.DBC
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class IndexAttribute : Attribute
    {
        public bool NonInline { get; }
        public IndexAttribute(bool nonInline = false) { NonInline = nonInline; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class CardinalityAttribute : Attribute
    {
        public int Count { get; }
        public CardinalityAttribute(int count) { Count = count; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ColumnAttribute : Attribute
    {
        public string Name { get; }
        public ColumnAttribute(string name) { Name = name; }
    }
}
