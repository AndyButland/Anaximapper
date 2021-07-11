namespace Anaximapper.Extensions
{
    using Microsoft.AspNetCore.Html;
    using System;
    using System.Linq;
    using System.Reflection;

    public static class TypeExtensions
    {
        /// <summary>
        /// Determine whether a type is simple (String, Decimal, DateTime, etc) 
        /// or complex (i.e. custom class with public properties and methods).
        /// </summary>
        /// <see cref="http://stackoverflow.com/questions/2442534/how-to-test-if-type-is-primitive"/>
        public static bool IsSimpleType(
            this Type type)
        {
            return
                type.IsValueType ||
                type.IsPrimitive ||
                new[]
                    {
                        typeof(string),
                        typeof(HtmlString),
                        typeof(decimal),
                        typeof(DateTime),
                        typeof(DateTimeOffset),
                        typeof(TimeSpan),
                        typeof(Guid)
                    }.Contains(type) ||
                Convert.GetTypeCode(type) != TypeCode.Object;
        }

        public static MethodInfo GetMethodFromTypeAndMethodName(this IReflect type, string methodName)
        {
            return type.GetMethod(methodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }
}
