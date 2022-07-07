using System;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Anaximapper.TestSite.PropertyValueGetters
{
    public class CategoriesGetter : IPropertyValueGetter
    {
        public object GetPropertyValue(IPublishedElement content, IPublishedValueFallback publishedValueFallback, string alias, string culture, string segment, Fallback fallback)
        {
            throw new NotImplementedException();
        }
    }
}