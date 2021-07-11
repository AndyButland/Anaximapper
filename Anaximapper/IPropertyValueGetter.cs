namespace Anaximapper
{
    using Umbraco.Cms.Core.Models.PublishedContent;

    public interface IPropertyValueGetter
    {
        object GetPropertyValue(IPublishedElement content, IPublishedValueFallback publishedValueFallback, string alias, string culture, string segment, Fallback fallback);
    }
}
