﻿namespace Anaximapper.TestSite.Models
{
    using Anaximapper.Attributes;
    using Anaximapper.Models;
    using Anaximapper.TestSite.Attributes;
    using Attributes;
    using Microsoft.AspNetCore.Html;
    using System;
    using System.Collections.Generic;
    using Umbraco.Cms.Core.Models;
    using Umbraco.Cms.Core.Models.PublishedContent;

    public class UberDocTypeViewModelWithAttribute
    {
        public UberDocTypeViewModelWithAttribute()
        {
            Comments = new List<CommentViewModelWithAttribute>();
            Countries = new List<CountryViewModel>();
            RelatedLinks = new List<LinkViewModel>();
            CollectionFromXml = new List<NamedItemViewModel>();
            CollectionFromDictionary = new List<NamedItemViewModel>();
            CollectionFromJson = new List<NamedItemViewModel>();
            SubModel = new SubModel();
            AutoMapSingle = new CountryViewModel();
            AutoMapMultiple = new List<CountryViewModel>();
            WelcomeTextEnglish = new CultureSpecificModelWithAttribute();
            WelcomeTextItalian = new CultureSpecificModelWithAttribute();
            Links = new List<Link>();
            NestedItems = new List<NestedItem>();
            NestedItems2 = new List<NestedItem>();
        }

        public int Id { get; set; }

        public string Heading { get; set; }
        
        public string UpperCaseHeading { get; set; }

        [PropertyMapping(SourceProperty = "Heading", DefaultValue = "Default text")]
        public string HeadingWithDefaultValue { get; set; }

        [PropertyMapping(SourceProperty = "CreateDate")]
        public DateTime CreatedOn { get; set; }

        public string FormattedCreatedOnDate { get; set; }

        public HtmlString BodyText { get; set; }
        
        public int StarRating { get; set; }

        public bool IsApproved { get; set; }

        public decimal AverageScore { get; set; }

        public MediaFile MainImage { get; set; }

        public MediaFile MediaPickedImage { get; set; }

        public IEnumerable<MediaFile> MultipleMediaPickedImages { get; set; }

        public string SingleValueFromXml { get; set; }

        public string SingleValueFromDictionary { get; set; }

        public string SingleValueFromJson { get; set; }

        public IList<CommentViewModelWithAttribute> Comments { get; set; }

        public IList<CountryViewModel> Countries { get; set; }

        public IList<LinkViewModel> RelatedLinks { get; set; }

        public IList<NamedItemViewModel> CollectionFromXml { get; set; }

        public IList<NamedItemViewModel> CollectionFromDictionary { get; set; }

        public IList<NamedItemViewModel> CollectionFromJson { get; set; }

        public SubModel SubModel { get; set; }

        [MapFromContentPicker]
        [PropertyMapping(SourceProperty = "selectedComment")]
        public CommentModel SelectedCommentModel { get; set; }

        [PropertyMapping(SourceRelatedProperty = "text")]
        public string SelectedComment { get; set; }

        [PropertyMapping(SourceProperty = "selectedComment", SourceRelatedProperty = "Id")]
        public int SelectedCommentId { get; set; }

        [PropertyMapping(SourcePropertiesForConcatenation = new string[] { "heading", "starRating", "Id", "Url" }, ConcatenationSeperator = ", ")]
        public string ConcatenatedValue { get; set; }

        public string ConditionalValueMet { get; set; }

        public string ConditionalValueNotMet { get; set; }

        [PropertyMapping(SourcePropertiesForCoalescing = new string[] { "emptyField", "Name" })]
        public string CoalescedValue { get; set; }

        [PropertyMapping(SourcePropertiesForCoalescing = new string[] { "emptyField", "heading" })]
        [MapPropertyValueToUpperCase]
        public string CoalescedValueWithMapFromAttribute { get; set; }

        [PropertyMapping(DefaultValue = "Default text")]
        public string NonMapped { get; set; }

        [PropertyMapping(SourceProperty = "emptyField", DefaultValue = "Default text")]
        public string NonMappedFromEmptyString { get; set; }

        [PropertyMapping(Ignore = true)]
        public string DocumentTypeAlias { get; set; }

        public DateTime? Date1 { get; set; }

        public DateTime? Date2 { get; set; }

        public string TimeTaken { get; set; }

        public CountryViewModel AutoMapSingle { get; set; }

        public IEnumerable<CountryViewModel> AutoMapMultiple { get; set; }

        [PropertyMapping(DictionaryKey = "testKey")]
        public string DictionaryValue { get; set; }

        public CultureSpecificModelWithAttribute WelcomeTextEnglish { get; set; }

        public CultureSpecificModelWithAttribute WelcomeTextItalian { get; set; }

        public IEnumerable<Link> Links { get; set; }

        public IList<NestedItem> NestedItems { get; set; }

        [PropertyMapping(SourceProperty = "nestedItems")]
        public IList<NestedItem> NestedItems2 { get; set; }

        public string Letter { get; set; }

        [PropertyMapping(FallbackMethods = new[] { Fallback.Language }, SourceProperty = "pickedCategory", SourceRelatedProperty = "categoryName")]
        public string PickedCategoryName { get; set; }

        [PropertyMapping(SourceProperty = "imageCrop")]
        [MapFromImageCropper(CropName = "square")]
        public string ImageCropUrl { get; set; }
    }

    public class CultureSpecificModelWithAttribute
    {
        public string WelcomeText { get; set; }

        [PropertyMapping(FallbackMethods = new[] { Fallback.Language })]
        public string HelloText { get; set; }
    }
}
