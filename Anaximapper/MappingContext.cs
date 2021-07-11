using Microsoft.AspNetCore.Http;

namespace Anaximapper
{
    public class MappingContext
    {
        public MappingContext(IPublishedContentMapper mapper, HttpContext httpContext)
        {
            Mapper = mapper;
            HttpContext = httpContext;
        }

        public IPublishedContentMapper Mapper { get; }

        public HttpContext HttpContext { get; }
    }
}
