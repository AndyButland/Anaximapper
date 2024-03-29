﻿namespace Anaximapper
{
    using Anaximapper.Attributes;
    using Anaximapper.Models;
    using Anaximapper.Extensions;
    using Microsoft.AspNetCore.Html;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Linq;
    using Umbraco.Cms.Core.Models.PublishedContent;
    using Umbraco.Cms.Core.Routing;
    using Umbraco.Cms.Core.Services;
    using Umbraco.Cms.Web.Common;
    using Umbraco.Extensions;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    public class PublishedContentMapper : IPublishedContentMapper
    {
        /// <summary>
        /// Provides a cache of view model settable properties, only need to use reflection once for each view model within the 
        /// application lifetime
        /// </summary>
        private static readonly ConcurrentDictionary<string, IList<PropertyInfo>> _settableProperties = new ConcurrentDictionary<string, IList<PropertyInfo>>();

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UmbracoHelper _umbracoHelper;
        private readonly IUserService _userService;
        private readonly IPublishedUrlProvider _publishedUrlProvider;
        private readonly IPublishedValueFallback _publishedValueFallback;
        private readonly IPropertyValueGetter _propertyValueGetter;

        private readonly Dictionary<string, CustomMapping> _customMappings;
        private readonly Dictionary<string, CustomObjectMapping> _customObjectMappings;
        
        public PublishedContentMapper(
            IHttpContextAccessor httpContextAccessor,
            IUserService userService,
            IPublishedUrlProvider publishedUrlProvider,
            IPublishedValueFallback publishedValueFallback,
            IPropertyValueGetter propertyValueGetter)
        {
            _httpContextAccessor = httpContextAccessor;
            _umbracoHelper = httpContextAccessor.HttpContext.RequestServices.GetRequiredService<UmbracoHelper>();
            _userService = userService;
            _publishedUrlProvider = publishedUrlProvider;
            _publishedValueFallback = publishedValueFallback;
            _propertyValueGetter = propertyValueGetter;

            _customMappings = new Dictionary<string, CustomMapping>();
            _customObjectMappings = new Dictionary<string, CustomObjectMapping>();

            InitializeDefaultCustomMappings();
        }

        /// <summary>
        /// Gets or sets the root URL from where assets are served from in order to populate
        /// absolute URLs for media files (and support CDN delivery)
        /// </summary>
        public string AssetsRootUrl { get; set; }

        /// <summary>
        /// Allows the mapper to use a custom mapping for a specified type from IPublishedContent
        /// </summary>
        /// <param name="propertyTypeFullName">Full name of the property type to map to</param>
        /// <param name="mapping">Mapping function</param>
        /// <param name="propertyName">Restricts this custom mapping to properties of this name</param>
        public IPublishedContentMapper AddCustomMapping(string propertyTypeFullName, CustomMapping mapping, string propertyName = null)
        {
            var key = propertyName == null ? propertyTypeFullName : string.Concat(propertyTypeFullName, ".", propertyName);
            _customMappings[key] = mapping;
            return this;
        }

        /// <summary>
        /// Allows the mapper to use a custom mapping for a specified type from an object
        /// </summary>
        /// <param name="propertyTypeFullName">Full name of the property type to map to</param>
        /// <param name="mapping">Mapping function</param>
        /// <param name="propertyName">Restricts this custom mapping to properties of this name</param>
        public IPublishedContentMapper AddCustomMapping(string propertyTypeFullName, CustomObjectMapping mapping, string propertyName = null)
        {
            var key = propertyName == null ? propertyTypeFullName : string.Concat(propertyTypeFullName, ".", propertyName);
            _customObjectMappings[key] = mapping;
            return this;
        }

        /// <summary>
        /// Maps an instance of IPublishedContent to the passed view model based on conventions (and/or overrides)
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="content">Instance of IPublishedContent</param>
        /// <param name="model">View model to map to</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient.  Can also indicate the level from which the map should be made above the current content node.  This allows you to pass the level in above the current content for where you want to map a particular property.  E.g. passing { "heading", 1 } will get the heading from the node one level up.</param>
        /// <param name="propertySet">Set of properties to map</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        public IPublishedContentMapper Map<T>(IPublishedContent content,
                                     T model,
                                     string culture = "",
                                     Dictionary<string, PropertyMapping> propertyMappings = null,
                                     PropertySet propertySet = PropertySet.All)
            where T : class
        {
            return Map((IPublishedElement)content, model, culture, propertyMappings, propertySet);
        }

        /// <summary>
        /// Maps an instance of IPublishedElement to the passed view model based on conventions (and/or overrides)
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="content">Instance of IPublishedContent</param>
        /// <param name="model">View model to map to</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient.  Can also indicate the level from which the map should be made above the current content node.  This allows you to pass the level in above the current content for where you want to map a particular property.  E.g. passing { "heading", 1 } will get the heading from the node one level up.</param>
        /// <param name="propertySet">Set of properties to map</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        public IPublishedContentMapper Map<T>(IPublishedElement content,
                                     T model,
                                     string culture = "",
                                     Dictionary<string, PropertyMapping> propertyMappings = null,
                                     PropertySet propertySet = PropertySet.All)
            where T : class
        {
            if (content == null)
            {
                return this;
            }

            // Ensure model is not null
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model), "Object to map to cannot be null");
            }

            // Property mapping overrides can be passed via the dictionary or via attributes on the view model.
            // The subequent mapping code uses the dictionary only, so we need to reflect on the view model
            // and update the dictionary to include keys provided via the attributes.
            propertyMappings = EnsurePropertyMappingsAndUpdateFromModel(model, propertyMappings);      
                
            // Loop through all settable properties on model
            foreach (var property in SettableProperties(model))
            {
                // Check if property has been marked as ignored, if so, don't attempt to map
                if (propertyMappings.IsPropertyIgnored(property.Name))
                {
                    continue;
                }

                // Check if mapping from a dictionary value
                if (propertyMappings.IsMappingFromDictionaryValue(property.Name))
                {
                    SetValueFromDictionary(model, property, propertyMappings[property.Name].DictionaryKey);
                    continue;
                }

                // Get content to map from (check if we want to map to content at a level above the currently passed node) and also
                // get the level above the current node that we are mapping from.
                // Can only do this for IPublishedContent, not IPublishedElement, as the latter has no concept of a Parent.
                var levelsAbove = 0;
                var contentToMapFrom = content is IPublishedContent
                    ? GetContentToMapFrom((IPublishedContent)content, propertyMappings, property.Name, out levelsAbove)
                    : content;

                // Check if property is a complex type and is being asked to be mapped from a higher level in the node tree.
                // If so, we need to trigger a separate mapping operation for this.
                if (!property.PropertyType.IsSimpleType() && levelsAbove > 0)
                {
                    typeof(PublishedContentMapper)
                        .GetMethod("MapIPublishedContent", BindingFlags.NonPublic | BindingFlags.Instance)
                        .MakeGenericMethod(property.PropertyType)
                        .Invoke(this, new[] { contentToMapFrom, property.GetValue(model), culture });
                    continue;
                }

                // Check if we have a string value formatter passed
                var stringValueFormatter = propertyMappings.GetStringValueFormatter(property.Name);

                // If default value passed, set it.  If a mapping is completed it'll be overwritten.
                SetDefaultValueIfProvided(model, propertyMappings, property);

                // Check if we are looking to concatenate or coalesce more than one source property
                var multipleMappingOperation = propertyMappings.GetMultiplePropertyMappingOperation(property.Name);
                switch (multipleMappingOperation)
                {
                    case MultiplePropertyMappingOperation.Concatenate:

                        // Loop through all the source properties requested for concatenation
                        var concatenationSeperator = propertyMappings[property.Name].ConcatenationSeperator ?? string.Empty;

                        var isFirst = true;
                        foreach (var sourceProp in propertyMappings[property.Name].SourcePropertiesForConcatenation)
                        {
                            // Call the mapping function, passing in each source property to use, and flag to contatenate
                            // on all but the first
                            propertyMappings[property.Name].SourceProperty = sourceProp;
                            MapContentProperty(model, property, contentToMapFrom, culture, propertyMappings,
                                concatenateToExistingValue: !isFirst, concatenationSeperator: concatenationSeperator, stringValueFormatter: stringValueFormatter, propertySet: propertySet);
                            isFirst = false;
                        }

                        break;
                    case MultiplePropertyMappingOperation.Coalesce:

                        // Loop through all the source properties requested for coalescing
                        foreach (var sourceProp in propertyMappings[property.Name].SourcePropertiesForCoalescing)
                        {
                            // Call the mapping function, passing in each source property to use, and flag to coalesce
                            // on all but the first
                            propertyMappings[property.Name].SourceProperty = sourceProp;
                            MapContentProperty(model, property, contentToMapFrom, culture, propertyMappings,
                                coalesceWithExistingValue: true, stringValueFormatter: stringValueFormatter, propertySet: propertySet);
                        }

                        break;
                    default:

                        // Map the single property
                        MapContentProperty(model, property, contentToMapFrom, culture, propertyMappings, stringValueFormatter: stringValueFormatter, propertySet: propertySet);
                        break;
                }
            }

            return this;
        }

        /// <summary>
        /// Helper to get the settable properties from a model for mapping from the cache or the model object
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="model">Instance of view model</param>
        /// <returns>Enumerable of settable properties from the model</returns>
        private static IEnumerable<PropertyInfo> SettableProperties<T>(T model) where T : class
        {
            var cacheKey = model.GetType().FullName;
            if (!_settableProperties.ContainsKey(cacheKey))
            {
                _settableProperties[cacheKey] = SettablePropertiesFromObject(model);
            }

            return _settableProperties[cacheKey];
        }

        /// <summary>
        /// Helper to get the settable properties from a model for mapping from the cache or the model object
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="model">Instance of view model</param>
        /// <returns>Enumerable of settable properties from the model</returns>
        private static IList<PropertyInfo> SettablePropertiesFromObject<T>(T model) where T : class
        {
            return model.GetType().GetProperties()
                .Where(p => p.GetSetMethod() != null)
                .ToList();
        }

        /// <summary>
        /// Helper to set the default value of a mapped property, if provided and mapping operation hasn't already found a value
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="model">View model to map to</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient.  Can also indicate the level from which the map should be made above the current content node.  This allows you to pass the level in above the current content for where you want to map a particular property.  E.g. passing { "heading", 1 } will get the heading from the node one level up.</param>
        /// <param name="property">Property of view model to map to</param>
        private static void SetDefaultValueIfProvided<T>(T model, IReadOnlyDictionary<string, PropertyMapping> propertyMappings, PropertyInfo property)
        {
            if (HasDefaultValue(propertyMappings, property.Name))
            {
                property.SetValue(model, propertyMappings[property.Name].DefaultValue);
            }
        }

        /// <summary>
        /// Helper to check if particular property has a default value
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <returns>True if mapping should be from child property</returns>
        private static bool HasDefaultValue(IReadOnlyDictionary<string, PropertyMapping> propertyMappings, string propName) =>
            propertyMappings.ContainsKey(propName) && propertyMappings[propName].DefaultValue != null;

        /// <summary>
        /// Maps content held in XML to the passed view model based on conventions (and/or overrides)
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="xml">XML fragment to map from</param>
        /// <param name="model">View model to map to</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        public IPublishedContentMapper Map<T>(XElement xml,
                                     T model,
                                     Dictionary<string, PropertyMapping> propertyMappings = null)
            where T : class
        {
            if (xml == null)
            {
                return this;
            }

            // Ensure model is not null
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model), "Object to map to cannot be null");
            }

            // Property mapping overrides can be passed via the dictionary or via attributes on the view model.
            // The subequent mapping code uses the dictionary only, so we need to reflect on the view model
            // and update the dictionary to include keys provided via the attributes.
            propertyMappings = EnsurePropertyMappingsAndUpdateFromModel(model, propertyMappings);

            MapFromXml(xml, model, propertyMappings);

            return this;
        }

        /// <summary>
        /// Maps information held in an XML document
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="xml">XML document</param>
        /// <param name="model">View model to map to</param>
        /// <param name="propertyMappingsBase">Optional set of property mappings, for use when convention mapping based on name is not sufficient</param>
        private static void MapFromXml<T>(XElement xml, T model, Dictionary<string, PropertyMapping> propertyMappingsBase)
            where T : class
        {
            // Loop through all settable properties on model
            foreach (var property in SettableProperties(model))
            {
                var propName = GetMappedPropertyName(property.Name, propertyMappingsBase, false);

                // If element with mapped name found, map the value (check case insensitively)
                var mappedElement = GetXElementCaseInsensitive(xml, propName);

                if (mappedElement != null)
                {
                    // Check if we are looking for a child mapping
                    if (propertyMappingsBase.IsMappingFromChildProperty(property.Name))
                    {
                        mappedElement = mappedElement.Element(propertyMappingsBase[property.Name].SourceChildProperty);
                    }

                    if (mappedElement != null)
                    {
                        SetTypedPropertyValue(model, property, mappedElement.Value);
                    }
                }
                else
                {
                    // Try to see if it's in an attribute too
                    var mappedAttribute = GetXAttributeCaseInsensitive(xml, propName);
                    if (mappedAttribute != null)
                    {
                        SetTypedPropertyValue(model, property, mappedAttribute.Value);
                    }
                }

                // If property value not set, and default value passed, use it
                SetDefaultValueIfProvided(model, propertyMappingsBase, property);
            }
        }

        /// <summary>
        /// Helper method to find the property name to map to based on conventions (and/or overrides)
        /// </summary>
        /// <param name="propName">Name of property to map to</param>
        /// <param name="propertyMappings">Set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <param name="convertToCamelCase">Flag for whether to convert property name to camel casing before attempting mapping</param>
        /// <returns>Name of property to map from</returns>
        protected static string GetMappedPropertyName(string propName, IReadOnlyDictionary<string, PropertyMapping> propertyMappings, bool convertToCamelCase = false)
        {
            var mappedName = propName;
            if (propertyMappings.ContainsKey(propName) &&
                !string.IsNullOrEmpty(propertyMappings[propName].SourceProperty))
            {
                mappedName = propertyMappings[propName].SourceProperty;
            }

            if (convertToCamelCase)
            {
                mappedName = CamelCase(mappedName);
            }

            return mappedName;
        }

        /// <summary>
        /// Helper to retrieve an XElement by name case insensitively
        /// </summary>
        /// <param name="xml">Xml fragment to search in</param>
        /// <param name="propName">Element name to look up</param>
        /// <returns>Matched XElement</returns>
        private static XElement GetXElementCaseInsensitive(XElement xml, string propName) =>
            xml?.Elements().SingleOrDefault(s => string.Compare(s.Name.ToString(), propName, true) == 0);

        /// <summary>
        /// Helper to retrieve an XAttribute by name case insensitively
        /// </summary>
        /// <param name="xml">Xml fragment to search in</param>
        /// <param name="propName">Element name to look up</param>
        /// <returns>Matched XAttribute</returns>
        private static XAttribute GetXAttributeCaseInsensitive(XElement xml, string propName) =>
            xml.Attributes().SingleOrDefault(s => string.Compare(s.Name.ToString(), propName, true) == 0);

        /// <summary>
        /// Helper method to convert a string value to an appropriate type for setting via reflection
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="model">View model to map to</param>
        /// <param name="property">Property to map to</param>
        /// <param name="stringValue">String representation of property value</param>
        /// <param name="concatenateToExistingValue">Flag for if we want to concatenate the value to the existing value</param>
        /// <param name="concatenationSeperator">If using concatenation, use this string to separate items</param>
        /// <param name="coalesceWithExistingValue">Flag for if we want to coalesce the value to the existing value</param>
        /// <param name="stringValueFormatter">A function for transformation of the string value, if passed</param>
        /// <param name="propertySet">Set of properties to map. This function will only run for All and Custom</param>
        private static void SetTypedPropertyValue<T>(T model, PropertyInfo property, string stringValue,
                                                       bool concatenateToExistingValue = false, string concatenationSeperator = "",
                                                       bool coalesceWithExistingValue = false,
                                                       Func<string, string> stringValueFormatter = null,
                                                       PropertySet propertySet = PropertySet.All)
        {
            if (propertySet != PropertySet.All && propertySet != PropertySet.Custom)
            {
                return;
            }

            var propertyTypeName = property.PropertyType.Name;
            var isNullable = false;
            if (propertyTypeName == "Nullable`1" && property.PropertyType.GenericTypeArguments.Length == 1)
            {
                propertyTypeName = property.PropertyType.GenericTypeArguments[0].Name;
                isNullable = true;
            }

            switch (propertyTypeName)
            {
                case "Boolean":
                    bool boolValue;
                    if (stringValue == "1" || stringValue == "0")
                    {
                        // Special case: Archetype stores "1" for boolean true, so we'll handle that convention
                        property.SetValue(model, stringValue == "1");
                    }
                    else if (bool.TryParse(stringValue, out boolValue))
                    {
                        property.SetValue(model, boolValue);
                    }

                    break;
                case "Byte":
                    byte byteValue;
                    if (byte.TryParse(stringValue, out byteValue))
                    {
                        property.SetValue(model, byteValue);
                    }

                    break;
                case "Int16":
                    short shortValue;
                    if (short.TryParse(stringValue, out shortValue))
                    {
                        property.SetValue(model, shortValue);
                    }

                    break;
                case "Int32":
                    int intValue;
                    if (int.TryParse(stringValue, out intValue))
                    {
                        property.SetValue(model, intValue);
                    }

                    break;
                case "Int64":
                    long longValue;
                    if (long.TryParse(stringValue, out longValue))
                    {
                        property.SetValue(model, longValue);
                    }

                    break;
                case "Decimal":
                    decimal decimalValue;
                    if (decimal.TryParse(stringValue, out decimalValue))
                    {
                        property.SetValue(model, decimalValue);
                    }

                    break;
                case "Float":
                    float floatValue;
                    if (float.TryParse(stringValue, out floatValue))
                    {
                        property.SetValue(model, floatValue);
                    }

                    break;
                case "Double":
                    double doubleValue;
                    if (double.TryParse(stringValue, out doubleValue))
                    {
                        property.SetValue(model, doubleValue);
                    }

                    break;
                case "DateTime":
                    DateTime dateTimeValue;
                    if (DateTime.TryParse(stringValue, out dateTimeValue))
                    {
                        // Umbraco returns DateTime.MinValue if no date set.  If mapping to a nullable date time makes more sense
                        // to leave as null if this value is returned.
                        if (dateTimeValue > DateTime.MinValue || !isNullable)
                        {
                            property.SetValue(model, dateTimeValue);
                        }
                    }

                    break;
                case "HtmlString":
                    var htmlString = new HtmlString(stringValue);
                    property.SetValue(model, htmlString);
                    break;
                case "String":

                    // Only supporting/makes sense to allow concatenation and coalescing for String type
                    if (concatenateToExistingValue)
                    {
                        var prefixValueWith = property.GetValue(model) + concatenationSeperator;
                        property.SetValue(model, prefixValueWith + stringValue);
                    }
                    else if (coalesceWithExistingValue)
                    {
                        // Check is existing value and only set if it's null, empty or whitespace
                        if (property.GetValue(model) == null || string.IsNullOrWhiteSpace(property.GetValue(model).ToString()))
                        {
                            property.SetValue(model, stringValue);
                        }
                    }
                    else if (stringValueFormatter != null)
                    {
                        property.SetValue(model, stringValueFormatter(stringValue));
                    }
                    else if (!string.IsNullOrEmpty(stringValue))
                    {
                        property.SetValue(model, stringValue);
                    }

                    break;
            }
        }

        /// <summary>
        /// Maps custom data held in a dictionary
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="dictionary">Dictionary of property name/value pairs</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="model">View model to map to</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        public IPublishedContentMapper Map<T>(Dictionary<string, object> dictionary,
                                     T model,
                                     string culture = "",
                                     Dictionary<string, PropertyMapping> propertyMappings = null)
            where T : class
        {
            if (dictionary == null)
            {
                return this;
            }

            // Ensure model is not null
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model), "Object to map to cannot be null");
            }

            // Property mapping overrides can be passed via the dictionary or via attributes on the view model.
            // The subequent mapping code uses the dictionary only, so we need to reflect on the view model
            // and update the dictionary to include keys provided via the attributes.
            propertyMappings = EnsurePropertyMappingsAndUpdateFromModel(model, propertyMappings);

            // Loop through all settable properties on model
            foreach (var property in SettableProperties(model))
            {
                var propName = GetMappedPropertyName(property.Name, propertyMappings);
                    
                // If element with mapped name found, map the value
                if (dictionary.ContainsKey(propName))
                {
                    // First check to see if property is marked with an attribute that implements IMapFromAttribute - if so, use that
                    var mapFromAttribute = GetMapFromAttribute(property);
                    if (mapFromAttribute != null)
                    {
                        mapFromAttribute.SetPropertyValue(dictionary[propName], property, model, new MappingContext(this, _httpContextAccessor.HttpContext));
                        continue;
                    }

                    // Then check to see if we have a custom dictionary mapping defined
                    var namedCustomMappingKey = GetNamedCustomMappingKey(property);
                    var unnamedCustomMappingKey = GetUnnamedCustomMappingKey(property); 
                    if (_customObjectMappings.ContainsKey(namedCustomMappingKey))
                    {
                        var value = _customObjectMappings[namedCustomMappingKey](this, _publishedValueFallback, _publishedUrlProvider, dictionary[propName]);
                        if (value != null)
                        {
                            property.SetValue(model, value);
                        }
                    }
                    else if (_customObjectMappings.ContainsKey(unnamedCustomMappingKey))
                    {
                        var value = _customObjectMappings[unnamedCustomMappingKey](this, _publishedValueFallback, _publishedUrlProvider, dictionary[propName]);
                        if (value != null)
                        {
                            property.SetValue(model, value);
                        }
                    }
                    else if (dictionary[propName] is IPublishedContent)
                    {
                        // Handle cases where the value object passed in the dictionary is actually an IPublishedContent
                        // - if so, we'll fully map from that
                        Map((IPublishedContent)dictionary[propName], property.GetValue(model), culture, propertyMappings);
                    }
                    else if (dictionary[propName] is IEnumerable<IPublishedContent>)
                    {
                        // Handle cases where the value object passed in the dictionary is actually an IEnumerable<IPublishedContent> content
                        // - if so, we'll fully map from that
                        // Have to make this call using reflection as we don't know the type of the generic collection at compile time
                        var propertyValue = property.GetValue(model);
                        var collectionPropertyType = GetGenericCollectionType(property);
                        typeof(PublishedContentMapper)
                            .GetMethod("MapCollectionOfIPublishedContent", BindingFlags.NonPublic | BindingFlags.Instance)
                            .MakeGenericMethod(collectionPropertyType)
                            .Invoke(this, new object[] { (IEnumerable<IPublishedContent>)dictionary[propName], propertyValue, culture, propertyMappings });
                    }
                    else
                    {
                        // Otherwise we just map the object as a simple type
                        var stringValue = dictionary[propName] != null ? dictionary[propName].ToString() : string.Empty;
                        SetTypedPropertyValue(model, property, stringValue);
                    }
                }

                // If property value not set, and default value passed, use it
                SetDefaultValueIfProvided(model, propertyMappings, property);
            }

            return this;
        }

        /// <summary>
        /// Helper to get the named custom mapping key (based on type and name)
        /// </summary>
        /// <param name="property">PropertyInfo to map to</param>
        /// <returns>Name of custom mapping key</returns>
        private static string GetNamedCustomMappingKey(PropertyInfo property) => string.Concat(property.PropertyType.FullName, ".", property.Name);

        /// <summary>
        /// Helper to get the unnamed custom mapping key (based on type only)
        /// </summary>
        /// <param name="property">PropertyInfo to map to</param>
        /// <returns>Name of custom mapping key</returns>
        private static string GetUnnamedCustomMappingKey(PropertyInfo property) => property.PropertyType.FullName;

        /// <summary>
        /// Maps information held in a JSON string
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="json">JSON string</param>
        /// <param name="model">View model to map to</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        public IPublishedContentMapper Map<T>(string json,
                                     T model,
                                     Dictionary<string, PropertyMapping> propertyMappings = null)
            where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return this;
            }

            // Ensure model is not null
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model), "Object to map to cannot be null");
            }

            // Property mapping overrides can be passed via the dictionary or via attributes on the view model.
            // The subequent mapping code uses the dictionary only, so we need to reflect on the view model
            // and update the dictionary to include keys provided via the attributes.
            propertyMappings = EnsurePropertyMappingsAndUpdateFromModel(model, propertyMappings);

            // Cast dictionary to base type, which can then be used by methods in the "common" referenced project.
            var propertyMappingsBase = propertyMappings;

            MapFromJson(json, model, propertyMappingsBase);

            return this;
        }

        /// <summary>
        /// Maps information held in a JSON string
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="json">JSON string</param>
        /// <param name="model">View model to map to</param>
        /// <param name="propertyMappingsBase">Optional set of property mappings, for use when convention mapping based on name is not sufficient</param>
        private void MapFromJson<T>(string json, T model, Dictionary<string, PropertyMapping> propertyMappingsBase)
            where T : class
        {
            // Parse JSON string to queryable object
            var jsonObj = JObject.Parse(json);

            // Loop through all settable properties on model
            foreach (var property in SettableProperties(model))
            {
                var propName = GetMappedPropertyName(property.Name, propertyMappingsBase, false);

                // If element with mapped name found, map the value
                var childPropName = string.Empty;
                if (propertyMappingsBase.IsMappingFromChildProperty(property.Name))
                {
                    childPropName = propertyMappingsBase[property.Name].SourceChildProperty;
                }

                var stringValue = GetJsonFieldCaseInsensitive(jsonObj, propName, childPropName);
                if (!string.IsNullOrEmpty(stringValue))
                {
                    SetTypedPropertyValue(model, property, stringValue);
                }

                // If property value not set, and default value passed, use it
                SetDefaultValueIfProvided(model, propertyMappingsBase, property);
            }
        }

        /// <summary>
        /// Helper to retrieve a JSON field by name case insensitively
        /// </summary>
        /// <param name="jsonObj">JSON object to get field value from</param>
        /// <param name="propName">Property name to look up</param>
        /// <param name="childPropName">Child property name to look up</param>
        /// <returns>String value of JSON field</returns>
        private string GetJsonFieldCaseInsensitive(JObject jsonObj, string propName, string childPropName)
        {
            var token = GetJToken(jsonObj, propName);
            if (token == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(childPropName))
            {
                // Looking up on child object
                var childToken = token[childPropName];

                // If not found, try with lower case
                if (childToken == null)
                {
                    childToken = token[childPropName.ToLowerInvariant()];
                }

                // If still not found, try with camel case
                if (childToken == null)
                {
                    childToken = token[CamelCase(childPropName)];
                }

                if (childToken != null)
                {
                    return (string)childToken;
                }
            }
            else
            {
                // Looking up directly on object
                return (string)token;
            }

            return string.Empty;
        }

        /// <summary>
        /// Lookup for JSON property
        /// </summary>
        /// <param name="jsonObj">JSON object</param>
        /// <param name="propName">Property to get value for</param>G
        /// <returns>JToken object, if found</returns>
        private static JToken GetJToken(JObject jsonObj, string propName) =>
            // If not found, try with lower case, ff still not found, try with camel case
            (jsonObj[propName] ?? jsonObj[propName.ToLowerInvariant()]) ?? jsonObj[CamelCase(propName)];

        /// <summary>
        /// Helper method to convert a string into camel case
        /// </summary>
        /// <param name="input">Input string</param>
        /// <returns>Camel cased string</returns>
        private static string CamelCase(string input) => char.ToLowerInvariant(input[0]) + input.Substring(1);

        /// <summary>
        /// Maps a collection of IPublishedElement to the passed view model
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="contentCollection">Collection of IPublishedContent</param>
        /// <param name="modelCollection">Collection from view model to map to</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient.  Can also indicate the level from which the map should be made above the current content node.  This allows you to pass the level in above the current content for where you want to map a particular property.  E.g. passing { "heading", 1 } will get the heading from the node one level up.</param>
        /// <param name="propertySet">Set of properties to map</param>
        /// <param name="clearCollectionBeforeMapping">Flag indicating whether to clear the collection mapping too before carrying out the mapping</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        public IPublishedContentMapper MapCollection<T>(IEnumerable<IPublishedContent> contentCollection,
                                               IList<T> modelCollection,
                                               string culture = "",
                                               Dictionary<string, PropertyMapping> propertyMappings = null,
                                               PropertySet propertySet = PropertySet.All,
                                               bool clearCollectionBeforeMapping = true)
            where T : class, new()
        { 
            return MapCollection((IEnumerable<IPublishedElement>)contentCollection, modelCollection, culture, propertyMappings, propertySet, clearCollectionBeforeMapping);
        }

        /// <summary>
        /// Maps a collection of IPublishedElement to the passed view model
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="contentCollection">Collection of IPublishedContent</param>
        /// <param name="modelCollection">Collection from view model to map to</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient.  Can also indicate the level from which the map should be made above the current content node.  This allows you to pass the level in above the current content for where you want to map a particular property.  E.g. passing { "heading", 1 } will get the heading from the node one level up.</param>
        /// <param name="propertySet">Set of properties to map</param>
        /// <param name="clearCollectionBeforeMapping">Flag indicating whether to clear the collection mapping too before carrying out the mapping</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        public IPublishedContentMapper MapCollection<T>(IEnumerable<IPublishedElement> contentCollection,
                                               IList<T> modelCollection,
                                               string culture = "",
                                               Dictionary<string, PropertyMapping> propertyMappings = null,
                                               PropertySet propertySet = PropertySet.All, 
                                               bool clearCollectionBeforeMapping = true)
            where T : class, new()
        {
            if (contentCollection == null)
            {
                return this;
            }

            if (modelCollection == null)
            {
                throw new ArgumentNullException(nameof(modelCollection), "Collection to map to can be empty, but not null");
            }

            // Check to see if the collection has any items already, if it does, clear it first (could have come about with an 
            // explicit mapping called before the auto-mapping feature was introduced).  In any case, assuming collection is empty
            // seems reasonable so this is the default behaviour.
            if (clearCollectionBeforeMapping && modelCollection.Any())
            {
                modelCollection.Clear();
            }

            foreach (var content in contentCollection)
            {
                var itemToCreate = new T();

                // Check for custom mappings for the type itself (in the Map() method we'll check for custom mappings on each property)
                var customMappingKey = itemToCreate.GetType().FullName;
                if (_customObjectMappings.ContainsKey(customMappingKey))
                {
                    itemToCreate = _customObjectMappings[customMappingKey](this, _publishedValueFallback, _publishedUrlProvider, content) as T;
                }
                else if (_customMappings.ContainsKey(customMappingKey))
                {
                    // Any custom mappings used here cannot be based on a single property, as we don't have a property to map to to work out what this should be.
                    // So we just pass an empty string into the custom mapping call.
                    itemToCreate = _customMappings[customMappingKey](this, content, _publishedValueFallback, _publishedUrlProvider, string.Empty, default(Fallback)) as T;
                }
                else
                {
                    // Otherwise map the single content item as normal
                    Map<T>(content, itemToCreate, culture, propertyMappings, propertySet);
                }

                modelCollection.Add(itemToCreate);
            }

            return this;
        }

        /// <summary>
        /// Maps a collection of content held in XML to the passed view model collection based on conventions (and/or overrides)
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="xml">XML fragment to map from</param>
        /// <param name="modelCollection">Collection from view model to map to</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <param name="groupElementName">Name of the element grouping each item in the XML (defaults to "Item")</param>
        /// <param name="createItemsIfNotAlreadyInList">Flag indicating whether to create items if they don't already exist in the collection, or to just map to existing ones</param>
        /// <param name="destIdentifyingPropName">When updating existing items in a collection, this property name is considered unique and used for look-ups to identify and update the correct item (defaults to "Id").</param>
        /// <param name="sourceIdentifyingPropName">When updating existing items in a collection, this XML element is considered unique and used for look-ups to identify and update the correct item (defaults to "Id").  Case insensitive.</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        public IPublishedContentMapper MapCollection<T>(XElement xml, IList<T> modelCollection,
                                               Dictionary<string, PropertyMapping> propertyMappings = null,
                                               string groupElementName = "item",
                                               bool createItemsIfNotAlreadyInList = true,
                                               string sourceIdentifyingPropName = "Id",
                                               string destIdentifyingPropName = "Id")
            where T : class, new()
        {
            if (xml == null)
            {
                return this;
            }

            if (modelCollection == null)
            {
                throw new ArgumentNullException(nameof(modelCollection), "Collection to map to can be empty, but not null");
            }

            // Loop through each of the items defined in the XML
            foreach (var element in xml.Elements(groupElementName))
            {
                // Check if item is already in the list by looking up provided unique key
                T itemToUpdate = default(T);
                if (TypeHasProperty(typeof(T), destIdentifyingPropName))
                {
                    itemToUpdate = GetExistingItemFromCollection(modelCollection, destIdentifyingPropName, element.Element(sourceIdentifyingPropName).Value);
                }

                if (itemToUpdate != null)
                {
                    // Item found, so map it
                    Map<T>(element, itemToUpdate, propertyMappings);
                }
                else
                {
                    // Item not found, so create if that was requested
                    if (!createItemsIfNotAlreadyInList)
                    {
                        continue;
                    }

                    var itemToCreate = new T();
                    Map(element, itemToCreate, propertyMappings);
                    modelCollection.Add(itemToCreate);
                }
            }

            return this;
        }

        /// <summary>
        /// Helper method to get an existing item from the model collection
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="modelCollection">Collection from view model to map to</param>
        /// <param name="modelPropertyName">Model property name to look up</param>
        /// <param name="valueToMatch">Property value to match on</param>
        /// <returns>Single instance of T if found in the collection</returns>
        private static T GetExistingItemFromCollection<T>(IEnumerable<T> modelCollection, string modelPropertyName, string valueToMatch) where T : new() =>
            modelCollection
                .SingleOrDefault(x => x.GetType()
                .GetProperties()
                .Single(p => p.Name == modelPropertyName)
                .GetValue(x).ToString().ToLowerInvariant() == valueToMatch.ToLowerInvariant());

        /// <summary>
        /// Helper to check if a given type has a property
        /// </summary>
        /// <param name="type">Type to check</param>
        /// <param name="propName">Name of property</param>
        /// <returns>True of property exists on type</returns>
        private static bool TypeHasProperty(Type type, string propName) =>
            type
                .GetProperties()
                .SingleOrDefault(p => p.Name == propName) != null;

        /// <summary>
        /// Maps a collection custom data held in an linked dictionary to a collection
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="dictionaries">Collection of custom data containing a list of dictionary of property name/value pairs.  One of these keys provides a lookup for the existing collection.</param>
        /// <param name="modelCollection">Collection from view model to map to</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <param name="createItemsIfNotAlreadyInList">Flag indicating whether to create items if they don't already exist in the collection, or to just map to existing ones</param>
        /// <param name="sourceIdentifyingPropName">When updating existing items in a collection, this dictionary key is considered unique and used for look-ups to identify and update the correct item (defaults to "Id").  Case insensitive.</param>
        /// <param name="destIdentifyingPropName">When updating existing items in a collection, this property name is considered unique and used for look-ups to identify and update the correct item (defaults to "Id").</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        public IPublishedContentMapper MapCollection<T>(IEnumerable<Dictionary<string, object>> dictionaries,
                                               IList<T> modelCollection,
                                               string culture = "",
                                               Dictionary<string, PropertyMapping> propertyMappings = null,
                                               bool createItemsIfNotAlreadyInList = true,
                                               string sourceIdentifyingPropName = "Id",
                                               string destIdentifyingPropName = "Id")
            where T : class, new()
        {
            if (dictionaries == null)
            {
                return this;
            }

            if (modelCollection == null)
            {
                throw new ArgumentNullException(nameof(modelCollection), "Collection to map to can be empty, but not null");
            }

            // Loop through each of the items defined in the dictionary
            foreach (var dictionary in dictionaries)
            {
                // Check if item is already in the list by looking up provided unique key
                var itemToUpdate = default(T);
                if (TypeHasProperty(typeof(T), destIdentifyingPropName))
                {
                    itemToUpdate = GetExistingItemFromCollection(modelCollection, destIdentifyingPropName, dictionary[sourceIdentifyingPropName].ToString());
                }

                if (itemToUpdate != null)
                {
                    // Item found, so map it
                    Map(dictionary, itemToUpdate, culture, propertyMappings);
                }
                else
                {
                    // Item not found, so create if that was requested
                    if (!createItemsIfNotAlreadyInList)
                    {
                        continue;
                    }

                    var itemToCreate = new T();
                    Map(dictionary, itemToCreate, culture, propertyMappings);
                    modelCollection.Add(itemToCreate);
                }
            }

            return this;
        }

        /// <summary>
        /// Maps a collection custom data held in a JSON string to a collection
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="json">JSON string containing collection</param>
        /// <param name="modelCollection">Collection from view model to map to</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <param name="rootElementName">Name of root element in JSON array</param>
        /// <param name="createItemsIfNotAlreadyInList">Flag indicating whether to create items if they don't already exist in the collection, or to just map to existing ones</param>
        /// <param name="sourceIdentifyingPropName">When updating existing items in a collection, this property name is considered unique and used for look-ups to identify and update the correct item (defaults to "Id").</param>
        /// <param name="destIdentifyingPropName">When updating existing items in a collection, this dictionary key is considered unique and used for look-ups to identify and update the correct item (defaults to "Id").  Case insensitive.</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        public IPublishedContentMapper MapCollection<T>(string json, IList<T> modelCollection,
                                               Dictionary<string, PropertyMapping> propertyMappings = null,
                                               string rootElementName = "items",
                                               bool createItemsIfNotAlreadyInList = true,
                                               string sourceIdentifyingPropName = "Id",
                                               string destIdentifyingPropName = "Id")
            where T : class, new()
        {
            if (string.IsNullOrEmpty(json))
            {
                return this;
            }

            if (modelCollection == null)
            {
                throw new ArgumentNullException(nameof(modelCollection), "Collection to map to can be empty, but not null");
            }

            // Loop through each of the items defined in the JSON
            var jsonObject = JObject.Parse(json);
            foreach (var element in jsonObject[rootElementName].Children())
            {
                // Check if item is already in the list by looking up provided unique key
                var itemToUpdate = default(T);
                if (TypeHasProperty(typeof(T), destIdentifyingPropName))
                {
                    itemToUpdate = GetExistingItemFromCollection(modelCollection, destIdentifyingPropName, element[sourceIdentifyingPropName].Value<string>());
                }

                if (itemToUpdate != null)
                {
                    // Item found, so map it
                    Map(element.ToString(), itemToUpdate, propertyMappings);
                }
                else
                {
                    // Item not found, so create if that was requested
                    if (!createItemsIfNotAlreadyInList)
                    {
                        continue;
                    }

                    var itemToCreate = new T();
                    Map(element.ToString(), itemToCreate, propertyMappings);
                    modelCollection.Add(itemToCreate);
                }
            }

            return this;
        }

        /// <summary>
        /// Sets up the default mappings of known types that will be handled automatically
        /// </summary>
        private void InitializeDefaultCustomMappings()
        {
            InitializeDefaultCustomMappingForMediaFile();
            InitializeDefaultCustomMappingForMediaFileCollection();
        }

        /// <summary>
        /// If a custom mapping hasn't already been provided, sets up the default mappings of single instances of <see cref="MediaFile"/> that will be handled automatically
        /// </summary>
        private void InitializeDefaultCustomMappingForMediaFile()
        {
            var customMappingKey = typeof(MediaFile).FullName;
            if (!_customMappings.ContainsKey(customMappingKey))
            {
                AddCustomMapping(customMappingKey, PickedMediaMapper.MapMediaFile);
            }
        }

        /// <summary>
        /// If a custom mapping hasn't already been provided, sets up the default mappings of collections of <see cref="MediaFile"/> that will be handled automatically
        /// </summary>
        private void InitializeDefaultCustomMappingForMediaFileCollection()
        {
            var customMappingKey = typeof(IEnumerable<MediaFile>).FullName;
            if (!_customMappings.ContainsKey(customMappingKey))
            {
                AddCustomMapping(customMappingKey, PickedMediaMapper.MapMediaFileCollection);
            }
        }

        /// <summary>
        /// Helper to ensure property mappings are not null even if not provided via the dictionary.
        /// Also to populate from attributes on the view model if that method is used for configuration of the mapping
        /// operation.
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="model">Instance of view model</param>
        /// <param name="propertyMappings">Set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <returns>Set of property mappings</returns>
        private Dictionary<string, PropertyMapping> EnsurePropertyMappingsAndUpdateFromModel<T>(T model, Dictionary<string, PropertyMapping> propertyMappings) where T : class
        {
            if (propertyMappings == null)
            {
                propertyMappings = new Dictionary<string, PropertyMapping>();
            }

            foreach (var property in SettableProperties(model))
            {
                var attribute = GetPropertyMappingAttribute(property);
                if (attribute == null)
                {
                    continue;
                }

                var propertyMapping = GetPropertyMappingAttributeAsPropertyMapping(attribute);
                if (propertyMappings.ContainsKey(property.Name))
                {
                    MapPropertyMappingValuesFromAttributeToDictionaryIfNotAlreadySet(propertyMappings, property, propertyMapping);
                }
                else
                {
                    // Property mapping not found on dictionary, so add it
                    propertyMappings.Add(property.Name, propertyMapping);
                }
            }

            return propertyMappings;
        }

        /// <summary>
        /// Helper to convert the values of a property mapping attribute to an instance of PropertyMapping
        /// </summary>
        /// <param name="attribute">Attribute added to a view model property</param>
        /// <returns>PropertyMapping instance</returns>
        private static PropertyMapping GetPropertyMappingAttributeAsPropertyMapping(PropertyMappingAttribute attribute)
        {
            var mapping = new PropertyMapping();
            MapPropertyMappingValues(attribute, mapping);
            return mapping;
        }

        /// <summary>
        /// Maps values from attribute to mapping object
        /// </summary>
        /// <param name="attribute">Property mapping attribute</param>
        /// <param name="mapping">Property mapping object</param>
        private static void MapPropertyMappingValues(PropertyMappingAttribute attribute, IPropertyMapping mapping)
        {
            mapping.SourceProperty = attribute.SourceProperty;
            mapping.LevelsAbove = attribute.LevelsAbove;
            mapping.SourceChildProperty = attribute.SourceChildProperty;
            mapping.SourceRelatedProperty = attribute.SourceRelatedProperty;
            mapping.ConcatenationSeperator = attribute.ConcatenationSeperator;
            mapping.SourcePropertiesForCoalescing = attribute.SourcePropertiesForCoalescing;
            mapping.SourcePropertiesForConcatenation = attribute.SourcePropertiesForConcatenation;
            mapping.DefaultValue = attribute.DefaultValue;
            mapping.DictionaryKey = attribute.DictionaryKey;
            mapping.Ignore = attribute.Ignore;
            mapping.PropertyValueGetter = attribute.PropertyValueGetter;
            mapping.MapRecursively = attribute.MapRecursively;
            mapping.FallbackMethods = attribute.FallbackMethods;
            mapping.CustomMappingType = attribute.CustomMappingType;
            mapping.CustomMappingMethod = attribute.CustomMappingMethod;
            mapping.MapFromPreValue = attribute.MapFromPreValue;
        }

        /// <summary>
        /// Helper to populate from attributes on the view model if that method is used for configuration of the mapping.
        /// Property mapping already exists on dictionary, so update the values not already set
        /// (if for some reason on both, dictionary takes priority)
        /// </summary>
        /// <param name="propertyMappings">Set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <param name="property">Property name</param>
        /// <param name="propertyMapping">Property mapping to map from</param>
        private static void MapPropertyMappingValuesFromAttributeToDictionaryIfNotAlreadySet(Dictionary<string, PropertyMapping> propertyMappings,
                                                                                               PropertyInfo property,
                                                                                               PropertyMapping propertyMapping)
        {
            if (string.IsNullOrEmpty(propertyMappings[property.Name].SourceProperty))
            {
                propertyMappings[property.Name].SourceProperty = propertyMapping.SourceProperty;
            }

            if (propertyMappings[property.Name].LevelsAbove == 0)
            {
                propertyMappings[property.Name].LevelsAbove = propertyMapping.LevelsAbove;
            }

            if (string.IsNullOrEmpty(propertyMappings[property.Name].SourceRelatedProperty))
            {
                propertyMappings[property.Name].SourceRelatedProperty = propertyMapping.SourceRelatedProperty;
            }

            if (string.IsNullOrEmpty(propertyMappings[property.Name].SourceChildProperty))
            {
                propertyMappings[property.Name].SourceChildProperty = propertyMapping.SourceChildProperty;
            }

            if (propertyMappings[property.Name].SourcePropertiesForConcatenation == null)
            {
                propertyMappings[property.Name].SourcePropertiesForConcatenation =
                    propertyMapping.SourcePropertiesForConcatenation;
            }

            if (string.IsNullOrEmpty(propertyMappings[property.Name].ConcatenationSeperator))
            {
                propertyMappings[property.Name].ConcatenationSeperator = propertyMapping.ConcatenationSeperator;
            }

            if (propertyMappings[property.Name].SourcePropertiesForCoalescing == null)
            {
                propertyMappings[property.Name].SourcePropertiesForCoalescing = propertyMapping.SourcePropertiesForCoalescing;
            }

            if (propertyMappings[property.Name].MapIfPropertyMatches.Equals(default(KeyValuePair<string, string>)))
            {
                propertyMappings[property.Name].MapIfPropertyMatches = propertyMapping.MapIfPropertyMatches;
            }

            if (propertyMappings[property.Name].DefaultValue == null)
            {
                propertyMappings[property.Name].DefaultValue = propertyMapping.DefaultValue;
            }

            if (propertyMappings[property.Name].DictionaryKey == null)
            {
                propertyMappings[property.Name].DictionaryKey = propertyMapping.DictionaryKey;
            }

            propertyMappings[property.Name].Ignore = propertyMapping.Ignore;

            if (propertyMappings[property.Name].PropertyValueGetter == null)
            {
                propertyMappings[property.Name].PropertyValueGetter = propertyMapping.PropertyValueGetter;
            }

            propertyMappings[property.Name].MapRecursively = propertyMapping.MapRecursively;

            if (propertyMappings[property.Name].FallbackMethods == null)
            {
                propertyMappings[property.Name].FallbackMethods = propertyMapping.FallbackMethods;
            }

            if (propertyMappings[property.Name].CustomMappingType == null)
            {
                propertyMappings[property.Name].CustomMappingType = propertyMapping.CustomMappingType;
            }

            if (propertyMappings[property.Name].CustomMappingMethod == null)
            {
                propertyMappings[property.Name].CustomMappingMethod = propertyMapping.CustomMappingMethod;
            }

            propertyMappings[property.Name].MapFromPreValue = propertyMapping.MapFromPreValue;
        }

        /// <summary>
        /// Helper to extract the PropertyMappingAttribute from a property of the model
        /// </summary>
        /// <param name="property">Property to check for attribute on</param>
        /// <returns>Instance of attribute if found, otherwise null</returns>
        private static PropertyMappingAttribute GetPropertyMappingAttribute(MemberInfo property) =>
            Attribute.GetCustomAttribute(property, typeof(PropertyMappingAttribute), false) as PropertyMappingAttribute;

        /// <summary>
        /// Helper to retrieve an attribute derived from IMapFromAttribute from a property
        /// </summary>
        /// <param name="property">Property to retrieve the attribute from</param>
        /// <returns>IMapFromAttribute marked on the property, or null if no such attribute is found</returns>
        private static IMapFromAttribute GetMapFromAttribute(PropertyInfo property)
        {
            return (IMapFromAttribute)property.GetCustomAttributes(false)
                .FirstOrDefault(x => x is IMapFromAttribute);
        }

        /// <summary>
        /// Gets the IPublishedContent to map from.  Normally this will be the one passed but it's possible to map at a level above the current node.
        /// </summary>
        /// <param name="content">Passed content to map from</param>
        /// <param name="propertyMappings">Dictionary of properties and levels to map from</param>
        /// <param name="propName">Name of property to map</param>
        /// <param name="levelsAbove">Output parameter indicating the levels above the current node we are mapping from</param>
        /// <returns>Instance of IPublishedContent to map from</returns>
        private static IPublishedElement GetContentToMapFrom(IPublishedContent content, 
                                                             IReadOnlyDictionary<string, PropertyMapping> propertyMappings, 
                                                             string propName, 
                                                             out int levelsAbove)
        {
            levelsAbove = 0;
            var contentToMapFrom = content;
            if (!propertyMappings.ContainsKey(propName))
            {
                return contentToMapFrom;
            }

            levelsAbove = propertyMappings[propName].LevelsAbove;
            for (var i = 0; i < levelsAbove; i++)
            {
                contentToMapFrom = contentToMapFrom.Parent;
                if (contentToMapFrom == null)
                {
                    break;
                }
            }

            return contentToMapFrom;
        }

        /// <summary>
        /// Maps a given IPublished content field (either native or from document type) to property on view model
        /// </summary>
        /// <typeparam name="T">Type of view model</typeparam>
        /// <param name="model">Instance of view model</param>
        /// <param name="property">Property of view model to map to</param>
        /// <param name="contentToMapFrom">IPublished content to map from</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <param name="concatenateToExistingValue">Flag for if we want to concatenate the value to the existing value</param>
        /// <param name="concatenationSeperator">If using concatenation, use this string to separate items</param>
        /// <param name="coalesceWithExistingValue">Flag for if we want to coalesce the value to the existing value</param>
        /// <param name="stringValueFormatter">A function for transformation of the string value, if passed</param>
        /// <param name="propertySet">Set of properties to map</param>
        private void MapContentProperty<T>(T model, PropertyInfo property, IPublishedElement contentToMapFrom,
                                           string culture,
                                           IReadOnlyDictionary<string, PropertyMapping> propertyMappings,
                                           bool concatenateToExistingValue = false, string concatenationSeperator = "",
                                           bool coalesceWithExistingValue = false, 
                                           Func<string, string> stringValueFormatter = null, 
                                           PropertySet propertySet = PropertySet.All)
        {
            // If the content we are mapping from is null then we can't map it
            if (contentToMapFrom == null)
            {
                return;
            }

            // Get the property value getter for this view model property
            var propertyValueGetter = GetPropertyValueGetter(property.Name, propertyMappings);

            // First check to see if there's a condition that might mean we don't carry out the mapping
            if (propertyMappings.IsMappingConditional(property.Name) && 
                !propertyMappings.IsMappingSpecifiedAsFromRelatedProperty(property.Name) && 
                !IsMappingConditionMet(contentToMapFrom, propertyValueGetter, culture, propertyMappings[property.Name].MapIfPropertyMatches))
            {
                return;
            }

            // Set native IPublishedContent properties (using convention that names match exactly)
            var propName = GetMappedPropertyName(property.Name, propertyMappings);
            if (contentToMapFrom.GetType().GetProperty(propName) != null)
            {
                MapNativeContentProperty(model, property, contentToMapFrom, propName, concatenateToExistingValue, concatenationSeperator, coalesceWithExistingValue, stringValueFormatter, propertySet);
                return;
            }

            // Set properties that were native IPublishedContent properties in V8, but have now been moved to extension methods
            // due to dependencies.
            if (TryMapNativeExtensionProperty(model, property, contentToMapFrom, propName, concatenateToExistingValue, concatenationSeperator, coalesceWithExistingValue, stringValueFormatter, propertySet))
            {
                return;
            }

            // Set custom properties (using convention that names match but with camelCasing on IPublishedContent 
            // properties, unless override provided)
            propName = GetMappedPropertyName(property.Name, propertyMappings, true);

            // Check to see if property should be mapped using a fall-back method.
            var fallback = Fallback.To(propertyMappings.GetMappingFallbackMethod(property.Name).ToArray());

            // Check to see if property is marked with an attribute that implements IMapFromAttribute - if so, use that
            var mapFromAttribute = GetMapFromAttribute(property);
            if (mapFromAttribute != null)
            {
                SetValueFromMapFromAttribute(model, property, contentToMapFrom, mapFromAttribute, 
                    propName, culture, fallback, 
                    propertyValueGetter, concatenateToExistingValue, concatenationSeperator, coalesceWithExistingValue);
                return;
            }

            // Map properties, first checking for custom mappings
            var namedCustomMappingKey = GetNamedCustomMappingKey(property);
            var unnamedCustomMappingKey = GetUnnamedCustomMappingKey(property);
            if (HasProvidedCustomMapping(propertyMappings, property.Name, out CustomMapping providedCustomMapping))
            {
                SetValueFromCustomMapping(model, property, contentToMapFrom, providedCustomMapping, propName, fallback);
            }
            else if (_customMappings.ContainsKey(namedCustomMappingKey))
            {
                SetValueFromCustomMapping(model, property, contentToMapFrom, _customMappings[namedCustomMappingKey], propName, fallback);
            }
            else if (_customMappings.ContainsKey(unnamedCustomMappingKey))
            {
                SetValueFromCustomMapping(model, property, contentToMapFrom, _customMappings[unnamedCustomMappingKey], propName, fallback);
            }
            else
            {
                // Otherwise map types we can handle
                var value = GetPropertyValue(contentToMapFrom, propertyValueGetter, propName, culture, fallback);
                if (value == null)
                {
                    return;
                }

                // Check if we are mapping to a related IPublishedContent
                if (propertyMappings.IsMappingSpecifiedAsFromRelatedProperty(property.Name))
                {
                    // The value we have will either be:
                    //  - an Id of a related IPublishedContent
                    //  - or the related content itself (if the Umbraco Core Property Editor Converters are in use
                    //    and we have used a standard content picker)
                    //  - or a list of related content (if the Umbraco Core Property Editor Converters are in use
                    //    and we have used a multi-node picker with a single value)

                    // So, try single IPublishedContent first
                    var relatedContentToMapFrom = value as IPublishedContent;

                    // If not, try a list and take the first if it exists
                    if (relatedContentToMapFrom == null)
                    {
                        var listOfRelatedContent = value as IEnumerable<IPublishedContent>;
                        relatedContentToMapFrom = listOfRelatedContent?.FirstOrDefault();
                    }

                    // If it's not already IPublishedContent, now check using Id
                    if (relatedContentToMapFrom == null && int.TryParse(value.ToString(), out int relatedId))
                    {
                        relatedContentToMapFrom = _umbracoHelper.Content(relatedId);
                    }

                    // If we have a related content item...
                    if (relatedContentToMapFrom == null)
                    {
                        return;
                    }

                    // Check to see if there's a condition that might mean we don't carry out the mapping (on the related content)
                    if (propertyMappings.IsMappingConditional(property.Name) &&
                        !IsMappingConditionMet(relatedContentToMapFrom, propertyValueGetter, culture, propertyMappings[property.Name].MapIfPropertyMatches))
                    {
                        return;
                    }

                    var relatedPropName = propertyMappings[property.Name].SourceRelatedProperty;

                    // Get the mapped field from the related content
                    if (relatedContentToMapFrom.GetType().GetProperty(relatedPropName) != null)
                    {
                        // Got a native field
                        MapNativeContentProperty(model, property, relatedContentToMapFrom, relatedPropName, concatenateToExistingValue, concatenationSeperator, coalesceWithExistingValue, stringValueFormatter, propertySet);
                    }
                    else
                    {
                        // Otherwise look at a doc type field
                        value = GetPropertyValue(relatedContentToMapFrom, propertyValueGetter, relatedPropName, culture);
                        if (value != null)
                        {
                            // Map primitive types
                            SetTypedPropertyValue(model, property, value.ToString(), concatenateToExistingValue, concatenationSeperator, coalesceWithExistingValue, stringValueFormatter, propertySet);
                        }
                    }
                }
                else if (!property.PropertyType.IsSimpleType())
                {
                    // Via property converters, we can get back IPublishedContent instances automatically.
                    // If that's the case and we are mapping to a complex sub-type, we can "automap" it.
                    // We have to use reflection to do this as the type parameter for the sub-type on the model is only known at run-time.
                    // All mapping customisations are expected to to be implemented as attributes on the sub-type (as we can't pass them in
                    // in the dictionary)
                    if (value is IPublishedContent)
                    {
                        typeof(PublishedContentMapper)
                            .GetMethod("MapIPublishedContent", BindingFlags.NonPublic | BindingFlags.Instance)
                            .MakeGenericMethod(property.PropertyType)
                            .Invoke(this, new[] { (IPublishedContent)value, property.GetValue(model), culture });
                    }
                    else if (value is IEnumerable<IPublishedContent> && property.PropertyType.GetInterface("IEnumerable") != null)
                    {
                        var collectionPropertyType = GetGenericCollectionType(property);
                        typeof(PublishedContentMapper)
                            .GetMethod("MapCollectionOfIPublishedContent", BindingFlags.NonPublic | BindingFlags.Instance)
                            .MakeGenericMethod(collectionPropertyType)
                            .Invoke(this, new[] { (IEnumerable<IPublishedElement>)value, property.GetValue(model), culture, null });
                    }
                    else if (value is IPublishedElement)
                    {
                        typeof(PublishedContentMapper)
                            .GetMethod("MapIPublishedElement", BindingFlags.NonPublic | BindingFlags.Instance)
                            .MakeGenericMethod(property.PropertyType)
                            .Invoke(this, new[] { (IPublishedContent)value, property.GetValue(model), culture });
                    }
                    else if (value is IEnumerable<IPublishedElement> && property.PropertyType.GetInterface("IEnumerable") != null)
                    {
                        var collectionPropertyType = GetGenericCollectionType(property);
                        typeof(PublishedContentMapper)
                            .GetMethod("MapCollectionOfIPublishedElement", BindingFlags.NonPublic | BindingFlags.Instance)
                            .MakeGenericMethod(collectionPropertyType)
                            .Invoke(this, new[] { (IEnumerable<IPublishedElement>)value, property.GetValue(model), culture, null });
                    }
                    else if (property.PropertyType.IsInstanceOfType(value))
                    {
                        // We could also have an instance of IPropertyValueGetter in use here.
                        // If that returns a complex type and it matches the type of the view model, 
                        // we can set it here.
                        // See: https://our.umbraco.com/projects/developer-tools/umbraco-mapper/bugs-questions-suggestions/92608-setting-complex-model-properties-using-custom-ipropertyvaluegetter
                        property.SetValue(model, value);
                    }
                }
                else
                {
                    // Map primitive types
                    SetTypedPropertyValue(model, property, value.ToString(), concatenateToExistingValue, concatenationSeperator, coalesceWithExistingValue, stringValueFormatter, propertySet);
                }
            }
        }



        /// <summary>
        /// Helper to determine the type of a generic collection
        /// </summary>
        /// <param name="property">Property info for collection</param>
        /// <returns>Type of collection</returns>
        private static Type GetGenericCollectionType(PropertyInfo property) =>
            property.PropertyType.GetTypeInfo().GenericTypeArguments[0];

        /// <summary>
        /// Helper method to set a property value using an <see cref="IMapFromAttribute"/>
        /// </summary>
        /// <typeparam name="T">Type of view model</typeparam>
        /// <param name="model">Instance of view model</param>
        /// <param name="property">Property of view model to map to</param>
        /// <param name="contentToMapFrom">IPublished content to map from</param>
        /// <param name="mapFromAttribute">Instance of <see cref="IMapFromAttribute"/> to use for mapping</param>
        /// <param name="propName">Name of property to map to</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="fallback">Fallback method(s) to use when content not found</param>
        /// <param name="propertyValueGetter">Type implementing <see cref="IPropertyValueGetter"/> with method to get property value</param>
        /// <param name="concatenateToExistingValue">Flag for if we want to concatenate the value to the existing value</param>
        /// <param name="concatenationSeperator">If using concatenation, use this string to separate items</param>
        /// <param name="coalesceWithExistingValue">Flag for if we want to coalesce the value to the existing value</param>
        private void SetValueFromMapFromAttribute<T>(T model, PropertyInfo property, IPublishedElement contentToMapFrom,
                                                     IMapFromAttribute mapFromAttribute, string propName,
                                                     string culture,
                                                     Fallback fallback, IPropertyValueGetter propertyValueGetter,
                                                     bool concatenateToExistingValue, string concatenationSeperator,
                                                     bool coalesceWithExistingValue)
        {
            var value = GetPropertyValue(contentToMapFrom, propertyValueGetter, propName, culture, fallback);

            // If mapping to a string, we should manipulate the mapped value to make use of the concatenation and coalescing 
            // settings, if provided.
            // So we need to save the current value.
            var currentStringValue = string.Empty;
            if (AreMappingToString(property))
            {
                currentStringValue = GetStringValueOrEmpty(model, property);
            }
            
            mapFromAttribute.SetPropertyValue(value, property, model, new MappingContext(this, _httpContextAccessor.HttpContext));

            if (!AreMappingToString(property))
            {
                return;
            }

            SetStringValueFromMapFromAttribute(model, property, concatenateToExistingValue, concatenationSeperator, coalesceWithExistingValue, currentStringValue);
        }

        private static bool AreMappingToString(PropertyInfo property) => property.PropertyType.Name == "String";

        private static string GetStringValueOrEmpty<T>(T model, PropertyInfo property) => property.GetValue(model)?.ToString() ?? string.Empty;

        /// <summary>
        /// Helper method to set a string property value, maniplate the mapped value to 
        /// make use of the concatenation and coalescing settings, and the string value formatter, if provided.
        /// </summary>
        /// <typeparam name="T">Type of view model</typeparam>
        /// <param name="model">Instance of view model</param>
        /// <param name="property">Property of view model to map to</param>
        /// <param name="concatenateToExistingValue">Flag for if we want to concatenate the value to the existing value</param>
        /// <param name="concatenationSeperator">If using concatenation, use this string to separate items</param>
        /// <param name="coalesceWithExistingValue">Flag for if we want to coalesce the value to the existing value</param>
        /// <param name="currentStringValue">Existing string value</param>
        private static void SetStringValueFromMapFromAttribute<T>(T model, PropertyInfo property,
                                                                    bool concatenateToExistingValue, string concatenationSeperator,
                                                                    bool coalesceWithExistingValue, string currentStringValue)
        {
            // If mapping to a string, we should maniplate the mapped value to make use of the concatenation and coalescing settings, and the string value formatter, if provided.
            if (concatenateToExistingValue)
            {
                var stringValue = GetStringValueOrEmpty(model, property);
                var prefixValueWith = currentStringValue + concatenationSeperator;
                property.SetValue(model, prefixValueWith + stringValue);
            }
            else if (coalesceWithExistingValue && string.IsNullOrWhiteSpace(currentStringValue))
            {
                var stringValue = GetStringValueOrEmpty(model, property);
                property.SetValue(model, stringValue);
            }
        }

        /// <summary>
        /// Helper to check if particular property has a provided CustomMapping
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <param name="customMapping">Provided custom mapping returned via out parameter</param>
        /// <returns>True if mapping should be from child property</returns>
        private static bool HasProvidedCustomMapping(IReadOnlyDictionary<string, PropertyMapping> propertyMappings,
                                                     string propName,
                                                     out CustomMapping customMapping)
        {
            if (propertyMappings.ContainsKey(propName) &&
                propertyMappings[propName].CustomMappingType != null &&
                !string.IsNullOrEmpty(propertyMappings[propName].CustomMappingMethod))
            {
                customMapping = InstantiateCustomMappingDelegateFromAttributeFields(propertyMappings[propName].CustomMappingType, propertyMappings[propName].CustomMappingMethod);
                return true;
            }

            customMapping = null;
            return false;
        }

        /// <summary>
        /// Helper to validate and create a <see cref="CustomMapping"/> based on provided type and method
        /// </summary>
        /// <param name="customMappingType">Type containing custom mapping</param>
        /// <param name="customMappingMethodName">Method implementing custom mapping</param>
        /// <returns>Instance of <see cref="CustomMapping"/></returns>
        private static CustomMapping InstantiateCustomMappingDelegateFromAttributeFields(Type customMappingType, string customMappingMethodName)
        {
            var customMappingMethod = customMappingType.GetMethodFromTypeAndMethodName(customMappingMethodName);
            if (customMappingMethod == null)
            {
                return null;
            }

            return (CustomMapping)Delegate.CreateDelegate(typeof(CustomMapping), customMappingMethod);
        }

        /// <summary>
        /// Helper method to set a property value using a CustomMapping
        /// </summary>
        /// <typeparam name="T">Type of view model</typeparam>
        /// <param name="model">Instance of view model</param>
        /// <param name="property">Property of view model to map to</param>
        /// <param name="contentToMapFrom">IPublished content to map from</param>
        /// <param name="customMapping">Instance of <see cref="CustomMapping"/> to use for mapping</param>
        /// <param name="propName">Name of property to map to</param>
        /// <param name="fallback">Fallback method(s) to use when content not found</param>
        private void SetValueFromCustomMapping<T>(T model, PropertyInfo property, IPublishedElement contentToMapFrom,
                                                  CustomMapping customMapping, string propName, Fallback fallback)
        {
            var value = customMapping(this, contentToMapFrom, _publishedValueFallback, _publishedUrlProvider, propName, fallback);
            if (value != null)
            {
                property.SetValue(model, value);
            }
        }

        /// <summary>
        /// Helper method to get the type to use to retrieve property values
        /// </summary>
        /// <param name="propName">Name of property to map to</param>
        /// <param name="propertyMappings">Set of property mappings, for use when convention mapping based on name is not sufficient</param>
        /// <returns>Name of property to map from</returns>
        private IPropertyValueGetter GetPropertyValueGetter(string propName, IReadOnlyDictionary<string, PropertyMapping> propertyMappings)
        {
            if (!propertyMappings.ContainsKey(propName) || propertyMappings[propName].PropertyValueGetter == null)
            {
                return _propertyValueGetter;
            }

            var propertyValueGetterType = propertyMappings[propName].PropertyValueGetter;
            if (!typeof(IPropertyValueGetter).IsAssignableFrom(propertyValueGetterType))
            {
                throw new InvalidOperationException(
                    $"The type provided as the PropertyValueGetter for the property {propName} must implement IPropertyValueGetter");
            }

            return Activator.CreateInstance(propertyValueGetterType) as IPropertyValueGetter;
        }

        /// <summary>
        /// Wrapper for retrieving a property value to allow override of the method used instead of the standard GetPropertyValue
        /// </summary>
        /// <param name="content">IPublished content to map from</param>
        /// <param name="propertyValueGetter">Type implementing <see cref="IPropertyValueGetter"/> with method to get property value</param>
        /// <param name="propName">Property alias</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="fallback">Fallback method(s) to use when content not found</param>
        /// <returns>Property value</returns>
        private object GetPropertyValue(IPublishedElement content, 
                                        IPropertyValueGetter propertyValueGetter, 
                                        string propName, 
                                        string culture,
                                        Fallback fallback = default(Fallback))
        {
            return propertyValueGetter.GetPropertyValue(content, _publishedValueFallback, propName, culture, null, fallback);
        }

        /// <summary>
        /// Helper to check if a mapping conditional applies
        /// </summary>
        /// <param name="contentToMapFrom">IPublished content to map from</param>
        /// <param name="propertyValueGetter">Type implementing <see cref="IPropertyValueGetter"/> with method to get property value</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="mapIfPropertyMatches">Property alias and value to match</param>
        /// <returns>True if mapping condition is met</returns>
        private bool IsMappingConditionMet(IPublishedElement contentToMapFrom, 
                                           IPropertyValueGetter propertyValueGetter, 
                                           string culture, 
                                           KeyValuePair<string, string> mapIfPropertyMatches)
        {
            var conditionalPropertyAlias = mapIfPropertyMatches.Key;
            var conditionalPropertyValue = GetPropertyValue(contentToMapFrom, propertyValueGetter, conditionalPropertyAlias, culture);
            return conditionalPropertyValue != null && 
                string.Equals(conditionalPropertyValue.ToString(), mapIfPropertyMatches.Value, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Helper to map a native IPublishedContent property to a view model property
        /// </summary>
        /// <typeparam name="T">Type of view model to map to</typeparam>
        /// <param name="model">View model to map to</param>
        /// <param name="property">Property to map to</param>
        /// <param name="contentToMapFrom">IPublishedContent instance to map from</param>
        /// <param name="propName">Name of property to map from</param>
        /// <param name="concatenateToExistingValue">Flag for if we want to concatenate the value to the existing value</param>
        /// <param name="concatenationSeperator">If using concatenation, use this string to separate items</param>
        /// <param name="coalesceWithExistingValue">Flag for if we want to coalesce the value to the existing value</param>
        /// <param name="stringValueFormatter">A function for transformation of the string value, if passed</param>
        /// <param name="propertySet">Set of properties to map. This function will only run for All and Native</param>
        private static void MapNativeContentProperty<T>(T model, PropertyInfo property,
                                                        IPublishedElement contentToMapFrom, string propName,
                                                        bool concatenateToExistingValue = false, string concatenationSeperator = "",
                                                        bool coalesceWithExistingValue = false,
                                                        Func<string, string> stringValueFormatter = null, 
                                                        PropertySet propertySet = PropertySet.All)
        {
            if (propertySet != PropertySet.All && propertySet != PropertySet.Native)
            {
                return;
            }

            var value = contentToMapFrom.GetType().GetProperty(propName).GetValue(contentToMapFrom);

            MapNativeContentPropertyValue(model, property, 
                concatenateToExistingValue, concatenationSeperator, coalesceWithExistingValue, 
                stringValueFormatter, value);
        }

        /// <summary>
        /// Helper to map to what were native IPublishedContent properties in V8 but now are evaluated from extension methods
        /// with dependency arguments.
        /// </summary>
        /// <typeparam name="T">Type of view model to map to</typeparam>
        /// <param name="model">View model to map to</param>
        /// <param name="property">Property to map to</param>
        /// <param name="contentToMapFrom">IPublishedContent instance to map from</param>
        /// <param name="propName">Name of property to map from</param>
        /// <param name="concatenateToExistingValue">Flag for if we want to concatenate the value to the existing value</param>
        /// <param name="concatenationSeperator">If using concatenation, use this string to separate items</param>
        /// <param name="coalesceWithExistingValue">Flag for if we want to coalesce the value to the existing value</param>
        /// <param name="stringValueFormatter">A function for transformation of the string value, if passed</param>
        /// <param name="propertySet">Set of properties to map. This function will only run for All and Native</param>
        private bool TryMapNativeExtensionProperty<T>(T model, PropertyInfo property,
                                                      IPublishedElement contentToMapFrom, string propName,
                                                      bool concatenateToExistingValue = false, string concatenationSeperator = "",
                                                      bool coalesceWithExistingValue = false,
                                                      Func<string, string> stringValueFormatter = null,
                                                      PropertySet propertySet = PropertySet.All)
        {
            if (propertySet != PropertySet.All && propertySet != PropertySet.Native)
            {
                return false;
            }

            object value = null;
            var publishedContent = contentToMapFrom as IPublishedContent;
            switch (propName)
            {
                case "CreatorName":
                    value = publishedContent?.CreatorName(_userService);
                    break;
                case "WriterName":
                    value = publishedContent?.WriterName(_userService);
                    break;
                case "Urll":
                    value = publishedContent?.Url(_publishedUrlProvider);
                    break;
            }

            if (value == null)
            {
                return false;
            }

            MapNativeContentPropertyValue(model, property,
                concatenateToExistingValue, concatenationSeperator, coalesceWithExistingValue,
                stringValueFormatter, value);
            return true;
        }

        /// <summary>
        /// Helper to map a value read from a native IPublishedContent property to a view model property
        /// </summary>
        /// <typeparam name="T">Type of view model to map to</typeparam>
        /// <param name="model">View model to map to</param>
        /// <param name="property">Property to map to</param>
        /// <param name="concatenateToExistingValue">Flag for if we want to concatenate the value to the existing value</param>
        /// <param name="concatenationSeperator">If using concatenation, use this string to separate items</param>
        /// <param name="coalesceWithExistingValue">Flag for if we want to coalesce the value to the existing value</param>
        /// <param name="stringValueFormatter">A function for transformation of the string value, if passed</param>
        /// <param name="value">Value to map</param>
        private static void MapNativeContentPropertyValue<T>(T model, PropertyInfo property,
                                                               bool concatenateToExistingValue, string concatenationSeperator,
                                                               bool coalesceWithExistingValue,
                                                               Func<string, string> stringValueFormatter,
                                                               object value)
        {
            // If we are mapping to a string, make sure to call ToString().  That way even if the source property is numeric, it'll be mapped.
            // Concatenation and coalescing only supported/makes sense in this case too.
            if (property.PropertyType.Name == "String")
            {
                var stringValue = value.ToString();
                if (concatenateToExistingValue)
                {
                    var prefixValueWith = property.GetValue(model) + concatenationSeperator;
                    property.SetValue(model, prefixValueWith + stringValue);
                }
                else if (coalesceWithExistingValue)
                {
                    // Check is existing value and only set if it's null, empty or whitespace
                    var existingValue = property.GetValue(model);
                    if (existingValue == null || string.IsNullOrWhiteSpace(existingValue.ToString()))
                    {
                        property.SetValue(model, stringValue);
                    }
                }
                else if (stringValueFormatter != null)
                {
                    property.SetValue(model, stringValueFormatter(stringValue));
                }
                else
                {
                    property.SetValue(model, stringValue);
                }
            }
            else
            {
                try
                {
                    property.SetValue(model, value);
                }
                catch (Exception ex)
                {
                    throw new PropertyMappingException(property, value, ex);
                }
            }
        }

        /// <summary>
        /// Maps a collection of IPublishedContent to the passed view model
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="contentCollection">Collection of IPublishedContent</param>
        /// <param name="modelCollection">Collection from view model to map to</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient.  Can also indicate the level from which the map should be made above the current content node.  This allows you to pass the level in above the current content for where you want to map a particular property.  E.g. passing { "heading", 1 } will get the heading from the node one level up.</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        /// <remarks>
        /// This method is created purely to support making a call to mapping a collection via reflection, to avoid the ambiguous match exception caused 
        /// by having multiple overloads.
        /// </remarks>
#pragma warning disable S1144 // Unused private types or members should be removed (as is used, via refelection call)
        private IPublishedContentMapper MapCollectionOfIPublishedContent<T>(IEnumerable<IPublishedContent> contentCollection,
                                                                   IList<T> modelCollection,
                                                                   string culture,
                                                                   Dictionary<string, PropertyMapping> propertyMappings) where T : class, new()
        {
            return MapCollection<T>(contentCollection, modelCollection, culture, propertyMappings);
        }
#pragma warning restore S1144 // Unused private types or members should be removedB

        /// <summary>
        /// Maps a single IPublishedContent to the passed view model
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="content">Single IPublishedContent</param>
        /// <param name="model">Model to map to</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        /// <remarks>
        /// This method is created purely to support making a call to mapping a collection via reflection, to avoid the ambiguous match exception caused 
        /// by having multiple overloads.
        /// </remarks>
#pragma warning disable S1144 // Unused private types or members should be removed (as is used, via refelection call)
        private IPublishedContentMapper MapIPublishedContent<T>(IPublishedContent content, T model, string culture) where T : class, new()
        {
            return Map<T>(content, model, culture);
        }
#pragma warning restore S1144 // Unused private types or members should be removed

        /// <summary>
        /// Maps a collection of IPublishedElement to the passed view model
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="contentCollection">Collection of IPublishedElement</param>
        /// <param name="modelCollection">Collection from view model to map to</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <param name="propertyMappings">Optional set of property mappings, for use when convention mapping based on name is not sufficient.  Can also indicate the level from which the map should be made above the current content node.  This allows you to pass the level in above the current content for where you want to map a particular property.  E.g. passing { "heading", 1 } will get the heading from the node one level up.</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        /// <remarks>
        /// This method is created purely to support making a call to mapping a collection via reflection, to avoid the ambiguous match exception caused 
        /// by having multiple overloads.
        /// </remarks>
#pragma warning disable S1144 // Unused private types or members should be removed (as is used, via refelection call)
        private IPublishedContentMapper MapCollectionOfIPublishedElement<T>(IEnumerable<IPublishedElement> contentCollection,
                                                                   IList<T> modelCollection,
                                                                   string culture,
                                                                   Dictionary<string, PropertyMapping> propertyMappings) where T : class, new()
        {
            return MapCollection<T>(contentCollection, modelCollection, culture, propertyMappings);
        }
#pragma warning restore S1144 // Unused private types or members should be removedB

        /// <summary>
        /// Maps a single IPublishedElement to the passed view model
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="content">Single IPublishedElement</param>
        /// <param name="model">Model to map to</param>
        /// <param name="culture">Culture to use when retrieving content</param>
        /// <returns>Instance of IUmbracoMapper</returns>
        /// <remarks>
        /// This method is created purely to support making a call to mapping a collection via reflection, to avoid the ambiguous match exception caused 
        /// by having multiple overloads.
        /// </remarks>
#pragma warning disable S1144 // Unused private types or members should be removed (as is used, via refelection call)
        private IPublishedContentMapper MapIPublishedElement<T>(IPublishedElement content, T model, string culture) where T : class, new()
        {
            return Map<T>(content, model, culture);
        }
#pragma warning restore S1144 // Unused private types or members should be removed

        /// <summary>
        /// Helper to set the value of a property from a dictionary key value
        /// </summary>
        /// <typeparam name="T">View model type</typeparam>
        /// <param name="model">View model to map to</param>
        /// <param name="property">Property of view model to map to</param>
        /// <param name="dictionaryKey">Dictionary key</param>
        private void SetValueFromDictionary<T>(T model, PropertyInfo property, string dictionaryKey) where T : class
        {
            property.SetValue(model, _umbracoHelper.GetDictionaryValue(dictionaryKey));
        }
    }
}
