using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Manages component property setting and type resolution for MCP tools.
    /// Centralizes type conversion logic and property manipulation.
    /// </summary>
    public static class ComponentPropertyManager
    {
        private static readonly ConcurrentDictionary<string, Type> _typeCache = new ConcurrentDictionary<string, Type>();
        
        /// <summary>
        /// Result of a property set operation
        /// </summary>
        public struct PropertySetResult
        {
            public bool Success { get; set; }
            public string PropertyName { get; set; }
            public object Value { get; set; }
            public string ErrorMessage { get; set; }
            
            public static PropertySetResult Successful(string propertyName, object value)
            {
                return new PropertySetResult 
                { 
                    Success = true, 
                    PropertyName = propertyName,
                    Value = value 
                };
            }
            
            public static PropertySetResult Failed(string propertyName, string error)
            {
                return new PropertySetResult 
                { 
                    Success = false, 
                    PropertyName = propertyName,
                    ErrorMessage = error 
                };
            }
        }
        
        /// <summary>
        /// Sets a property on a component with automatic type conversion
        /// </summary>
        public static PropertySetResult SetProperty(Component component, string propertyName, object value, bool inPrefabMode = false)
        {
            if (component == null)
                return PropertySetResult.Failed(propertyName, "Component is null");
                
            if (string.IsNullOrEmpty(propertyName))
                return PropertySetResult.Failed(propertyName, "Property name is empty");
            
            try
            {
                var type = component.GetType();

                // First try to find as a property (public properties)
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    var convertedValue = ConvertValue(value, property.PropertyType, inPrefabMode);
                    property.SetValue(component, convertedValue);
                    return PropertySetResult.Successful(propertyName, convertedValue);
                }

                // Then try to find as a field (including private SerializeFields)
                var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var convertedValue = ConvertValue(value, field.FieldType, inPrefabMode);
                    field.SetValue(component, convertedValue);
                    return PropertySetResult.Successful(propertyName, convertedValue);
                }

                // Property/field not found
                var availableMembers = GetAvailableMembers(type);
                return PropertySetResult.Failed(propertyName, 
                    $"Property/field '{propertyName}' not found on {type.Name}. " +
                    $"Available members: {string.Join(", ", availableMembers)}");
            }
            catch (Exception e)
            {
                return PropertySetResult.Failed(propertyName, $"Exception: {e.Message}");
            }
        }
        
        /// <summary>
        /// Sets a nested property using dot notation (e.g., "material.color")
        /// </summary>
        public static PropertySetResult SetNestedProperty(Component component, string propertyPath, object value, bool inPrefabMode = false)
        {
            if (component == null)
                return PropertySetResult.Failed(propertyPath, "Component is null");
                
            if (string.IsNullOrEmpty(propertyPath))
                return PropertySetResult.Failed(propertyPath, "Property path is empty");
            
            try
            {
                var parts = propertyPath.Split('.');
                object current = component;
                Type currentType = component.GetType();
                
                // Navigate to the nested property
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    var propertyName = parts[i];
                    var property = currentType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                    var field = currentType.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (property != null)
                    {
                        current = property.GetValue(current);
                        currentType = property.PropertyType;
                    }
                    else if (field != null)
                    {
                        current = field.GetValue(current);
                        currentType = field.FieldType;
                    }
                    else
                    {
                        return PropertySetResult.Failed(propertyPath, 
                            $"Could not find property or field '{propertyName}' in path '{propertyPath}'");
                    }
                    
                    if (current == null)
                    {
                        return PropertySetResult.Failed(propertyPath, 
                            $"Null reference at '{propertyName}' in path '{propertyPath}'");
                    }
                }
                
                // Set the final property
                var finalPropertyName = parts[parts.Length - 1];
                var finalProperty = currentType.GetProperty(finalPropertyName, BindingFlags.Public | BindingFlags.Instance);
                var finalField = currentType.GetField(finalPropertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (finalProperty != null && finalProperty.CanWrite)
                {
                    var convertedValue = ConvertValue(value, finalProperty.PropertyType, inPrefabMode);
                    finalProperty.SetValue(current, convertedValue);
                    return PropertySetResult.Successful(propertyPath, convertedValue);
                }
                else if (finalField != null)
                {
                    var convertedValue = ConvertValue(value, finalField.FieldType, inPrefabMode);
                    finalField.SetValue(current, convertedValue);
                    return PropertySetResult.Successful(propertyPath, convertedValue);
                }
                else
                {
                    return PropertySetResult.Failed(propertyPath, 
                        $"Could not find writable property or field '{finalPropertyName}' at end of path");
                }
            }
            catch (Exception e)
            {
                return PropertySetResult.Failed(propertyPath, $"Exception: {e.Message}");
            }
        }
        
        /// <summary>
        /// Resolves a component type from a string name with caching
        /// </summary>
        public static Type ResolveComponentType(string componentType)
        {
            if (string.IsNullOrEmpty(componentType))
                return null;

            // Check cache first
            if (_typeCache.TryGetValue(componentType, out var cachedType))
                return cachedType;

            // Normalize component name (handle case variations)
            var normalizedName = NormalizeComponentName(componentType);

            // Common Unity assemblies to search
            var assembliesToSearch = new[]
            {
                "UnityEngine",
                "UnityEngine.CoreModule",
                "UnityEngine.PhysicsModule",
                "UnityEngine.Physics2DModule",
                "UnityEngine.AudioModule",
                "UnityEngine.AnimationModule",
                "UnityEngine.ParticleSystemModule",
                "UnityEngine.UIModule",
                "UnityEngine.TextRenderingModule",
                "UnityEngine.InputLegacyModule",
                "UnityEngine.AIModule",
                "UnityEngine.TerrainModule",
                "UnityEngine.VideoModule",
                "UnityEngine.UnityWebRequestModule",
                "UnityEngine.XRModule"
            };

            // Try exact type name first
            foreach (var assembly in assembliesToSearch)
            {
                var fullTypeName = $"UnityEngine.{normalizedName}, {assembly}";
                var type = Type.GetType(fullTypeName);
                if (type != null && IsValidComponentType(type))
                {
                    _typeCache.TryAdd(componentType, type);
                    return type;
                }
            }

            // Try without UnityEngine prefix
            foreach (var assembly in assembliesToSearch)
            {
                var fullTypeName = $"{normalizedName}, {assembly}";
                var type = Type.GetType(fullTypeName);
                if (type != null && IsValidComponentType(type))
                {
                    _typeCache.TryAdd(componentType, type);
                    return type;
                }
            }

            // Fallback: search all loaded assemblies
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if ((type.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase) ||
                             type.Name.Equals(componentType, StringComparison.OrdinalIgnoreCase)) &&
                            IsValidComponentType(type))
                        {
                            _typeCache.TryAdd(componentType, type);
                            return type;
                        }
                    }
                }
                catch
                {
                    // Skip assemblies that can't be loaded
                }
            }

            return null;
        }
        
        /// <summary>
        /// Gets suggested component names for a given input
        /// </summary>
        public static List<string> GetComponentSuggestions(string componentType)
        {
            var suggestions = new List<string>();
            var lowerInput = componentType.ToLowerInvariant();

            // Common Unity components for suggestions
            var commonComponents = new[]
            {
                "Rigidbody", "BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider",
                "AudioSource", "Light", "Camera", "MeshRenderer", "MeshFilter", "Transform",
                "Animator", "Animation", "Rigidbody2D", "BoxCollider2D", "CircleCollider2D",
                "ParticleSystem", "LineRenderer", "TrailRenderer", "SkinnedMeshRenderer",
                "CharacterController", "NavMeshAgent", "AudioListener", "Canvas", "Image",
                "Text", "Button", "Slider", "Toggle", "InputField", "Dropdown", "ScrollRect"
            };

            // Find components that start with the same letter or contain the input
            foreach (var comp in commonComponents)
            {
                var lowerComp = comp.ToLowerInvariant();
                if (lowerComp.StartsWith(lowerInput) ||
                    lowerComp.Contains(lowerInput) ||
                    LevenshteinDistance(lowerInput, lowerComp) <= 2)
                {
                    suggestions.Add(comp);
                }
            }

            return suggestions.Take(3).ToList(); // Limit to 3 suggestions
        }
        
        /// <summary>
        /// Converts a value to the target type with special handling for Unity types
        /// </summary>
        private static object ConvertValue(object value, Type targetType, bool inPrefabMode)
        {
            if (value == null)
                return null;
            
            var valueType = value.GetType();
            
            // If already the correct type, return as-is
            if (targetType.IsAssignableFrom(valueType))
                return value;
            
            // Handle Unity object references
            if (value is string stringValue && IsUnityObjectType(targetType))
            {
                return ResolveObjectReference(stringValue, targetType, inPrefabMode);
            }
            
            // Handle JToken values from JSON parsing
            if (value is JToken jToken)
            {
                value = ConvertJToken(jToken, targetType);
            }
            
            // Handle Vector types
            if (targetType == typeof(Vector2))
            {
                return ConvertToVector2(value);
            }
            else if (targetType == typeof(Vector3))
            {
                return ConvertToVector3(value);
            }
            else if (targetType == typeof(Vector4) || targetType == typeof(Color))
            {
                return ConvertToVector4OrColor(value, targetType);
            }
            else if (targetType == typeof(Quaternion))
            {
                return ConvertToQuaternion(value);
            }
            
            // Handle enums
            if (targetType.IsEnum)
            {
                if (value is string enumString)
                {
                    return Enum.Parse(targetType, enumString, true);
                }
                else if (value is int || value is long || value is float || value is double)
                {
                    return Enum.ToObject(targetType, Convert.ToInt32(value));
                }
            }
            
            // Handle primitive conversions
            if (targetType.IsPrimitive || targetType == typeof(string))
            {
                return Convert.ChangeType(value, targetType);
            }
            
            // Handle LayerMask (string only for safety)
            if (targetType == typeof(LayerMask))
            {
                if (value is string layerName)
                {
                    var layerIndex = LayerMask.NameToLayer(layerName);
                    if (layerIndex >= 0)
                        return (LayerMask)(1 << layerIndex);
                    else
                        throw new ArgumentException($"Layer '{layerName}' not found");
                }
                else
                {
                    throw new ArgumentException("LayerMask values must be provided as layer name strings, not integers");
                }
            }
            
            // Handle arrays
            if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType();
                return ConvertToArray(value, elementType, inPrefabMode);
            }
            
            // Handle generic lists
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = targetType.GetGenericArguments()[0];
                return ConvertToList(value, elementType, inPrefabMode);
            }
            
            return value;
        }
        
        private static object ConvertJToken(JToken jToken, Type targetType)
        {
            if (jToken.Type == JTokenType.Array)
            {
                var array = jToken as JArray;
                
                // Handle array types
                if (targetType.IsArray)
                {
                    return array; // Will be handled by ConvertToArray
                }
                
                // Handle List types
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    return array; // Will be handled by ConvertToList
                }
                
                // Handle specific Unity types
                if (targetType == typeof(Vector2) && array.Count >= 2)
                {
                    return new Vector2(array[0].Value<float>(), array[1].Value<float>());
                }
                else if (targetType == typeof(Vector3) && array.Count >= 3)
                {
                    return new Vector3(array[0].Value<float>(), array[1].Value<float>(), array[2].Value<float>());
                }
                else if ((targetType == typeof(Vector4) || targetType == typeof(Color)) && array.Count >= 4)
                {
                    var vec = new Vector4(array[0].Value<float>(), array[1].Value<float>(), 
                                         array[2].Value<float>(), array[3].Value<float>());
                    return targetType == typeof(Color) ? (object)(Color)vec : vec;
                }
                else if (targetType == typeof(Quaternion) && array.Count >= 4)
                {
                    return new Quaternion(array[0].Value<float>(), array[1].Value<float>(), 
                                         array[2].Value<float>(), array[3].Value<float>());
                }
            }
            
            return jToken.ToObject(targetType);
        }
        
        private static Vector2 ConvertToVector2(object value)
        {
            if (value is float[] floatArray && floatArray.Length >= 2)
                return new Vector2(floatArray[0], floatArray[1]);
            if (value is double[] doubleArray && doubleArray.Length >= 2)
                return new Vector2((float)doubleArray[0], (float)doubleArray[1]);
            if (value is List<object> list && list.Count >= 2)
                return new Vector2(Convert.ToSingle(list[0]), Convert.ToSingle(list[1]));
            
            return Vector2.zero;
        }
        
        private static Vector3 ConvertToVector3(object value)
        {
            if (value is float[] floatArray && floatArray.Length >= 3)
                return new Vector3(floatArray[0], floatArray[1], floatArray[2]);
            if (value is double[] doubleArray && doubleArray.Length >= 3)
                return new Vector3((float)doubleArray[0], (float)doubleArray[1], (float)doubleArray[2]);
            if (value is List<object> list && list.Count >= 3)
                return new Vector3(Convert.ToSingle(list[0]), Convert.ToSingle(list[1]), Convert.ToSingle(list[2]));
            
            return Vector3.zero;
        }
        
        private static object ConvertToVector4OrColor(object value, Type targetType)
        {
            Vector4 vec = Vector4.zero;
            
            if (value is float[] floatArray && floatArray.Length >= 4)
                vec = new Vector4(floatArray[0], floatArray[1], floatArray[2], floatArray[3]);
            else if (value is double[] doubleArray && doubleArray.Length >= 4)
                vec = new Vector4((float)doubleArray[0], (float)doubleArray[1], (float)doubleArray[2], (float)doubleArray[3]);
            else if (value is List<object> list && list.Count >= 4)
                vec = new Vector4(Convert.ToSingle(list[0]), Convert.ToSingle(list[1]), 
                                 Convert.ToSingle(list[2]), Convert.ToSingle(list[3]));
            
            return targetType == typeof(Color) ? (object)(Color)vec : vec;
        }
        
        private static Quaternion ConvertToQuaternion(object value)
        {
            if (value is float[] floatArray && floatArray.Length >= 4)
                return new Quaternion(floatArray[0], floatArray[1], floatArray[2], floatArray[3]);
            if (value is double[] doubleArray && doubleArray.Length >= 4)
                return new Quaternion((float)doubleArray[0], (float)doubleArray[1], 
                                     (float)doubleArray[2], (float)doubleArray[3]);
            if (value is List<object> list && list.Count >= 4)
                return new Quaternion(Convert.ToSingle(list[0]), Convert.ToSingle(list[1]), 
                                     Convert.ToSingle(list[2]), Convert.ToSingle(list[3]));
            
            // Assume Euler angles if only 3 values
            if (value is float[] eulerArray && eulerArray.Length == 3)
                return Quaternion.Euler(eulerArray[0], eulerArray[1], eulerArray[2]);
            if (value is double[] eulerDoubleArray && eulerDoubleArray.Length == 3)
                return Quaternion.Euler((float)eulerDoubleArray[0], (float)eulerDoubleArray[1], 
                                       (float)eulerDoubleArray[2]);
            
            return Quaternion.identity;
        }
        
        private static bool IsUnityObjectType(Type type)
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(type);
        }
        
        private static UnityEngine.Object ResolveObjectReference(string objectName, Type targetType, bool inPrefabMode)
        {
            // Handle Material assets first
            if (targetType == typeof(Material) || typeof(Material).IsAssignableFrom(targetType))
            {
                // Search for materials by name
                var materials = Resources.FindObjectsOfTypeAll<Material>();
                var foundMaterial = materials.FirstOrDefault(m => m.name == objectName);
                if (foundMaterial != null)
                    return foundMaterial;
                
                // Also search in project assets
                var guids = AssetDatabase.FindAssets($"{objectName} t:Material");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material != null && material.name == objectName)
                        return material;
                }
                return null;
            }
            
            // Handle other asset types
            if (typeof(ScriptableObject).IsAssignableFrom(targetType) || typeof(UnityEngine.Object).IsAssignableFrom(targetType))
            {
                // Search for assets by type and name
                var assets = Resources.FindObjectsOfTypeAll(targetType);
                var foundAsset = assets.FirstOrDefault(a => a.name == objectName);
                if (foundAsset != null)
                    return foundAsset;
                
                // Search in project assets
                var typeName = targetType.Name;
                var guids = AssetDatabase.FindAssets($"{objectName} t:{typeName}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath(path, targetType);
                    if (asset != null && asset.name == objectName)
                        return asset;
                }
            }
            
            // Handle GameObjects and Components
            GameObject foundObject = null;
            
            // Find the GameObject
            if (inPrefabMode)
            {
                var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null)
                {
                    var allTransforms = prefabStage.prefabContentsRoot.GetComponentsInChildren<Transform>(true);
                    var found = allTransforms.FirstOrDefault(t => t.name == objectName);
                    if (found != null)
                        foundObject = found.gameObject;
                }
            }
            else
            {
                foundObject = GameObject.Find(objectName);
                if (foundObject == null)
                {
                    // Try finding inactive objects
                    var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                    foundObject = allObjects.FirstOrDefault(obj => obj.name == objectName && 
                                                                  obj.scene.isLoaded && 
                                                                  !EditorUtility.IsPersistent(obj));
                }
            }
            
            if (foundObject == null)
                return null;
            
            // Convert to the target type
            if (targetType == typeof(GameObject))
                return foundObject;
            else if (targetType == typeof(Transform))
                return foundObject.transform;
            else if (typeof(Component).IsAssignableFrom(targetType))
                return foundObject.GetComponent(targetType);
            
            return null;
        }
        
        private static string NormalizeComponentName(string componentType)
        {
            // Handle common aliases and variations
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"rigidbody", "Rigidbody"},
                {"rb", "Rigidbody"},
                {"collider", "BoxCollider"},
                {"boxcollider", "BoxCollider"},
                {"spherecollider", "SphereCollider"},
                {"capsulecollider", "CapsuleCollider"},
                {"meshcollider", "MeshCollider"},
                {"audiosource", "AudioSource"},
                {"audio", "AudioSource"},
                {"light", "Light"},
                {"camera", "Camera"},
                {"meshrenderer", "MeshRenderer"},
                {"renderer", "MeshRenderer"},
                {"meshfilter", "MeshFilter"},
                {"transform", "Transform"},
                {"animator", "Animator"},
                {"animation", "Animation"},
                {"rigidbody2d", "Rigidbody2D"},
                {"rb2d", "Rigidbody2D"},
                {"collider2d", "BoxCollider2D"},
                {"boxcollider2d", "BoxCollider2D"},
                {"circlecollider2d", "CircleCollider2D"},
                {"particlesystem", "ParticleSystem"},
                {"particles", "ParticleSystem"}
            };

            if (aliases.TryGetValue(componentType, out var normalized))
                return normalized;

            // Default: capitalize first letter
            if (componentType.Length > 0)
                return char.ToUpper(componentType[0]) + componentType.Substring(1);

            return componentType;
        }
        
        private static bool IsValidComponentType(Type type)
        {
            if (type == null)
                return false;

            // Must inherit from Component
            if (!typeof(UnityEngine.Component).IsAssignableFrom(type))
                return false;

            // Must not be abstract
            if (type.IsAbstract)
                return false;

            // Must not be interface
            if (type.IsInterface)
                return false;

            // Must have parameterless constructor or be Unity component
            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                if (type.Namespace == null || !type.Namespace.StartsWith("UnityEngine"))
                    return false;
            }

            return true;
        }
        
        private static List<string> GetAvailableMembers(Type type)
        {
            var members = new List<string>();
            
            // Get writable properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .Select(p => $"{p.Name} ({p.PropertyType.Name})");
            members.AddRange(properties);
            
            // Get serialized fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<SerializeField>() != null || f.IsPublic)
                .Select(f => $"{f.Name} ({f.FieldType.Name})");
            members.AddRange(fields);
            
            return members;
        }
        
        private static int LevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return s2?.Length ?? 0;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            var matrix = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++) matrix[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++) matrix[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(Math.Min(
                        matrix[i - 1, j] + 1,      // deletion
                        matrix[i, j - 1] + 1),     // insertion
                        matrix[i - 1, j - 1] + cost); // substitution
                }
            }

            return matrix[s1.Length, s2.Length];
        }
        
        /// <summary>
        /// Converts a value to an array of the specified element type
        /// </summary>
        private static object ConvertToArray(object value, Type elementType, bool inPrefabMode)
        {
            if (value == null)
                return null;
            
            // Handle JArray
            if (value is JArray jArray)
            {
                var array = Array.CreateInstance(elementType, jArray.Count);
                for (int i = 0; i < jArray.Count; i++)
                {
                    var convertedElement = ConvertValue(jArray[i].ToObject<object>(), elementType, inPrefabMode);
                    array.SetValue(convertedElement, i);
                }
                return array;
            }
            
            // Handle List<object>
            if (value is List<object> objectList)
            {
                var array = Array.CreateInstance(elementType, objectList.Count);
                for (int i = 0; i < objectList.Count; i++)
                {
                    var convertedElement = ConvertValue(objectList[i], elementType, inPrefabMode);
                    array.SetValue(convertedElement, i);
                }
                return array;
            }
            
            // Handle object[]
            if (value is object[] objectArray)
            {
                var array = Array.CreateInstance(elementType, objectArray.Length);
                for (int i = 0; i < objectArray.Length; i++)
                {
                    var convertedElement = ConvertValue(objectArray[i], elementType, inPrefabMode);
                    array.SetValue(convertedElement, i);
                }
                return array;
            }
            
            // Handle already typed arrays
            if (value.GetType().IsArray)
            {
                var sourceArray = (Array)value;
                var array = Array.CreateInstance(elementType, sourceArray.Length);
                for (int i = 0; i < sourceArray.Length; i++)
                {
                    var convertedElement = ConvertValue(sourceArray.GetValue(i), elementType, inPrefabMode);
                    array.SetValue(convertedElement, i);
                }
                return array;
            }
            
            // Handle comma-separated string for simple types
            if (value is string stringValue && (elementType.IsPrimitive || elementType == typeof(string)))
            {
                var parts = stringValue.Split(',').Select(s => s.Trim()).ToArray();
                var array = Array.CreateInstance(elementType, parts.Length);
                for (int i = 0; i < parts.Length; i++)
                {
                    var convertedElement = ConvertValue(parts[i], elementType, inPrefabMode);
                    array.SetValue(convertedElement, i);
                }
                return array;
            }
            
            // If single value, create array with one element
            var singleArray = Array.CreateInstance(elementType, 1);
            singleArray.SetValue(ConvertValue(value, elementType, inPrefabMode), 0);
            return singleArray;
        }
        
        /// <summary>
        /// Converts a value to a List<T> of the specified element type
        /// </summary>
        private static object ConvertToList(object value, Type elementType, bool inPrefabMode)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = Activator.CreateInstance(listType);
            var addMethod = listType.GetMethod("Add");
            
            if (value == null)
                return list;
            
            // Handle JArray
            if (value is JArray jArray)
            {
                foreach (var item in jArray)
                {
                    var convertedElement = ConvertValue(item.ToObject<object>(), elementType, inPrefabMode);
                    addMethod.Invoke(list, new[] { convertedElement });
                }
                return list;
            }
            
            // Handle List<object>
            if (value is List<object> objectList)
            {
                foreach (var item in objectList)
                {
                    var convertedElement = ConvertValue(item, elementType, inPrefabMode);
                    addMethod.Invoke(list, new[] { convertedElement });
                }
                return list;
            }
            
            // Handle arrays
            if (value.GetType().IsArray)
            {
                var array = (Array)value;
                foreach (var item in array)
                {
                    var convertedElement = ConvertValue(item, elementType, inPrefabMode);
                    addMethod.Invoke(list, new[] { convertedElement });
                }
                return list;
            }
            
            // Handle comma-separated string for simple types
            if (value is string stringValue && (elementType.IsPrimitive || elementType == typeof(string)))
            {
                var parts = stringValue.Split(',').Select(s => s.Trim()).ToArray();
                foreach (var part in parts)
                {
                    var convertedElement = ConvertValue(part, elementType, inPrefabMode);
                    addMethod.Invoke(list, new[] { convertedElement });
                }
                return list;
            }
            
            // If single value, create list with one element
            var singleConverted = ConvertValue(value, elementType, inPrefabMode);
            addMethod.Invoke(list, new[] { singleConverted });
            return list;
        }
    }
}