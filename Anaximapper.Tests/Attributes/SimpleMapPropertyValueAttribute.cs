namespace Anaximapper.Tests.Attributes
{
    using Anaximapper;
    using System;
    using System.Reflection;

    [AttributeUsage(AttributeTargets.Property)]
    public class SimpleMapPropertyValueAttribute : Attribute, IMapFromAttribute
    {
        public void SetPropertyValue<T>(object fromObject, PropertyInfo property, T model, IPublishedContentMapper mapper)
        {
            var rawValue = fromObject as string;
            property.SetValue(model, rawValue);
        }
    }
}
