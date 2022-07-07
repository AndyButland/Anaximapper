using Anaximapper.Attributes;
using System.Collections.Generic;
using Umbraco.Cms.Core.Models.PublishedContent;

namespace Anaximapper.TestSite.Models
{
    public class CategoryListViewModel
    {
        public List<CategoryDetail> PickedCategories { get; set; } = new List<CategoryDetail>();

        public class CategoryDetail
        {
            public string Name { get; set; }

            [PropertyMapping(FallbackMethods = new[] { Fallback.Language })]
            public string CategoryName { get; set; }
        }
    }
}