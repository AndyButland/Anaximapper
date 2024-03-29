﻿namespace Anaximapper
{
    using System.Reflection;

    /// <summary>
    /// Attributes which implement this interface can be applied to model properties,
    /// declaring that the property should be mapped in a specific way.
    /// </summary>
    public interface IMapFromAttribute
    {
        /// <summary>
        /// Defines how a property value should be mapped.
        /// </summary>
        /// <typeparam name="T">Model type</typeparam>
        /// <param name="fromObject">Data from which to obtain the property value</param>
        /// <param name="property">Property which we're setting the value of</param>
        /// <param name="model">Model being populated</param>
        /// <param name="context">Context containing references to mapper and HttpContext</param>
        void SetPropertyValue<T>(object fromObject, PropertyInfo property, T model, MappingContext context);
    }
}
