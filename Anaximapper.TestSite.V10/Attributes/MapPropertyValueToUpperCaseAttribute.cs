namespace Anaximapper.TestSite.Attributes
{
    using System;
    using System.Reflection;

    [AttributeUsage(AttributeTargets.Property)]
    public class MapPropertyValueToUpperCaseAttribute : Attribute, IMapFromAttribute
    {
        public void SetPropertyValue<T>(object fromObject, PropertyInfo property, T model, MappingContext context)
        {
            var rawValue = fromObject as string;
            property.SetValue(model, rawValue?.ToUpperInvariant());
        }
    }
}
