namespace Anaximapper.TestSite.Controllers
{
    using Anaximapper.TestSite.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.Extensions.Logging;
    using Umbraco.Cms.Core.Web;
    using Umbraco.Cms.Web.Common.Controllers;

    public class NewsLandingPageController : RenderController
    {
        private readonly IPublishedContentMapper _mapper;

        public NewsLandingPageController(
            ILogger<NewsLandingPageController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedContentMapper mapper)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _mapper = mapper;
        }

        public IActionResult NewsLandingPage()
        {
            var model = new NewsLandingPageViewModel();
            _mapper.Map(CurrentPage, model);

            return CurrentTemplate(model);
        }
    }
}
