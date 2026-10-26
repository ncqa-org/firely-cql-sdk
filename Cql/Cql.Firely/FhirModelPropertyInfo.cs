/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Fhir.Introspection;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Hl7.Cql.Fhir
{
    internal class FhirModelPropertyInfo : PropertyInfo
    {
        private readonly PropertyInfo propertyInfo;

        public FhirModelPropertyInfo(PropertyInfo propertyInfo, PropertyMapping mapping)
        {
            this.propertyInfo = propertyInfo;
            Mapping = mapping;
        }

        public override PropertyAttributes Attributes => propertyInfo.Attributes;

        public override bool CanRead => propertyInfo.CanRead;

        public override bool CanWrite => propertyInfo.CanWrite;

        public override Type PropertyType => propertyInfo.PropertyType;

        public override Type? DeclaringType => propertyInfo.DeclaringType;

        public override string Name => propertyInfo.Name;

        public override Type? ReflectedType => propertyInfo.ReflectedType;

        public PropertyMapping Mapping { get; }

        // PropertyMapping.GetValue/SetValue were removed in Firely SDK 6.x, so we build our own
        // compiled (fast) accessors lazily from the native property.
        private Func<object, object?>? getter;
        private Action<object, object?>? setter;

        private Func<object, object?> Getter => getter ??= BuildGetter();

        private Action<object, object?> Setter => setter ??= BuildSetter();

        private Func<object, object?> BuildGetter()
        {
            var declaringType = propertyInfo.DeclaringType!;
            var instance = Expression.Parameter(typeof(object), "instance");
            var body = Expression.Convert(
                Expression.Property(Expression.Convert(instance, declaringType), propertyInfo),
                typeof(object));
            return Expression.Lambda<Func<object, object?>>(body, instance).Compile();
        }

        private Action<object, object?> BuildSetter()
        {
            var declaringType = propertyInfo.DeclaringType!;
            var instance = Expression.Parameter(typeof(object), "instance");
            var value = Expression.Parameter(typeof(object), "value");
            var body = Expression.Assign(
                Expression.Property(Expression.Convert(instance, declaringType), propertyInfo),
                Expression.Convert(value, propertyInfo.PropertyType));
            return Expression.Lambda<Action<object, object?>>(body, instance, value).Compile();
        }

        public override MethodInfo[] GetAccessors(bool nonPublic)
        {
            return propertyInfo.GetAccessors(nonPublic);
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return propertyInfo.GetCustomAttributes(inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return propertyInfo.GetCustomAttributes(attributeType, inherit);
        }

        public override MethodInfo? GetGetMethod(bool nonPublic)
        {
            return propertyInfo.GetGetMethod(nonPublic);
        }

        public override ParameterInfo[] GetIndexParameters()
        {
            return propertyInfo.GetIndexParameters();
        }

        public override MethodInfo? GetSetMethod(bool nonPublic)
        {
            return propertyInfo.GetSetMethod(nonPublic);
        }

        // Override to use a fast compiled getter
        public override object? GetValue(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture) =>
            obj is not null ? Getter(obj) : null;

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return propertyInfo.IsDefined(attributeType, inherit);
        }

        // Override to use a fast compiled setter
        public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture)
        {
            if (obj is not null)
                Setter(obj, value);
        }
    }
}
