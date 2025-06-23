using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Editor.McpTools
{
    /// <summary>
    /// Unified MCP tool for comprehensive object creation and manipulation in Unity
    /// </summary>
    [McpServerToolType, Description("Unified object creation and manipulation tools for Unity")]
    internal sealed class McpUnifiedObjectTool
    {
        [McpServerTool, Description("Create objects in the scene (Primitive, Empty GameObject, or Prefab instance)")]
        public async ValueTask<string> CreateObject(
            [Description("Type of object to create: 'primitive', 'empty', or 'prefab'")]
            string type,
            [Description("Name for the created object")]
            string objectName,
            [Description("For primitive: Cube, Sphere, Cylinder, Capsule, Plane, Quad (optional)")]
            string primitiveType = null,
            [Description("For prefab: Name of the prefab asset (optional)")]
            string prefabName = null,
            [Description("Position in world space [x,y,z] (optional)")]
            float[] position = null,
            [Description("Rotation in euler angles [x,y,z] (optional)")]
            float[] rotation = null,
            [Description("Scale [x,y,z] (optional, not for prefabs)")]
            float[] scale = null,
            [Description("Parent object name (optional)")]
            string parentName = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                GameObject gameObject = null;

                switch (type?.ToLower())
                {
                    case "primitive":
                        if (string.IsNullOrEmpty(primitiveType))
                            return "Error: primitiveType is required when creating a primitive";
                        
                        if (!Enum.TryParse<PrimitiveType>(primitiveType, true, out var primType))
                            return $"Error: Invalid primitive type '{primitiveType}'. Valid types: Cube, Sphere, Cylinder, Capsule, Plane, Quad";
                        
                        gameObject = GameObject.CreatePrimitive(primType);
                        break;

                    case "empty":
                        gameObject = new GameObject();
                        break;

                    case "prefab":
                        if (string.IsNullOrEmpty(prefabName))
                            return "Error: prefabName is required when creating from prefab";
                        
                        var prefabAsset = FindPrefab(prefabName);
                        if (prefabAsset == null)
                            return $"Error: Prefab '{prefabName}' not found";
                        
                        gameObject = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                        break;

                    default:
                        return "Error: type must be 'primitive', 'empty', or 'prefab'";
                }

                if (gameObject == null)
                    return "Error: Failed to create object";

                // Set name
                if (!string.IsNullOrEmpty(objectName))
                    gameObject.name = objectName;

                // Set transform properties
                if (position != null && position.Length >= 3)
                    gameObject.transform.position = new Vector3(position[0], position[1], position[2]);

                if (rotation != null && rotation.Length >= 3)
                    gameObject.transform.eulerAngles = new Vector3(rotation[0], rotation[1], rotation[2]);

                if (scale != null && scale.Length >= 3 && type != "prefab")
                    gameObject.transform.localScale = new Vector3(scale[0], scale[1], scale[2]);

                // Set parent
                if (!string.IsNullOrEmpty(parentName))
                {
                    var parent = GameObject.Find(parentName);
                    if (parent != null)
                        gameObject.transform.SetParent(parent.transform);
                }

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Undo.RegisterCreatedObjectUndo(gameObject, $"Create {type}");

                var pos = gameObject.transform.position;
                var rot = gameObject.transform.eulerAngles;
                var scl = gameObject.transform.localScale;
                var parentInfo = gameObject.transform.parent != null ? $" (child of {gameObject.transform.parent.name})" : "";

                return $"Successfully created {type} '{gameObject.name}' at position ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}), " +
                       $"rotation ({rot.x:F2}, {rot.y:F2}, {rot.z:F2}), scale ({scl.x:F2}, {scl.y:F2}, {scl.z:F2}){parentInfo}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating object: {e}");
                return $"Error creating object: {e.Message}";
            }
        }

        [McpServerTool, Description("Manipulate existing objects (transform, parent, duplicate, or delete)")]
        public async ValueTask<string> ManipulateObject(
            [Description("Name of the GameObject to manipulate")]
            string objectName,
            [Description("Operation to perform: 'transform', 'parent', 'duplicate', or 'delete'")]
            string operation,
            [Description("For transform/duplicate: Position [x,y,z] (optional)")]
            float[] position = null,
            [Description("For transform/duplicate: Rotation [x,y,z] (optional)")]
            float[] rotation = null,
            [Description("For transform: Scale [x,y,z] (optional)")]
            float[] scale = null,
            [Description("For parent: Parent object name, null to unparent (optional)")]
            string parentName = null,
            [Description("For duplicate: Name for the duplicated object (optional)")]
            string newObjectName = null,
            [Description("For parent: Keep world position when parenting (optional, default: true)")]
            bool worldPositionStays = true)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                    return $"Error: GameObject '{objectName}' not found";

                switch (operation?.ToLower())
                {
                    case "transform":
                        var changes = new List<string>();
                        
                        if (position != null && position.Length >= 3)
                        {
                            gameObject.transform.position = new Vector3(position[0], position[1], position[2]);
                            changes.Add($"position: ({position[0]:F2}, {position[1]:F2}, {position[2]:F2})");
                        }
                        
                        if (rotation != null && rotation.Length >= 3)
                        {
                            gameObject.transform.eulerAngles = new Vector3(rotation[0], rotation[1], rotation[2]);
                            changes.Add($"rotation: ({rotation[0]:F2}, {rotation[1]:F2}, {rotation[2]:F2})");
                        }
                        
                        if (scale != null && scale.Length >= 3)
                        {
                            gameObject.transform.localScale = new Vector3(scale[0], scale[1], scale[2]);
                            changes.Add($"scale: ({scale[0]:F2}, {scale[1]:F2}, {scale[2]:F2})");
                        }
                        
                        EditorUtility.SetDirty(gameObject);
                        return changes.Count > 0 
                            ? $"Successfully updated transform on '{objectName}': {string.Join(", ", changes)}"
                            : $"No transform changes applied to '{objectName}'";

                    case "parent":
                        Transform newParent = null;
                        if (!string.IsNullOrEmpty(parentName))
                        {
                            var parentObj = GameObject.Find(parentName);
                            if (parentObj == null)
                                return $"Error: Parent GameObject '{parentName}' not found";
                            newParent = parentObj.transform;
                        }
                        
                        var oldParent = gameObject.transform.parent;
                        gameObject.transform.SetParent(newParent, worldPositionStays);
                        EditorUtility.SetDirty(gameObject);
                        
                        var parentInfo = newParent != null ? $"'{newParent.name}'" : "root";
                        var oldParentInfo = oldParent != null ? $"'{oldParent.name}'" : "root";
                        return $"Successfully moved '{objectName}' from {oldParentInfo} to {parentInfo}";

                    case "duplicate":
                        var duplicated = GameObject.Instantiate(gameObject);
                        duplicated.name = !string.IsNullOrEmpty(newObjectName) 
                            ? newObjectName 
                            : gameObject.name + "_Copy";
                        
                        duplicated.transform.SetParent(gameObject.transform.parent);
                        
                        if (position != null && position.Length >= 3)
                            duplicated.transform.position = new Vector3(position[0], position[1], position[2]);
                        
                        if (rotation != null && rotation.Length >= 3)
                            duplicated.transform.eulerAngles = new Vector3(rotation[0], rotation[1], rotation[2]);
                        
                        Undo.RegisterCreatedObjectUndo(duplicated, $"Duplicate {objectName}");
                        EditorUtility.SetDirty(duplicated);
                        
                        return $"Successfully duplicated '{objectName}' as '{duplicated.name}'";

                    case "delete":
                        Undo.DestroyObjectImmediate(gameObject);
                        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                        return $"Successfully deleted GameObject '{objectName}'";

                    default:
                        return "Error: operation must be 'transform', 'parent', 'duplicate', or 'delete'";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error manipulating object: {e}");
                return $"Error manipulating object: {e.Message}";
            }
        }

        [McpServerTool, Description("Add or configure components on GameObjects")]
        public async ValueTask<string> ConfigureComponent(
            [Description("Name of the GameObject")]
            string objectName,
            [Description("Component type (e.g., 'Rigidbody', 'BoxCollider', 'AudioSource', 'Renderer')")]
            string componentType,
            [Description("JSON string with component properties to set (optional)")]
            string properties = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                    return $"Error: GameObject '{objectName}' not found";

                UnityEngine.Component component = null;
                
                // Try to find existing component
                var compType = Type.GetType($"UnityEngine.{componentType}, UnityEngine") ??
                              Type.GetType($"UnityEngine.{componentType}, UnityEngine.CoreModule") ??
                              Type.GetType(componentType);
                
                if (compType == null)
                    return $"Error: Component type '{componentType}' not found";
                
                component = gameObject.GetComponent(compType);
                
                // Add component if it doesn't exist
                if (component == null)
                {
                    component = gameObject.AddComponent(compType);
                    Undo.RegisterCreatedObjectUndo(component, $"Add {componentType}");
                }

                // Apply properties if provided
                if (!string.IsNullOrEmpty(properties))
                {
                    try
                    {
                        var propsDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(properties);
                        var changes = new List<string>();

                        foreach (var prop in propsDict)
                        {
                            if (SetComponentProperty(component, prop.Key, prop.Value))
                                changes.Add($"{prop.Key}: {prop.Value}");
                        }

                        EditorUtility.SetDirty(gameObject);
                        
                        return changes.Count > 0
                            ? $"Successfully configured {componentType} on '{objectName}': {string.Join(", ", changes)}"
                            : $"Added {componentType} to '{objectName}' but no properties were set";
                    }
                    catch (JsonException)
                    {
                        return $"Error: Invalid JSON format for properties";
                    }
                }
                else
                {
                    EditorUtility.SetDirty(gameObject);
                    return $"Successfully added {componentType} to '{objectName}'";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error configuring component: {e}");
                return $"Error configuring component: {e.Message}";
            }
        }

        [McpServerTool, Description("Get information about a specific GameObject")]
        public async ValueTask<string> GetObjectInfo(
            [Description("Name of the GameObject to inspect")]
            string objectName)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                    return $"Error: GameObject '{objectName}' not found";

                var info = new System.Text.StringBuilder();
                info.AppendLine($"GameObject: {gameObject.name}");
                info.AppendLine($"Active: {gameObject.activeInHierarchy}");
                info.AppendLine($"Position: {gameObject.transform.position}");
                info.AppendLine($"Rotation: {gameObject.transform.eulerAngles}");
                info.AppendLine($"Scale: {gameObject.transform.localScale}");
                
                if (gameObject.transform.parent != null)
                    info.AppendLine($"Parent: {gameObject.transform.parent.name}");

                info.AppendLine($"Children: {gameObject.transform.childCount}");
                info.AppendLine("Components:");
                
                foreach (var component in gameObject.GetComponents<UnityEngine.Component>())
                {
                    if (component != null)
                        info.AppendLine($"  - {component.GetType().Name}");
                }

                return info.ToString();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting object info: {e}");
                return $"Error getting object info: {e.Message}";
            }
        }

        [McpServerTool, Description("List all GameObjects in the active scene")]
        public async ValueTask<string> ListSceneObjects(
            [Description("Optional filter to match object names (case-insensitive)")]
            string nameFilter = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                var sceneObjects = new List<string>();

                foreach (var obj in allObjects)
                {
                    if (obj.scene != EditorSceneManager.GetActiveScene())
                        continue;

                    if (EditorUtility.IsPersistent(obj))
                        continue;

                    if (!string.IsNullOrEmpty(nameFilter) && 
                        !obj.name.ToLowerInvariant().Contains(nameFilter.ToLowerInvariant()))
                        continue;

                    var pos = obj.transform.position;
                    var parentInfo = obj.transform.parent != null ? $" (child of {obj.transform.parent.name})" : " (root)";
                    var componentCount = obj.GetComponents<UnityEngine.Component>().Length;
                    
                    sceneObjects.Add($"'{obj.name}' at ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}){parentInfo} - {componentCount} components");
                }

                if (sceneObjects.Count == 0)
                    return nameFilter != null 
                        ? $"No objects found matching filter '{nameFilter}'" 
                        : "No objects found in active scene";

                var result = $"Found {sceneObjects.Count} objects in active scene";
                if (!string.IsNullOrEmpty(nameFilter))
                    result += $" matching '{nameFilter}'";
                result += ":\n" + string.Join("\n", sceneObjects);

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error listing scene objects: {e}");
                return $"Error listing scene objects: {e.Message}";
            }
        }

        private GameObject FindPrefab(string prefabName)
        {
            var prefabGuids = AssetDatabase.FindAssets($"t:Prefab {prefabName}");
            
            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                
                if (asset != null && asset.name == prefabName)
                    return asset;
            }
            
            return null;
        }

        private bool SetComponentProperty(UnityEngine.Component component, string propertyName, object value)
        {
            try
            {
                var type = component.GetType();
                var property = type.GetProperty(propertyName);
                
                if (property != null && property.CanWrite)
                {
                    if (value is Newtonsoft.Json.Linq.JArray jArray)
                    {
                        if (property.PropertyType == typeof(Vector3))
                        {
                            var values = jArray.ToObject<float[]>();
                            if (values.Length >= 3)
                            {
                                property.SetValue(component, new Vector3(values[0], values[1], values[2]));
                                return true;
                            }
                        }
                        else if (property.PropertyType == typeof(Color))
                        {
                            var values = jArray.ToObject<float[]>();
                            if (values.Length >= 3)
                            {
                                property.SetValue(component, new Color(values[0], values[1], values[2], values.Length > 3 ? values[3] : 1f));
                                return true;
                            }
                        }
                    }
                    else
                    {
                        var convertedValue = Convert.ChangeType(value, property.PropertyType);
                        property.SetValue(component, convertedValue);
                        return true;
                    }
                }
                
                var field = type.GetField(propertyName);
                if (field != null)
                {
                    if (value is Newtonsoft.Json.Linq.JArray jArray)
                    {
                        if (field.FieldType == typeof(Vector3))
                        {
                            var values = jArray.ToObject<float[]>();
                            if (values.Length >= 3)
                            {
                                field.SetValue(component, new Vector3(values[0], values[1], values[2]));
                                return true;
                            }
                        }
                        else if (field.FieldType == typeof(Color))
                        {
                            var values = jArray.ToObject<float[]>();
                            if (values.Length >= 3)
                            {
                                field.SetValue(component, new Color(values[0], values[1], values[2], values.Length > 3 ? values[3] : 1f));
                                return true;
                            }
                        }
                    }
                    else
                    {
                        var convertedValue = Convert.ChangeType(value, field.FieldType);
                        field.SetValue(component, convertedValue);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to set property {propertyName}: {e.Message}");
            }
            
            return false;
        }
    }
}