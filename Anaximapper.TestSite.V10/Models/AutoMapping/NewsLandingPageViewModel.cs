namespace Anaximapper.TestSite.Models
{
    using Anaximapper.Attributes;
    using Microsoft.AspNetCore.Html;
    using System;
    using System.Collections.Generic;

    public class NewsLandingPageViewModel
    {
        public NewsLandingPageViewModel()
        {
            NewsCategory = new Category();
            TopStory = new NewsStory();
            OtherStories = new List<NewsStory>();
        }

        [PropertyMapping(MapRecursively = true)]
        public string Title { get; set; }

        public string Heading { get; set; }

        [PropertyMapping(LevelsAbove = 1)]
        public Category NewsCategory { get; set; }

        public NewsStory TopStory { get; set; }

        public IEnumerable<NewsStory> OtherStories { get; set; }

        public class Category
        {
            public string Title { get; set; }
        }

        public class NewsStory
        {
            public string Headline { get; set; }

            public DateTime StoryDate { get; set; }

            public HtmlString BodyText { get; set; }
        }
    }
}