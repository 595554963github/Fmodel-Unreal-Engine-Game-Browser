using System;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Objects;
using System.Diagnostics.CodeAnalysis;

namespace CUE4Parse.UE4.Assets.Utils
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class StructFallback : Attribute { }

    public static class StructFallbackUtil
    {
        public static ObjectMapper? ObjectMapper = new DefaultObjectMapper();

        public static object? MapToClass(this FStructFallback? fallback, Type type)
        {
            if (fallback == null || type == null)
            {
                return null;
            }

            object value;
            var dataConstructor = type.GetConstructor(new[] { typeof(FStructFallback) });
            if (dataConstructor != null)
            {
                // Let the constructor with FStructFallback assign the data
                value = dataConstructor.Invoke(new object[] { fallback });
            }
            else
            {
                // Or automatically map the values using reflection
                value = Activator.CreateInstance(type) ??
                    throw new InvalidOperationException($"无法创建类型 {type.Name} 的实例");
                ObjectMapper?.Map(fallback, value);
            }
            return value;
        }
    }

    public abstract class ObjectMapper
    {
        public abstract void Map(IPropertyHolder src, [DisallowNull] object dst);
    }

    public class DefaultObjectMapper : ObjectMapper
    {
        public override void Map(IPropertyHolder src, [DisallowNull] object dst)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));

            // TODO
        }
    }
}