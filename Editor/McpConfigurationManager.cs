using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityNaturalMCPExtension.Editor
{
    public static class McpConfigurationManager
    {
        private static readonly Dictionary<Type, object> DefaultConfigurationCache = new();
        private static readonly Dictionary<string, Type> ConfigurationTypeCache = new();

        public static T ParseConfiguration<T>(string jsonConfig) where T : class
        {
            if (string.IsNullOrEmpty(jsonConfig))
                return GetDefaultConfiguration<T>();

            try
            {
                var config = JsonConvert.DeserializeObject<T>(jsonConfig);
                return config ?? GetDefaultConfiguration<T>();
            }
            catch (JsonException ex)
            {
                Debug.LogWarning($"Failed to parse configuration JSON for type {typeof(T).Name}: {ex.Message}");
                return GetDefaultConfiguration<T>();
            }
        }

        public static ValidationResult ValidateConfiguration<T>(T config) where T : class
        {
            if (config == null)
                return ValidationResult.Failure("Configuration is null");

            var validationErrors = new List<string>();
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var value = property.GetValue(config);
                var validationResult = ValidateProperty(property, value);
                
                if (!validationResult.IsValid)
                    validationErrors.AddRange(validationResult.Errors);
            }

            return validationErrors.Any() 
                ? ValidationResult.Failure(validationErrors.ToArray())
                : ValidationResult.Success();
        }

        public static Dictionary<string, object> GetDefaultConfiguration(Type configurationType)
        {
            if (DefaultConfigurationCache.TryGetValue(configurationType, out var cachedDefault))
                return (Dictionary<string, object>)cachedDefault;

            var defaultConfig = new Dictionary<string, object>();
            var properties = configurationType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                var defaultValue = GetDefaultValue(property);
                if (defaultValue != null)
                    defaultConfig[property.Name] = defaultValue;
            }

            DefaultConfigurationCache[configurationType] = defaultConfig;
            return defaultConfig;
        }

        public static T GetDefaultConfiguration<T>() where T : class
        {
            var defaultDict = GetDefaultConfiguration(typeof(T));
            var json = JsonConvert.SerializeObject(defaultDict);
            return JsonConvert.DeserializeObject<T>(json) ?? Activator.CreateInstance<T>();
        }

        public static bool TryParseConfiguration<T>(string jsonConfig, out T config, out ValidationResult validationResult) where T : class
        {
            config = null;
            validationResult = ValidationResult.Success();

            try
            {
                config = ParseConfiguration<T>(jsonConfig);
                validationResult = ValidateConfiguration(config);
                return validationResult.IsValid;
            }
            catch (Exception ex)
            {
                validationResult = ValidationResult.Failure($"Configuration parsing failed: {ex.Message}");
                return false;
            }
        }


        public static Type ResolveConfigurationType(string typeName)
        {
            if (ConfigurationTypeCache.TryGetValue(typeName, out var cachedType))
                return cachedType;

            var type = Type.GetType(typeName) ?? 
                       AppDomain.CurrentDomain.GetAssemblies()
                           .SelectMany(a => a.GetTypes())
                           .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);

            if (type != null)
                ConfigurationTypeCache[typeName] = type;

            return type;
        }

        public static string SerializeConfiguration<T>(T config) where T : class
        {
            if (config == null)
                return "{}";

            try
            {
                return JsonConvert.SerializeObject(config, Formatting.Indented);
            }
            catch (JsonException ex)
            {
                Debug.LogError($"Failed to serialize configuration: {ex.Message}");
                return "{}";
            }
        }

        public static Dictionary<string, object> MergeConfigurations(Dictionary<string, object> baseConfig, Dictionary<string, object> overrideConfig)
        {
            if (baseConfig == null)
                return overrideConfig ?? new Dictionary<string, object>();
            
            if (overrideConfig == null)
                return baseConfig;

            var merged = new Dictionary<string, object>(baseConfig);
            
            foreach (var kvp in overrideConfig)
            {
                merged[kvp.Key] = kvp.Value;
            }

            return merged;
        }

        private static ValidationResult ValidateProperty(PropertyInfo property, object value)
        {
            var errors = new List<string>();

            var requiredAttribute = property.GetCustomAttribute<RequiredAttribute>();
            if (requiredAttribute != null && (value == null || (value is string str && string.IsNullOrEmpty(str))))
            {
                errors.Add($"Property '{property.Name}' is required but was null or empty");
            }

            var rangeAttribute = property.GetCustomAttribute<RangeAttribute>();
            if (rangeAttribute != null && value is IComparable comparable)
            {
                var min = Convert.ChangeType(rangeAttribute.min, property.PropertyType);
                var max = Convert.ChangeType(rangeAttribute.max, property.PropertyType);
                
                if (comparable.CompareTo(min) < 0 || comparable.CompareTo(max) > 0)
                {
                    errors.Add($"Property '{property.Name}' value {value} is outside the valid range [{rangeAttribute.min}, {rangeAttribute.max}]");
                }
            }

            var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultValueAttribute != null && value == null)
            {
                // This is handled by setting the default value, not an error
            }

            return errors.Any() 
                ? ValidationResult.Failure(errors.ToArray())
                : ValidationResult.Success();
        }

        private static object GetDefaultValue(PropertyInfo property)
        {
            var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();
            if (defaultValueAttribute != null)
                return defaultValueAttribute.Value;

            var propertyType = property.PropertyType;

            if (propertyType.IsValueType)
                return Activator.CreateInstance(propertyType);

            if (propertyType == typeof(string))
                return string.Empty;

            if (propertyType.IsArray)
                return Array.CreateInstance(propertyType.GetElementType()!, 0);

            if (propertyType.IsGenericType)
            {
                var genericType = propertyType.GetGenericTypeDefinition();
                if (genericType == typeof(List<>))
                    return Activator.CreateInstance(propertyType);
                if (genericType == typeof(Dictionary<,>))
                    return Activator.CreateInstance(propertyType);
            }

            return null;
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; private set; }
        public string[] Errors { get; private set; }

        private ValidationResult(bool isValid, string[] errors)
        {
            IsValid = isValid;
            Errors = errors ?? Array.Empty<string>();
        }

        public static ValidationResult Success() => new(true, null);
        public static ValidationResult Failure(params string[] errors) => new(false, errors);

        public override string ToString()
        {
            return IsValid ? "Valid" : $"Invalid: {string.Join("; ", Errors)}";
        }
    }

    public class RequiredAttribute : Attribute
    {
    }
}