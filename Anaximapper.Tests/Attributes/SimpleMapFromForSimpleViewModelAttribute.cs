﻿namespace Anaximapper.Tests.Attributes
{
    using Anaximapper;
    using System;
    using System.Reflection;

    [AttributeUsage(AttributeTargets.Property)]
    public class SimpleMapFromForSimpleViewModelAttribute : Attribute, IMapFromAttribute
    {
        public void SetPropertyValue<T>(object fromObject, PropertyInfo property, T model, MappingContext context) =>
            property.SetValue(model, new SimpleViewModel { Id = 1001, Name = "Child item" });
    }
}
