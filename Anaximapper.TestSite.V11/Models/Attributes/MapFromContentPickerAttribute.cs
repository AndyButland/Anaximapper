namespace Anaximapper.TestSite.Models.Attributes
{
    using System;
    using System.Reflection;
    using Umbraco.Cms.Core.Models.PublishedContent;

    [AttributeUsage(AttributeTargets.Property)]
    public class MapFromContentPickerAttribute : Attribute, IMapFromAttribute
    {
        public void SetPropertyValue<T>(object fromObject, PropertyInfo property, T model, MappingContext context)
        {
            var method = GetType().GetMethod("GetInstance", BindingFlags.NonPublic | BindingFlags.Instance);
            var genericMethod = method.MakeGenericMethod(property.PropertyType);
            var item = genericMethod.Invoke(this, new[] { fromObject, context.Mapper });
            property.SetValue(model, item);
        }

        private T GetInstance<T>(object fromObject, IPublishedContentMapper mapper)
            where T : class
        {
            T instance = default(T);
            if (fromObject != null)
            {
                var content = fromObject as IPublishedContent;
                if (content != null)
                {
                    instance = Activator.CreateInstance<T>();
                    mapper.Map(content, instance);
                }

            }

            return instance;
        }
    }
}