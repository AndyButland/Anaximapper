namespace Anaximapper
{
    using Umbraco.Cms.Core.Models.PublishedContent;
    using Umbraco.Cms.Core.Routing;

    /// <summary>
    /// Provides a custom mapping of a single property of an IPublishedContent to an object
    /// </summary>
    /// <param name="mapper">The instance of IUmbracoMapper performing the mapping</param>
    /// <param name="publishedValueFallback">The published value fallback.</param>
    /// <param name="publishedUrlProvider">The published URL provider.</param>
    /// <param name="content">Instance of IPublishedContent</param>
    /// <param name="propertyName">Name of the property to map</param>
    /// <param name="fallback">Fallback method(s) to use when content not found</param>
    /// <returns>Instance of object containing mapped data</returns>
    public delegate object CustomMapping(IPublishedContentMapper mapper, IPublishedElement content, IPublishedValueFallback publishedValueFallback, IPublishedUrlProvider publishedUrlProvider, string propertyName, Fallback fallback);

    /// <summary>
    /// Provides a custom mapping from a dictionary object to an object
    /// </summary>
    /// <param name="mapper">The instance of IUmbracoMapper performing the mapping</param>
    /// <param name="publishedValueFallback">The published value fallback.</param>
    /// <param name="publishedUrlProvider">The published URL provider.</param>
    /// <param name="value">Instance of the object to map from</param>
    /// <returns>Instance of object containing mapped data</returns>
    public delegate object CustomObjectMapping(IPublishedContentMapper mapper, IPublishedValueFallback publishedValueFallback, IPublishedUrlProvider publishedUrlProvider, object value);
}
