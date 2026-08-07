// Copyright 2026 jx
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace Compose.NET.Types;

/// <summary>
/// Reflection-based decoder that maps dictionary data (typically from parsed YAML
/// extension fields) into strongly-typed objects. This mirrors compose-go's use of
/// <c>mapstructure.Decode</c> for decoding <c>x-*</c> extension values.
/// <para>
/// Dictionary keys are matched to target type properties using case-insensitive
/// snake_case â†’ PascalCase conversion (e.g., <c>max_retries</c> â†’ <c>MaxRetries</c>).
/// Nested dictionaries are recursively decoded into complex property types.
/// </para>
/// <para>
/// This decoder relies on reflection and runtime-constructed generic types, so it
/// is not compatible with ahead-of-time compilation or trimming for arbitrary
/// target types. Callers must opt in via <see cref="Extensions.Decode{T}(string)"/>
/// and <see cref="Extensions.TryDecode{T}(string, out T)"/>, which carry the
/// corresponding annotations.
/// </para>
/// </summary>
[RequiresUnreferencedCode(
    "ExtensionDecoder uses reflection to map dictionary data to target types, which may not survive trimming.")]
[RequiresDynamicCode(
    "ExtensionDecoder constructs generic types at runtime, which is not supported with ahead-of-time compilation.")]
internal static class ExtensionDecoder
{
    /// <summary>
    /// Decodes the given value into an instance of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type. Must have a parameterless constructor.</typeparam>
    /// <param name="value">
    /// The source value. Expected to be a <see cref="Dictionary{String, Object}"/>,
    /// <see cref="Extensions"/>, or other <see cref="IDictionary{String, Object}"/>.
    /// </param>
    /// <returns>A new instance of <typeparamref name="T"/> populated from the dictionary.</returns>
    /// <exception cref="InvalidOperationException">
    /// The value is not a dictionary-like object or a property cannot be decoded.
    /// </exception>
    public static T Decode<T>(object value) where T : new()
    {
        var dict = ToDictionary(value);
        var result = new T();
        Populate(result, typeof(T), dict);
        return result;
    }

    /// <summary>
    /// Decodes the given value into an instance of the specified type.
    /// </summary>
    /// <param name="targetType">The target type. Must have a parameterless constructor.</param>
    /// <param name="value">The source value (dictionary-like).</param>
    /// <returns>A new instance of <paramref name="targetType"/> populated from the dictionary.</returns>
    internal static object Decode(Type targetType, object value)
    {
        var dict = ToDictionary(value);
        var result = Activator.CreateInstance(targetType)
                     ?? throw new InvalidOperationException(
                         $"Failed to create instance of '{targetType.Name}'. Ensure it has a parameterless constructor.");
        Populate(result, targetType, dict);
        return result;
    }

    private static void Populate(object target, Type targetType, IDictionary<string, object> dict)
    {
        var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propMap = BuildPropertyMap(properties);

        foreach (var kvp in dict)
        {
            var key = NormalizeKey(kvp.Key);
            if (!propMap.TryGetValue(key, out var prop))
                continue;

            if (!prop.CanWrite)
                continue;

            var converted = ConvertValue(kvp.Value, prop.PropertyType, kvp.Key);
            prop.SetValue(target, converted);
        }
    }

    /// <summary>
    /// Builds a lookup from normalized property names to <see cref="PropertyInfo"/>.
    /// </summary>
    private static Dictionary<string, PropertyInfo> BuildPropertyMap(PropertyInfo[] properties)
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in properties)
        {
            // Map both the raw property name and the snake_case equivalent
            var normalized = NormalizeKey(prop.Name);
            map[normalized] = prop;
            // Also store original name lowered for direct match
            map[prop.Name.ToLowerInvariant()] = prop;
        }
        return map;
    }

    /// <summary>
    /// Normalizes a key by converting it to lowercase and removing underscores/hyphens.
    /// This allows matching snake_case dict keys to PascalCase property names.
    /// e.g., "max_retries" â†’ "maxretries", "MaxRetries" â†’ "maxretries"
    /// </summary>
    private static string NormalizeKey(string key)
    {
        // Remove underscores, hyphens and lowercase everything
        // This makes "max_retries" == "MaxRetries" == "maxRetries" == "max-retries"
#if NETSTANDARD2_1
        var sb = new System.Text.StringBuilder(key.Length);
        foreach (var c in key)
        {
            if (c != '_' && c != '-')
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
#else
        return string.Create(key.Length, key, static (span, k) =>
        {
            int pos = 0;
            foreach (var c in k)
            {
                if (c != '_' && c != '-')
                    span[pos++] = char.ToLowerInvariant(c);
            }
            // Fill remaining with null chars (will be trimmed)
            for (int i = pos; i < span.Length; i++)
                span[i] = '\0';
        }).TrimEnd('\0');
#endif
    }

    /// <summary>
    /// Converts a raw value to the target property type.
    /// </summary>
    private static object? ConvertValue(object? value, Type targetType, string key)
    {
        if (value is null)
        {
            if (IsNullable(targetType))
                return null;
            // For value types, return default
            if (targetType.IsValueType)
                return Activator.CreateInstance(targetType);
            return null;
        }

        var valueType = value.GetType();

        // Unwrap Nullable<T> for comparison
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Direct assignment if types are compatible
        if (underlyingType.IsAssignableFrom(valueType))
            return value;

        // String target
        if (underlyingType == typeof(string))
            return Convert.ToString(value, CultureInfo.InvariantCulture);

        // Primitive / numeric conversions
        if (IsPrimitiveConvertible(underlyingType))
            return ConvertPrimitive(value, underlyingType, key);

        // Enum
        if (underlyingType.IsEnum)
            return ConvertEnum(value, underlyingType, key);

        // Dictionary<string, TValue>
        if (IsStringDictionary(underlyingType, out var dictValueType))
            return ConvertToDictionary(value, underlyingType, dictValueType!, key);

        // Array (must be checked before List<T> since arrays implement IList<T>)
        if (underlyingType.IsArray)
        {
            var arrayElementType = underlyingType.GetElementType()!;
            return ConvertToArray(value, arrayElementType, key);
        }

        // List<T> or IList<T>
        if (IsListType(underlyingType, out var elementType))
            return ConvertToList(value, underlyingType, elementType!, key);

        // Nested complex object (has parameterless constructor and value is dict-like)
        if (IsDictionaryLike(value) && HasParameterlessConstructor(underlyingType))
            return Decode(underlyingType, value);

        // Last resort: try Convert.ChangeType
        try
        {
            return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot convert value of type '{valueType.Name}' to property type '{targetType.Name}' for key '{key}'.",
                ex);
        }
    }

    private static bool IsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    private static bool IsPrimitiveConvertible(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(float)
               || type == typeof(double) || type == typeof(decimal) || type == typeof(bool)
               || type == typeof(byte) || type == typeof(short) || type == typeof(uint)
               || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);
    }

    private static object ConvertPrimitive(object value, Type targetType, string key)
    {
        try
        {
            // Handle string â†’ bool special cases
            if (targetType == typeof(bool) && value is string boolStr)
            {
                if (bool.TryParse(boolStr, out var b))
                    return b;
                // Also accept "1"/"0", "yes"/"no"
                var lower = boolStr.ToLowerInvariant();
                if (lower == "1" || lower == "yes" || lower == "on")
                    return true;
                if (lower == "0" || lower == "no" || lower == "off")
                    return false;
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot convert '{value}' (type '{value.GetType().Name}') to '{targetType.Name}' for key '{key}'.",
                ex);
        }
    }

    private static object ConvertEnum(object value, Type enumType, string key)
    {
        if (value is string s)
        {
            // Try case-insensitive parse
            if (Enum.TryParse(enumType, s, ignoreCase: true, out var result) && result != null)
                return result;

            // Try replacing hyphens/underscores
            var cleaned = s.Replace("-", "_");
            if (Enum.TryParse(enumType, cleaned, ignoreCase: true, out result) && result != null)
                return result;

            throw new InvalidOperationException(
                $"Cannot convert string '{s}' to enum type '{enumType.Name}' for key '{key}'.");
        }

        // Numeric value
        try
        {
            return Enum.ToObject(enumType, value);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot convert '{value}' to enum type '{enumType.Name}' for key '{key}'.", ex);
        }
    }

    private static bool IsStringDictionary(Type type, out Type? valueType)
    {
        valueType = null;

        // Check Dictionary<string, T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var args = type.GetGenericArguments();
            if (args[0] == typeof(string))
            {
                valueType = args[1];
                return true;
            }
        }

        // Check IDictionary<string, T> interfaces
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                var args = iface.GetGenericArguments();
                if (args[0] == typeof(string))
                {
                    valueType = args[1];
                    return true;
                }
            }
        }

        return false;
    }

    private static object ConvertToDictionary(object value, Type dictType, Type valueType, string key)
    {
        var sourceDict = ToDictionaryLoose(value);
        if (sourceDict == null)
        {
            throw new InvalidOperationException(
                $"Cannot convert value of type '{value.GetType().Name}' to dictionary for key '{key}'.");
        }

        // Create the target dictionary
        var targetDictType = typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType);
        var targetDict = (IDictionary)Activator.CreateInstance(targetDictType)!;

        foreach (DictionaryEntry entry in sourceDict)
        {
            var k = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
            var v = ConvertValue(entry.Value, valueType, $"{key}.{k}");
            targetDict[k] = v;
        }

        return targetDict;
    }

    private static IDictionary? ToDictionaryLoose(object value)
    {
        if (value is IDictionary dict)
            return dict;
        return null;
    }

    private static bool IsListType(Type type, out Type? elementType)
    {
        elementType = null;

        // Check List<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        // Check IList<T> interface
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IList<>))
            {
                elementType = iface.GetGenericArguments()[0];
                return true;
            }
        }

        // Also check if type itself is IList<T>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    private static object ConvertToList(object value, Type listType, Type elementType, string key)
    {
        var concreteListType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(concreteListType)!;

        if (value is IEnumerable enumerable && value is not string && value is not IDictionary)
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                var converted = ConvertValue(item, elementType, $"{key}[{index}]");
                list.Add(converted);
                index++;
            }
        }
        else
        {
            // Single value â†’ wrap in list
            var converted = ConvertValue(value, elementType, key);
            list.Add(converted);
        }

        // If the target type is List<T>, return directly
        if (listType.IsAssignableFrom(concreteListType))
            return list;

        // Otherwise try to find a constructor that accepts IEnumerable<T> or similar
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        var ctor = listType.GetConstructor(new[] { enumerableType });
        if (ctor != null)
            return ctor.Invoke(new object[] { list });

        return list;
    }

    private static object ConvertToArray(object value, Type elementType, string key)
    {
        var items = new List<object?>();

        if (value is IEnumerable enumerable && value is not string && value is not IDictionary)
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                items.Add(ConvertValue(item, elementType, $"{key}[{index}]"));
                index++;
            }
        }
        else
        {
            items.Add(ConvertValue(value, elementType, key));
        }

        var array = Array.CreateInstance(elementType, items.Count);
        for (int i = 0; i < items.Count; i++)
        {
            array.SetValue(items[i], i);
        }

        return array;
    }

    private static bool IsDictionaryLike(object value)
    {
        return value is IDictionary<string, object>
               || value is Extensions
               || value is IDictionary;
    }

    private static bool HasParameterlessConstructor(Type type)
    {
        if (type.IsAbstract || type.IsInterface)
            return false;
        return type.GetConstructor(Type.EmptyTypes) != null;
    }

    private static IDictionary<string, object> ToDictionary(object value)
    {
        if (value is IDictionary<string, object> typed)
            return typed;

        if (value is Extensions extensions)
            return extensions;

        if (value is IDictionary dict)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dict)
            {
                var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
                if (entry.Value != null)
                    result[key] = entry.Value;
            }
            return result;
        }

        throw new InvalidOperationException(
            $"Cannot decode extension value of type '{value.GetType().Name}'. " +
            $"Expected a dictionary-like object (Dictionary<string, object>, Extensions, or IDictionary).");
    }
}
