namespace Anaximapper.TestSite.Controllers
{
    using Anaximapper.TestSite.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using Umbraco.Cms.Core.Models.PublishedContent;
    using Umbraco.Cms.Core.Web;
    using Umbraco.Cms.Web.Common.Controllers;
    using Umbraco.Extensions;

    public class CategoryListController : RenderController
    {
        private readonly IPublishedContentMapper _mapper;

        public CategoryListController(
            ILogger<CategoryListController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedContentMapper mapper)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _mapper = mapper;
        }

        public IActionResult CategoryList(string culture = "")
        {
            var model = new CategoryListViewModel();
            _mapper.Map(CurrentPage, model)
                .MapCollection(CurrentPage.Value<IEnumerable<IPublishedContent>>("pickedCategories"), model.PickedCategories, culture);
            return CurrentTemplate(model);
        }
    }
}
