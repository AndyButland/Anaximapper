namespace Anaximapper.Extensions
{
    using Umbraco.Cms.Core.DependencyInjection;
    using Umbraco.Extensions;

    public static class UmbracoBuilderExtensions
    {
        public static IUmbracoBuilder AddAnaximapper(this IUmbracoBuilder builder)
        {
            builder.Services.AddUnique<IPublishedContentMapper, PublishedContentMapper>();
            builder.Services.AddUnique<IPropertyValueGetter, DefaultPropertyValueGetter>();

            return builder;
        }
    }
}
