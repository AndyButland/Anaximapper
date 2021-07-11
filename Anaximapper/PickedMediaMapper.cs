namespace Anaximapper
{
    using Anaximapper.Models;
    using System.Collections.Generic;
    using System.Linq;
    using Umbraco.Cms.Core.Models.PublishedContent;
    using Umbraco.Cms.Core.Routing;
    using Umbraco.Extensions;

    public static class PickedMediaMapper
    {
        /// <summary>
        /// Native mapper for mapping a multiple  media picker property
        /// </summary>
        /// <param name="mapper">The mapper</param>
        /// <param name="contentToMapFrom">Umbraco content item to map from</param>
        /// <param name="publishedValueFallback">The published value fallback.</param> 
        /// <param name="publishedUrlProvider">The published URL provider.</param>
        /// <param name="propName">Name of the property to map</param>
        /// <param name="fallback">Fallback method(s) to use when content not found</param>
        /// <returns>MediaFile instance</returns>
        public static object MapMediaFileCollection(
            IPublishedContentMapper mapper,
            IPublishedElement contentToMapFrom,
            IPublishedValueFallback publishedValueFallback,
            IPublishedUrlProvider publishedUrlProvider,
            string propName,
            Fallback fallback)
        {
            // With V8 will get IPublishedContent
            var mediaCollection = contentToMapFrom.Value<IEnumerable<IPublishedContent>>(publishedValueFallback, propName, fallback: fallback);
            if (mediaCollection == null)
            {
                // Also check for single IPublishedContent (which could get if multiple media disabled)
                var media = contentToMapFrom.Value<IPublishedContent>(propName, fallback: fallback);
                if (media != null)
                {
                    mediaCollection = new List<IPublishedContent> { media };
                }
            }

            return mediaCollection != null ? GetMediaFileCollection(mediaCollection, publishedValueFallback, publishedUrlProvider, mapper.AssetsRootUrl) : null;
        }

        /// <summary>
        /// Native mapper for mapping a media picker property
        /// </summary>
        /// <param name="mapper">The mapper</param>
        /// <param name="contentToMapFrom">Umbraco content item to map from</param>
        /// <param name="publishedValueFallback">The published value fallback.</param> 
        /// <param name="publishedUrlProvider">The published URL provider.</param>
        /// <param name="propName">Name of the property to map</param>
        /// <param name="fallback">Fallback method(s) to use when content not found</param>
        /// <returns>MediaFile instance</returns>
        public static object MapMediaFile(
            IPublishedContentMapper mapper,
            IPublishedElement contentToMapFrom, 
            IPublishedValueFallback publishedValueFallback,
            IPublishedUrlProvider publishedUrlProvider,
            string propName,
            Fallback fallback)
        {
            // With V8 will get IPublishedContent
            var media = contentToMapFrom.Value<IPublishedContent>(publishedValueFallback, propName, fallback: fallback);
            return media != null ? GetMediaFile(media, publishedValueFallback, publishedUrlProvider, mapper.AssetsRootUrl) : null;
        }

        /// <summary>
        /// Helper to convert a collection of IPublishedContent media item into a collection of MediaFile objects
        /// </summary>
        /// <param name="mediaCollection">Selected media</param>
        /// <param name="publishedValueFallback">The published value fallback.</param> 
        /// <param name="publishedUrlProvider">The published URL provider.</param>
        /// <param name="rootUrl">Root Url for mapping full path to media</param>
        /// <returns>MediaFile instance</returns>
        private static IEnumerable<MediaFile> GetMediaFileCollection(IEnumerable<IPublishedContent> mediaCollection, IPublishedValueFallback publishedValueFallback, IPublishedUrlProvider publishedUrlProvider, string rootUrl)
        {
            return mediaCollection.Select(media => GetMediaFile(media, publishedValueFallback, publishedUrlProvider, rootUrl));
        }

        /// <summary>
        /// Helper to convert an IPublishedContent media item into a MediaFile object
        /// </summary>
        /// <param name="media">Selected media</param>
        /// <param name="publishedValueFallback">The published value fallback.</param> 
        /// <param name="publishedUrlProvider">The published URL provider.</param>
        /// <param name="rootUrl">Root Url for mapping full path to media</param>
        /// <returns>MediaFile instance</returns>
        private static MediaFile GetMediaFile(IPublishedContent media, IPublishedValueFallback publishedValueFallback, IPublishedUrlProvider publishedUrlProvider, string rootUrl)
        {
            var mediaFile = new MediaFile
            {
                Id = media.Id,
                Name = media.Name,
                Url = media.Url(publishedUrlProvider),
                DomainWithUrl = rootUrl + media.Url(publishedUrlProvider),
                DocumentTypeAlias = media.ContentType.Alias,
                Width = media.Value<int>(publishedValueFallback, "umbracoWidth"),
                Height = media.Value<int>(publishedValueFallback, "umbracoHeight"),
                Size = media.Value<int>(publishedValueFallback, "umbracoBytes"),
                FileExtension = media.Value<string>(publishedValueFallback, "umbracoExtension"),
                AltText = media.Value<string>(publishedValueFallback, "altText")
            };

            return mediaFile;
        }
    }
}
