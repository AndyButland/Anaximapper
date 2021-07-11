using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace Anaximapper
{
    [Serializable]
    public class PropertyMappingException : Exception
    {
        public PropertyMappingException()
            : base() { }

        public PropertyMappingException(string message)
            : base(message) { }

        public PropertyMappingException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public PropertyMappingException(string message, Exception innerException)
            : base(message, innerException) { }

        public PropertyMappingException(string format, Exception innerException, params object[] args)
            : base(string.Format(format, args), innerException) { }

        protected PropertyMappingException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        internal PropertyMappingException(PropertyInfo property, object value, Exception innerException)
            : base(CreateMessage(property, value), innerException) { }

        private static string CreateMessage(PropertyInfo property, object value) =>
            $"Could not map to property '{property.Name}' from value '{value}'";
    }
}