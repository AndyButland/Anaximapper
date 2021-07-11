namespace Anaximapper
{
    using Umbraco.Cms.Core.Models.PublishedContent;
    using Umbraco.Extensions;

    public class DefaultPropertyValueGetter : IPropertyValueGetter
    {
        public virtual object GetPropertyValue(IPublishedElement content, IPublishedValueFallback publishedValueFallback, string alias, string culture, string segment, Fallback fallback)
        {
            // We need to cast to IPublishedContent if that's what we are mapping from, such that the fallback methods are
            // handled correctly.
            var publishedContent = content as IPublishedContent;
            var cultureOrNull = string.IsNullOrEmpty(culture) ? null : culture;
            return publishedContent != null
                ? publishedContent.Value(publishedValueFallback, alias, cultureOrNull, segment, fallback)
                : content.Value(publishedValueFallback, alias, cultureOrNull, segment, fallback);
        }
    }
}
