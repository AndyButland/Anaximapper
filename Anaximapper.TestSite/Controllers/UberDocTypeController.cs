namespace Anaximapper.TestSite.Controllers
{
    using Anaximapper.TestSite.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewEngines;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Xml.Linq;
    using Umbraco.Cms.Core.Models.PublishedContent;
    using Umbraco.Cms.Core.Web;
    using Umbraco.Cms.Web.Common.Controllers;
    using Umbraco.Extensions;

    public class UberDocTypeController : RenderController
    {
        private readonly IPublishedContentMapper _mapper;

        public UberDocTypeController(
            ILogger<UberDocTypeController> logger,
            ICompositeViewEngine compositeViewEngine,
            IUmbracoContextAccessor umbracoContextAccessor,
            IPublishedContentMapper mapper)
            : base(logger, compositeViewEngine, umbracoContextAccessor)
        {
            _mapper = mapper;
        }

        public override IActionResult Index()
        {
            // Get related and inner content
            var countryNodes = GetRelatedNodes();
            var nestedItems = CurrentPage.Value<IEnumerable<IPublishedElement>>("nestedItems");

            // Create view model and run mapping
            var model = new UberDocTypeViewModel();
            _mapper.Map(CurrentPage, model, propertyMappings: new Dictionary<string, PropertyMapping> 
                    { 
                        { 
                            "CreatedOn", new PropertyMapping 
                                { 
                                    SourceProperty = "CreateDate" 
                                } 
                        }, 
                        { 
                            "SelectedComment", new PropertyMapping 
                                { 
                                    SourceRelatedProperty = "text" 
                                } 
                        }, 
                        { 
                            "SelectedCommentId", new PropertyMapping 
                                { 
                                    SourceProperty = "selectedComment",
                                    SourceRelatedProperty = "Id" 
                                } 
                        }, 
                        { 
                            "SelectedCommentModel", new PropertyMapping 
                                { 
                                    SourceProperty = "selectedComment",
                                } 
                        }, 
                        { 
                            "ConcatenatedValue", new PropertyMapping 
                                { 
                                    SourcePropertiesForConcatenation = new string[] { "heading", "starRating", "Id", "Url" },
                                    ConcatenationSeperator = ", ",
                                } 
                        }, 
                        { 
                            "ConditionalValueMet", new PropertyMapping 
                                { 
                                    SourceProperty = "heading",
                                    MapIfPropertyMatches = new KeyValuePair<string, string>("isApproved", "true"),
                                } 
                        }, 
                        { 
                            "ConditionalValueNotMet", new PropertyMapping 
                                { 
                                    SourceProperty = "heading",
                                    MapIfPropertyMatches = new KeyValuePair<string, string>("isApproved", "0"),
                                } 
                        },
                        { 
                            "CoalescedValue", new PropertyMapping 
                                { 
                                    SourcePropertiesForCoalescing = new string[] { "emptyField", "Name" },
                                } 
                        }, 
                        { 
                            "UpperCaseHeading", new PropertyMapping 
                                { 
                                    SourceProperty = "heading",
                                    StringValueFormatter = x => 
                                    {
                                        return x.ToUpper();
                                    }
                                } 
                        }, 
                        { 
                            "FormattedCreatedOnDate", new PropertyMapping 
                                { 
                                    SourceProperty = "CreateDate",
                                    StringValueFormatter = x => DateTime.Parse(x).ToString("dd MMMM, yyyy")
                                } 
                        }, 
                        { 
                            "NonMapped", new PropertyMapping 
                                { 
                                    DefaultValue = "Default text",
                                } 
                        }, 
                        { 
                            "NonMappedFromEmptyString", new PropertyMapping 
                                { 
                                    SourceProperty = "emptyField",
                                    DefaultValue = "Default text",
                                } 
                        }, 
                        { 
                            "HeadingWithDefaultValue", new PropertyMapping 
                                { 
                                    SourceProperty = "Heading",
                                    DefaultValue = "Default text",
                                } 
                        }, 
                        { 
                            "DocumentTypeAlias", new PropertyMapping 
                                { 
                                    Ignore = true,
                                } 
                        }, 
                        { 
                            "DictionaryValue", new PropertyMapping 
                                { 
                                    DictionaryKey = "testKey" 
                                } 
                        },
                        {
                            "NestedItems2", new PropertyMapping
                                {
                                    SourceProperty = "nestedItems"
                                }
                        },
                    })
                .MapCollection(CurrentPage.Children.Where(x => x.IsDocumentType("Comment")), model.Comments,
                    propertyMappings: new Dictionary<string, PropertyMapping>
                        { 
                            { 
                                "ParentPage", new PropertyMapping 
                                    { 
                                        SourceProperty = "Name", 
                                        LevelsAbove = 1 
                                    }
                            },
                            {
                                "Country", new PropertyMapping 
                                    { 
                                        SourceRelatedProperty = "Name", 
                                    } 
                            },
                            {
                                "MediaPickedImage", new PropertyMapping
                                    {
                                        MapRecursively = true
                                    }
                            },
                            {
                                "StarRating", new PropertyMapping
                                    {
                                        MapRecursively = true
                                    }
                            } 
                        })
                .MapCollection(countryNodes, model.Countries)
                .Map(GetSingleXml(), model, new Dictionary<string, PropertyMapping> { { "SingleValueFromXml", new PropertyMapping { SourceProperty = "Day" } }, })
                .MapCollection(GetCollectionXml(), model.CollectionFromXml, null, "Month")
                .Map(GetSingleDictionary(), model, propertyMappings: new Dictionary<string, PropertyMapping> { { "SingleValueFromDictionary", new PropertyMapping { SourceProperty = "Animal" } }, })
                .MapCollection(GetCollectionDictionary(), model.CollectionFromDictionary)
                .Map(GetSingleJson(), model, new Dictionary<string, PropertyMapping> { { "SingleValueFromJson", new PropertyMapping { SourceProperty = "Name" } }, })
                .MapCollection(GetCollectionJson(), model.CollectionFromJson)
                .Map(CurrentPage, model.SubModel)
                .Map(CurrentPage, model.WelcomeTextEnglish)
                .Map(CurrentPage, model.WelcomeTextItalian, "it",
                    new Dictionary<string, PropertyMapping>
                        {
                            { "HelloText", new PropertyMapping 
                                { 
                                    FallbackMethods = Fallback.ToLanguage.ToArray() 
                                } 
                            }
                        })
                .MapCollection(nestedItems, model.NestedItems, clearCollectionBeforeMapping: false);

            return CurrentTemplate(model);
        }

        public IActionResult UberDocTypeWithAttribute()
        {
            // Get related content
            var countryNodes = GetRelatedNodes();
            var nestedItems = CurrentPage.Value<IEnumerable<IPublishedElement>>("nestedItems");

            // Create view model and run mapping
            var model = new UberDocTypeViewModelWithAttribute();
            MapToViewModelWithAttributes(countryNodes, nestedItems, model);

            return CurrentTemplate(model);
        }

        private void MapToViewModelWithAttributes(IEnumerable<IPublishedContent> countryNodes, IEnumerable<IPublishedElement> nestedItems, UberDocTypeViewModelWithAttribute model)
        {
            _mapper.Map(CurrentPage, model, propertyMappings: new Dictionary<string, PropertyMapping> 
                    { 
                        { 
                            "ConditionalValueMet", new PropertyMapping 
                                { 
                                    SourceProperty = "heading",
                                    MapIfPropertyMatches = new KeyValuePair<string, string>("isApproved", "true"),
                                } 
                        }, 
                        { 
                            "ConditionalValueNotMet", new PropertyMapping 
                                { 
                                    SourceProperty = "heading",
                                    MapIfPropertyMatches = new KeyValuePair<string, string>("isApproved", "0"),
                                } 
                        },
                        { 
                            "UpperCaseHeading", new PropertyMapping 
                                { 
                                    SourceProperty = "heading",
                                    StringValueFormatter = x => 
                                    {
                                        return x.ToUpper();
                                    }
                                } 
                        }, 
                        { 
                            "FormattedCreatedOnDate", new PropertyMapping 
                                { 
                                    SourceProperty = "CreateDate",
                                    StringValueFormatter = x => 
                                    {
                                        return DateTime.Parse(x).ToString("dd MMMM, yyyy");
                                    }
                                } 
                        }, 
                    })
                .MapCollection(CurrentPage.Children.Where(x => x.IsDocumentType("Comment")), model.Comments)
                .MapCollection(countryNodes, model.Countries)
                .Map(GetSingleXml(), model, new Dictionary<string, PropertyMapping> { { "SingleValueFromXml", new PropertyMapping { SourceProperty = "Day" } }, })
                .MapCollection(GetCollectionXml(), model.CollectionFromXml, null, "Month")
                .Map(GetSingleDictionary(), model, propertyMappings: new Dictionary<string, PropertyMapping> { { "SingleValueFromDictionary", new PropertyMapping { SourceProperty = "Animal" } }, })
                .MapCollection(GetCollectionDictionary(), model.CollectionFromDictionary)
                .Map(GetSingleJson(), model, new Dictionary<string, PropertyMapping> { { "SingleValueFromJson", new PropertyMapping { SourceProperty = "Name" } }, })
                .MapCollection(GetCollectionJson(), model.CollectionFromJson)
                .Map(CurrentPage, model.SubModel)
                .Map(CurrentPage, model.WelcomeTextEnglish)
                .Map(CurrentPage, model.WelcomeTextItalian, "it")
                .MapCollection(nestedItems, model.NestedItems, clearCollectionBeforeMapping: false);
        }

        public IActionResult UberDocTypeWithAttributeAndDiagnostics()
        {
            // Get related content
            var countryNodes = GetRelatedNodes();
            var nestedItems = CurrentPage.Value<IEnumerable<IPublishedElement>>("nestedItems");

            var sw = new Stopwatch();

            var model = new UberDocTypeViewModelWithAttribute();

            var times = 10000;

            sw.Start();
            for (int i = 0; i < times; i++)
            {
                MapToViewModelWithAttributes(countryNodes, nestedItems, model);
            }

            var timeTaken = sw.ElapsedMilliseconds;
            sw.Stop();

            model.TimeTaken = string.Format("Time taken for {0} mapping operations: {1}ms", times, timeTaken);
            return CurrentTemplate(model);
        }

        private IEnumerable<IPublishedContent> GetRelatedNodes() => CurrentPage.Value<IEnumerable<IPublishedContent>>("countries");

        private static XElement GetSingleXml() =>
            new XElement("Date",
                new XElement("Day", "Sunday"));

        private static XElement GetCollectionXml()
        {
            return new XElement("Months",
                new XElement("Month",
                    new XElement("Name", "January")),
                new XElement("Month",
                    new XElement("Name", "February")));
        }

        private static Dictionary<string, object> GetSingleDictionary()
        {
            return new Dictionary<string, object> 
            { 
                { "Animal", "Iguana" },
            };
        }

        private static List<Dictionary<string, object>> GetCollectionDictionary() =>
            new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "Name", "Shark" },
                },
                new Dictionary<string, object>
                {
                    { "Name", "Whale" },
                },
                new Dictionary<string, object>
                {
                    { "Name", "Dophin" },
                },
            };

        private static string GetSingleJson() =>
            @"{
                'Name': 'Eric Cantona',
              }";

        private static string GetCollectionJson() =>
            @"{ 'items': [{
                    'Name': 'David Gower',
                },
                {
                    'Name': 'Geoffrey Boycott',
                }]}";
    }
}
