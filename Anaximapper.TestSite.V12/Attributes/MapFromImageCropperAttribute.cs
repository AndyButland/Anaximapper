namespace Anaximapper.TestSite.Attributes
{
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Reflection;
    using Umbraco.Cms.Core.Media;
    using Umbraco.Cms.Core.PropertyEditors.ValueConverters;

    [AttributeUsage(AttributeTargets.Property)]
    public class MapFromImageCropperAttribute : Attribute, IMapFromAttribute
    {
        public string CropName { get; set; }

        public void SetPropertyValue<T>(object fromObject, PropertyInfo property, T model, MappingContext context)
        {
            var imageCropperValue = fromObject as ImageCropperValue;
            if (imageCropperValue == null)
            {
                return;
            }

            var imageUrlGenerator = context.HttpContext.RequestServices.GetRequiredService<IImageUrlGenerator>();
            property.SetValue(model, imageCropperValue.Src + imageCropperValue.GetCropUrl(CropName, imageUrlGenerator));
        }
    }
}
