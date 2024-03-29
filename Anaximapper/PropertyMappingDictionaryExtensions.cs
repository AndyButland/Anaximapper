﻿namespace Anaximapper
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class PropertyMappingDictionaryExtensions
    {
        /// <summary>
        /// Helper to check if particular property has a conditional mapping
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <returns>True if mapping should be from child property</returns>
        public static bool IsMappingConditional(this IReadOnlyDictionary<string, PropertyMapping> propertyMappings, string propName)
        {
            return propertyMappings.ContainsKey(propName) &&
                   !string.IsNullOrEmpty(propertyMappings[propName].MapIfPropertyMatches.Key);
        }

        /// <summary>
        /// Helper to check if particular property should be mapped from a related property
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <returns>True if mapping should be from child property</returns>
        public static bool IsMappingSpecifiedAsFromRelatedProperty(this IReadOnlyDictionary<string, PropertyMapping> propertyMappings, string propName)
        {
            return propertyMappings.ContainsKey(propName) &&
                   !string.IsNullOrEmpty(propertyMappings[propName].SourceRelatedProperty);
        }

        /// <summary>
        /// Helper to check if particular property should be mapped from a child property
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <returns>True if mapping should be from child property</returns>
        public static bool IsMappingFromChildProperty(this IReadOnlyDictionary<string, PropertyMapping> propertyMappings, string propName)
        {
            return propertyMappings.ContainsKey(propName) &&
                   !string.IsNullOrEmpty(propertyMappings[propName].SourceChildProperty);
        }

        /// <summary>
        /// Helper to check if particular property is to be bound to a dictionary value
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <returns>True if mapping should be from child property</returns>
        public static bool IsMappingFromDictionaryValue(this IReadOnlyDictionary<string, PropertyMapping> propertyMappings, string propName)
        {
            return propertyMappings.ContainsKey(propName) && !string.IsNullOrEmpty(propertyMappings[propName].DictionaryKey);
        }

        /// <summary>
        /// Helper to retrieve the string value formatting function for the property mapping if available
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <returns>Formatting function if found, otherwise null</returns>
        public static Func<string, string> GetStringValueFormatter(this IReadOnlyDictionary<string, PropertyMapping> propertyMappings, string propName)
        {
            if (propertyMappings != null &&
                propertyMappings.ContainsKey(propName) &&
                propertyMappings[propName].StringValueFormatter != null)
            {
                return propertyMappings[propName].StringValueFormatter;
            }

            return null;
        }

        /// <summary>
        /// Helper to check if particular property should be mapped from the concatenation or coalescing of more than one 
        /// source property
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <returns>Type of multiple mapping operation</returns>
        public static MultiplePropertyMappingOperation GetMultiplePropertyMappingOperation(this IReadOnlyDictionary<string, PropertyMapping> propertyMappings, string propName)
        {
            var result = MultiplePropertyMappingOperation.None;

            if (!propertyMappings.ContainsKey(propName))
            {
                return result;
            }

            if (propertyMappings[propName].SourcePropertiesForConcatenation != null &&
                propertyMappings[propName].SourcePropertiesForConcatenation.Any())
            {
                result = MultiplePropertyMappingOperation.Concatenate;
            }
            else if (propertyMappings[propName].SourcePropertiesForCoalescing != null &&
                     propertyMappings[propName].SourcePropertiesForCoalescing.Any())
            {
                result = MultiplePropertyMappingOperation.Coalesce;
            }

            return result;
        }

        /// <summary>
        /// Helper to check if property is ignored
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <returns>True if ignored</returns>
        public static bool IsPropertyIgnored(this IReadOnlyDictionary<string, PropertyMapping> propertyMappings, string propName)
        {
            return propertyMappings.ContainsKey(propName) && propertyMappings[propName].Ignore;
        }

        /// <summary>
        /// Helper to check if property should be mapped from the prevalue label
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <returns>True if should map from prevalue label</returns>
        public static bool ShouldMapFromPreValue(this IReadOnlyDictionary<string, PropertyMapping> propertyMappings, string propName)
        {
            return propertyMappings.ContainsKey(propName) && propertyMappings[propName].MapFromPreValue;
        }

        /// <summary>
        /// Helper to check if particular property should be mapped recursively
        /// </summary>
        /// <param name="propertyMappings">Dictionary of mapping convention overrides</param>
        /// <param name="propName">Name of property to map to</param>
        /// <returns>True if mapping should be from child property</returns>
        public static IEnumerable<int> GetMappingFallbackMethod(this IReadOnlyDictionary<string, PropertyMapping> propertyMappings, string propName)
        {
            if (!propertyMappings.ContainsKey(propName))
            {
                return Enumerable.Empty<int>();
            }

            // If using the more flexible FallBackMethods, this takes priority.
            if (propertyMappings[propName].FallbackMethods != null && propertyMappings[propName].FallbackMethods.Any())
            {
                return propertyMappings[propName].FallbackMethods;
            }

            // Otherwise we use MapRecursively
            if (propertyMappings[propName].MapRecursively)
            {
                return new List<int> { Constants.FallbackToAncestors };
            }

            return Enumerable.Empty<int>();
        }
    }
}
