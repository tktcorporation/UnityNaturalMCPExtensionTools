using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;

using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Unified MCP tool for comprehensive object creation and manipulation in Unity
    /// </summary>
    [McpServerToolType, Description("Unified object creation and manipulation tools for Unity")]
    internal sealed class McpUnifiedObjectTool : McpToolBase
    {
        // Type cache moved to ComponentPropertyManager
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
            string parentName = null,
            [Description("Create in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            return await ExecuteOperation(async () =>
            {

                GameObject gameObject = null;

                switch (type?.ToLower())
                {
                    case "primitive":
                        if (string.IsNullOrEmpty(primitiveType))
                            return McpToolUtilities.CreateErrorMessage("CreateObject", "primitiveType is required when creating a primitive");

                        if (!Enum.TryParse<PrimitiveType>(primitiveType, true, out var primType))
                            return McpToolUtilities.CreateErrorMessage("CreateObject", $"Invalid primitive type '{primitiveType}'. Valid types: Cube, Sphere, Cylinder, Capsule, Plane, Quad");

                        gameObject = GameObject.CreatePrimitive(primType);
                        break;

                    case "empty":
                        gameObject = new GameObject();
                        break;

                    case "prefab":
                        if (string.IsNullOrEmpty(prefabName))
                            return McpToolUtilities.CreateErrorMessage("CreateObject", "prefabName is required when creating from prefab");

                        var prefabAsset = FindPrefab(prefabName);
                        if (prefabAsset == null)
                            return McpToolUtilities.CreateErrorMessage("CreateObject", $"Prefab '{prefabName}' not found");

                        gameObject = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                        break;

                    default:
                        return McpToolUtilities.CreateErrorMessage("CreateObject", "type must be 'primitive', 'empty', or 'prefab'");
                }

                if (gameObject == null)
                    return McpToolUtilities.CreateErrorMessage("CreateObject", "Failed to create object");

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
                    var parent = await FindGameObjectSafe(parentName, inPrefabMode);
                    if (parent != null)
                        gameObject.transform.SetParent(parent.transform);
                }

                Undo.RegisterCreatedObjectUndo(gameObject, $"Create {type}");

                var pos = gameObject.transform.position;
                var rot = gameObject.transform.eulerAngles;
                var scl = gameObject.transform.localScale;
                var parentInfo = gameObject.transform.parent != null ? $" (child of {gameObject.transform.parent.name})" : "";

                return McpToolUtilities.CreateSuccessMessage($"Created {type}", gameObject.name, 
                    $"at position ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}), rotation ({rot.x:F2}, {rot.y:F2}, {rot.z:F2}), scale ({scl.x:F2}, {scl.y:F2}, {scl.z:F2}){parentInfo}");
            }, "CreateObject", inPrefabMode);
        }

        [McpServerTool, Description("Manipulate existing objects (transform, parent, duplicate, rename, delete, setactive, or setlayer)")]
        public async ValueTask<string> ManipulateObject(
            [Description("Name of the GameObject to manipulate")]
            string objectName,
            [Description("Operation to perform: 'transform', 'parent', 'duplicate', 'rename', 'delete', 'setactive', or 'setlayer'")]
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
            [Description("For rename: New name for the object (optional)")]
            string newName = null,
            [Description("For parent: Keep world position when parenting (optional, default: true)")]
            bool worldPositionStays = true,
            [Description("For setactive: Set active state (optional)")]
            bool? isActive = null,
            [Description("For setlayer: Layer name to set (optional)")]
            string layerName = null,
            [Description("For setlayer: Layer index to set (0-31, optional)")]
            int? layerIndex = null,
            [Description("Operate in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            return await ExecuteOperation(async () =>
            {
                var gameObject = await FindGameObjectSafe(objectName, inPrefabMode);
                if (gameObject == null)
                    return McpToolUtilities.CreateErrorMessage("ManipulateObject", $"GameObject not found{(inPrefabMode ? " in Prefab mode" : " in scene")}", objectName);

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
                            ? McpToolUtilities.CreateSuccessMessage("Updated transform", objectName, string.Join(", ", changes))
                            : McpToolUtilities.CreateSuccessMessage("No transform changes applied", objectName);

                    case "parent":
                        Transform newParent = null;
                        if (!string.IsNullOrEmpty(parentName))
                        {
                            var parentObj = await FindGameObjectSafe(parentName, inPrefabMode);
                            if (parentObj == null)
                                return McpToolUtilities.CreateErrorMessage("ManipulateObject", $"Parent GameObject not found{(inPrefabMode ? " in Prefab mode" : " in scene")}", parentName);
                            newParent = parentObj.transform;
                        }

                        var oldParent = gameObject.transform.parent;
                        gameObject.transform.SetParent(newParent, worldPositionStays);
                        EditorUtility.SetDirty(gameObject);

                        var parentInfo = newParent != null ? $"'{newParent.name}'" : "root";
                        var oldParentInfo = oldParent != null ? $"'{oldParent.name}'" : "root";
                        return McpToolUtilities.CreateSuccessMessage("Moved object", objectName, $"from {oldParentInfo} to {parentInfo}");

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

                        return McpToolUtilities.CreateSuccessMessage("Duplicated object", objectName, $"as '{duplicated.name}'");

                    case "rename":
                        if (string.IsNullOrEmpty(newName))
                            return McpToolUtilities.CreateErrorMessage("ManipulateObject", "newName is required for rename operation");
                        
                        var oldName = gameObject.name;
                        Undo.RecordObject(gameObject, $"Rename {oldName}");
                        gameObject.name = newName;
                        EditorUtility.SetDirty(gameObject);
                        
                        return McpToolUtilities.CreateSuccessMessage("Renamed GameObject", oldName, $"to '{newName}'");

                    case "delete":
                        Undo.DestroyObjectImmediate(gameObject);
                        return McpToolUtilities.CreateSuccessMessage("Deleted GameObject", objectName);

                    case "setactive":
                        if (!isActive.HasValue)
                            return McpToolUtilities.CreateErrorMessage("ManipulateObject", "isActive is required for setactive operation");
                        
                        var oldActiveState = gameObject.activeSelf;
                        Undo.RecordObject(gameObject, $"Set Active {objectName}");
                        gameObject.SetActive(isActive.Value);
                        EditorUtility.SetDirty(gameObject);
                        
                        return McpToolUtilities.CreateSuccessMessage("Changed active state", objectName, $"from {oldActiveState} to {isActive.Value}");

                    case "setlayer":
                        if (layerName == null && layerIndex == null)
                            return McpToolUtilities.CreateErrorMessage("ManipulateObject", "Either layerName or layerIndex is required for setlayer operation");
                        
                        int targetLayerIndex = -1;
                        
                        if (layerName != null)
                        {
                            targetLayerIndex = LayerMask.NameToLayer(layerName);
                            if (targetLayerIndex == -1)
                                return McpToolUtilities.CreateErrorMessage("ManipulateObject", $"Layer '{layerName}' not found");
                        }
                        else if (layerIndex.HasValue)
                        {
                            if (!IsValidLayerIndex(layerIndex.Value))
                                return McpToolUtilities.CreateErrorMessage("ManipulateObject", $"Layer index {layerIndex.Value} is invalid. Must be between 0 and 31");
                            targetLayerIndex = layerIndex.Value;
                        }
                        
                        var oldLayer = gameObject.layer;
                        var oldLayerName = LayerMask.LayerToName(oldLayer);
                        var newLayerName = LayerMask.LayerToName(targetLayerIndex);
                        
                        Undo.RecordObject(gameObject, $"Set Layer {objectName}");
                        gameObject.layer = targetLayerIndex;
                        EditorUtility.SetDirty(gameObject);
                        
                        return McpToolUtilities.CreateSuccessMessage("Changed layer", objectName, $"from {oldLayer} ({oldLayerName}) to {targetLayerIndex} ({newLayerName})");

                    default:
                        return McpToolUtilities.CreateErrorMessage("ManipulateObject", "Invalid operation. Must be 'transform', 'parent', 'duplicate', 'rename', 'delete', 'setactive', or 'setlayer'");
                }
            }, "ManipulateObject", inPrefabMode);
        }


        [McpServerTool, Description("Add or configure components on GameObjects")]
        public async ValueTask<string> ConfigureComponent(
            [Description("Name of the GameObject")]
            string objectName,
            [Description("JSON string with structured component configuration")]
            string componentConfigurationJson,
            [Description("Configure in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            return await ExecuteOperation(async () =>
            {
                var gameObject = await FindGameObjectSafe(objectName, inPrefabMode);
                if (gameObject == null)
                    return McpToolUtilities.CreateErrorMessage("ConfigureComponent", $"GameObject not found{(inPrefabMode ? " in Prefab mode" : " in scene")}", objectName);

                // Parse and validate component configuration using McpConfigurationManager
                bool isValid = McpConfigurationManager.TryParseConfiguration<ComponentConfiguration>(
                    componentConfigurationJson, out var config, out var validationResult);
                
                if (!isValid)
                {
                    return McpToolUtilities.CreateErrorMessage("ConfigureComponent", 
                        $"Invalid component configuration: {string.Join(", ", validationResult.Errors)}", objectName);
                }
                UnityEngine.Component component = null;

                // Resolve component type using ComponentPropertyManager
                var compType = ComponentPropertyManager.ResolveComponentType(config.componentType);

                if (compType == null)
                {
                    // Try to suggest similar component names
                    var suggestions = ComponentPropertyManager.GetComponentSuggestions(config.componentType);
                    var suggestionText = suggestions.Any() ? $" Did you mean: {string.Join(", ", suggestions)}?" : "";
                    return McpToolUtilities.CreateErrorMessage("ConfigureComponent", 
                        $"Component type '{config.componentType}' not found.{suggestionText}", objectName);
                }

                // Check if component already exists
                component = gameObject.GetComponent(compType);
                bool wasAdded = false;

                // Add component if it doesn't exist
                if (component == null)
                {
                    try
                    {
                        component = gameObject.AddComponent(compType);
                        Undo.RegisterCreatedObjectUndo(component, $"Add {config.componentType}");
                        wasAdded = true;
                    }
                    catch (Exception e)
                    {
                        return McpToolUtilities.CreateErrorMessage("ConfigureComponent", 
                            $"Failed to add component '{config.componentType}': {e.Message}", objectName);
                    }
                }

                // Apply properties if provided
                var changes = new List<string>();
                var errors = new List<string>();

                if (config.properties != null && config.properties.Count > 0)
                {
                    foreach (var prop in config.properties)
                    {
                        // Check if this is a nested property (contains dot notation)
                        ComponentPropertyManager.PropertySetResult result;
                        if (prop.Key.Contains("."))
                        {
                            result = ComponentPropertyManager.SetNestedProperty(component, prop.Key, prop.Value, inPrefabMode);
                        }
                        else
                        {
                            result = ComponentPropertyManager.SetProperty(component, prop.Key, prop.Value, inPrefabMode);
                        }
                        
                        if (result.Success)
                        {
                            changes.Add($"{prop.Key}: {prop.Value}");
                        }
                        else
                        {
                            errors.Add($"{prop.Key}: {result.ErrorMessage}");
                        }
                    }
                }

                EditorUtility.SetDirty(gameObject);

                var actionText = wasAdded ? "Added" : "Configured";
                var result_message = changes.Count > 0
                    ? $"Successfully {actionText.ToLower()} {compType.Name} on '{objectName}': {string.Join(", ", changes)}"
                    : $"{actionText} {compType.Name} to '{objectName}' but no properties were set";

                if (errors.Count > 0)
                {
                    result_message += $"\nErrors: {string.Join("; ", errors)}";
                }

                return result_message;

            }, "ConfigureComponent", inPrefabMode);
        }

        [McpServerTool, Description("Get information about a specific GameObject")]
        public async ValueTask<string> GetObjectInfo(
            [Description("Name of the GameObject to inspect")]
            string objectName,
            [Description("Get info from Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false,
            [Description("Include SerializeField null checks in component details (optional, default: false)")]
            bool checkSerializeFields = false)
        {
            return await ExecuteOperation(async () =>
            {
                var gameObject = await FindGameObjectSafe(objectName, inPrefabMode);
                if (gameObject == null)
                    return McpToolUtilities.CreateErrorMessage("GetObjectInfo", $"GameObject not found{(inPrefabMode ? " in Prefab mode" : " in scene")}", objectName);

                var info = new System.Text.StringBuilder();
                info.AppendLine($"GameObject: {gameObject.name}");
                info.AppendLine($"Active: {gameObject.activeInHierarchy}");
                info.AppendLine($"Layer: {gameObject.layer} ({LayerMask.LayerToName(gameObject.layer)})");
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
                    {
                        info.AppendLine($"  - {component.GetType().Name}");
                        
                        if (checkSerializeFields)
                        {
                            var fieldDetails = GetSerializeFieldDetails(component);
                            if (!string.IsNullOrEmpty(fieldDetails))
                            {
                                info.AppendLine($"    SerializeFields:");
                                info.AppendLine(fieldDetails);
                            }
                        }
                    }
                }

                return info.ToString();
            }, "GetObjectInfo", inPrefabMode);
        }

        [McpServerTool, Description("List all GameObjects in the active scene or Prefab mode")]
        public async ValueTask<string> ListSceneObjects(
            [Description("Optional filter to match object names (case-insensitive)")]
            string nameFilter = null,
            [Description("List objects in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            return await ExecuteOperation(async () =>
            {
                var sceneObjects = new List<string>();

                if (inPrefabMode)
                {
                    var prefabStage = GetCurrentPrefabStage();
                    if (prefabStage == null)
                        return McpToolUtilities.CreateErrorMessage("ListSceneObjects", "Prefab mode is not active. Please open a prefab first.");

                    var root = prefabStage.prefabContentsRoot;

                    // Add root object
                    if (string.IsNullOrEmpty(nameFilter) ||
                        root.name.ToLowerInvariant().Contains(nameFilter.ToLowerInvariant()))
                    {
                        var pos = root.transform.position;
                        var componentCount = root.GetComponents<UnityEngine.Component>().Length;
                        sceneObjects.Add($"'{root.name}' at ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) (root) - {componentCount} components");
                    }

                    // Add all children
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.gameObject == root)
                            continue;

                        if (!string.IsNullOrEmpty(nameFilter) &&
                            !child.name.ToLowerInvariant().Contains(nameFilter.ToLowerInvariant()))
                            continue;

                        var pos = child.position;
                        var parentInfo = child.parent != null ? $" (child of {child.parent.name})" : " (root)";
                        var componentCount = child.GetComponents<UnityEngine.Component>().Length;

                        sceneObjects.Add($"'{child.name}' at ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}){parentInfo} - {componentCount} components");
                    }
                }
                else
                {
                    var allObjects = McpToolUtilities.GetAllGameObjects(inPrefabMode, true);

                    foreach (var obj in allObjects)
                    {
                        if (!string.IsNullOrEmpty(nameFilter) &&
                            !obj.name.ToLowerInvariant().Contains(nameFilter.ToLowerInvariant()))
                            continue;

                        var pos = obj.transform.position;
                        var parentInfo = obj.transform.parent != null ? $" (child of {obj.transform.parent.name})" : " (root)";
                        var componentCount = obj.GetComponents<UnityEngine.Component>().Length;

                        sceneObjects.Add($"'{obj.name}' at ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}){parentInfo} - {componentCount} components");
                    }
                }

                if (sceneObjects.Count == 0)
                    return nameFilter != null
                        ? $"No objects found matching filter '{nameFilter}'{(inPrefabMode ? " in Prefab mode" : " in active scene")}"
                        : $"No objects found{(inPrefabMode ? " in Prefab mode" : " in active scene")}";

                var result = $"Found {sceneObjects.Count} objects{(inPrefabMode ? " in Prefab mode" : " in active scene")}";
                if (!string.IsNullOrEmpty(nameFilter))
                    result += $" matching '{nameFilter}'";
                result += ":\n" + string.Join("\n", sceneObjects);

                return result;
            }, "ListSceneObjects", false); // Don't validate prefab mode since we handle it inside
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

        // Method moved to ComponentPropertyManager

        // Method moved to ComponentPropertyManager

        // Method moved to ComponentPropertyManager

        // Method moved to ComponentPropertyManager

        // Method moved to ComponentPropertyManager

        // Method moved to ComponentPropertyManager

        // SetPropertyValue method moved to ComponentPropertyManager

        private bool IsUnityObjectType(Type type)
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        // PropertySetResult struct moved to ComponentPropertyManager

        // SetNestedPropertyWithDetails method moved to ComponentPropertyManager

        // SetComponentPropertyWithDetails method moved to ComponentPropertyManager

        // SetPropertyValueWithDetails method moved to ComponentPropertyManager


        private UnityEngine.Object ResolveObjectReference(string objectName, Type targetType, bool inPrefabMode)
        {
            if (string.IsNullOrEmpty(objectName) || targetType == null)
                return null;

            Debug.Log($"Resolving object reference '{objectName}' for type {targetType.Name}");

            // Strategy 1: Asset types should be found in Asset Database
            if (IsAssetType(targetType))
            {
                Debug.Log($"Type {targetType.Name} is an asset type, searching in Asset Database");
                return FindAssetByNameAndType(objectName, targetType);
            }

            // Strategy 2: Scene object types should be found in scene
            if (IsSceneObjectType(targetType))
            {
                Debug.Log($"Type {targetType.Name} is a scene object type, searching in scene");
                var gameObject = McpToolUtilities.FindGameObject(objectName, inPrefabMode);
                if (gameObject == null)
                {
                    Debug.LogWarning($"GameObject '{objectName}' not found in {(inPrefabMode ? "Prefab mode" : "scene")}");
                    return null;
                }

                // Handle different Unity object types
                if (targetType == typeof(Transform))
                {
                    return gameObject.transform;
                }
                else if (typeof(UnityEngine.Component).IsAssignableFrom(targetType))
                {
                    var component = gameObject.GetComponent(targetType);
                    if (component == null)
                    {
                        Debug.LogWarning($"Component of type {targetType.Name} not found on GameObject '{objectName}'");
                    }
                    return component;
                }
            }

            // Strategy 3: GameObject - try scene first, then asset (Prefab)
            if (targetType == typeof(GameObject))
            {
                Debug.Log($"Type is GameObject, searching in scene first");
                var gameObject = McpToolUtilities.FindGameObject(objectName, inPrefabMode);
                if (gameObject != null)
                {
                    Debug.Log($"Found GameObject '{objectName}' in {(inPrefabMode ? "Prefab mode" : "scene")}");
                    return gameObject;
                }

                // If not found in scene, try as Prefab asset
                Debug.Log($"GameObject '{objectName}' not found in scene, trying as Prefab asset");
                var prefabAsset = FindAssetByNameAndType(objectName, targetType);
                if (prefabAsset != null)
                {
                    Debug.Log($"Found Prefab asset '{objectName}'");
                    return prefabAsset;
                }

                Debug.LogWarning($"GameObject or Prefab '{objectName}' not found");
                return null;
            }

            Debug.LogWarning($"Unsupported target type {targetType.Name} for object reference '{objectName}'");
            return null;
        }

        private bool IsAssetType(Type type)
        {
            if (type == null)
                return false;

            // Common Unity asset types that should be found in the Asset Database
            var assetTypes = new[]
            {
                typeof(AudioClip),
                typeof(Material),
                typeof(Texture),
                typeof(Texture2D),
                typeof(Mesh),
                typeof(Sprite),
                typeof(Font),
                typeof(Shader),
                typeof(ScriptableObject),
                typeof(AnimationClip),
                typeof(AnimatorController),
                typeof(PhysicsMaterial),
                typeof(PhysicsMaterial2D)
            };

            // Check if the type is exactly one of the asset types or inherits from them
            return assetTypes.Any(assetType => assetType.IsAssignableFrom(type));
        }

        private bool IsSceneObjectType(Type type)
        {
            if (type == null)
                return false;

            // Types that should be found in the scene
            return type == typeof(Transform) || 
                   typeof(UnityEngine.Component).IsAssignableFrom(type);
        }

        private UnityEngine.Object FindAssetByNameAndType(string assetName, Type targetType)
        {
            if (string.IsNullOrEmpty(assetName) || targetType == null)
                return null;

            try
            {
                // Get the type name for AssetDatabase search
                string typeName = GetAssetDatabaseTypeName(targetType);
                if (string.IsNullOrEmpty(typeName))
                    return null;

                // Search for assets with the specified type and name
                var guids = AssetDatabase.FindAssets($"t:{typeName} {assetName}");
                
                foreach (var guid in guids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);
                    
                    if (asset != null && asset.name == assetName)
                    {
                        Debug.Log($"Found asset '{assetName}' of type {targetType.Name} at path: {assetPath}");
                        return asset;
                    }
                }

                // If no exact match found, try without name filter
                guids = AssetDatabase.FindAssets($"t:{typeName}");
                foreach (var guid in guids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);
                    
                    if (asset != null && asset.name.Equals(assetName, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"Found asset '{assetName}' of type {targetType.Name} at path: {assetPath} (case-insensitive match)");
                        return asset;
                    }
                }

                Debug.LogWarning($"Asset '{assetName}' of type {targetType.Name} not found in project");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error searching for asset '{assetName}' of type {targetType.Name}: {e.Message}");
                return null;
            }
        }

        private string GetAssetDatabaseTypeName(Type type)
        {
            // Map Unity types to their AssetDatabase type names
            var typeMap = new Dictionary<Type, string>
            {
                { typeof(AudioClip), "AudioClip" },
                { typeof(Material), "Material" },
                { typeof(Texture), "Texture" },
                { typeof(Texture2D), "Texture2D" },
                { typeof(Mesh), "Mesh" },
                { typeof(Sprite), "Sprite" },
                { typeof(Font), "Font" },
                { typeof(Shader), "Shader" },
                { typeof(AnimationClip), "AnimationClip" },
                { typeof(AnimatorController), "AnimatorController" },
                { typeof(PhysicsMaterial), "PhysicsMaterial" },
                { typeof(PhysicsMaterial2D), "PhysicsMaterial2D" },
                { typeof(GameObject), "Prefab" } // For Prefab assets
            };

            if (typeMap.TryGetValue(type, out var typeName))
                return typeName;

            // Handle ScriptableObject and its derivatives
            if (typeof(ScriptableObject).IsAssignableFrom(type))
                return "ScriptableObject";

            // Return the type name for other Unity objects
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return type.Name;

            return null;
        }

        private string GetSerializeFieldDetails(UnityEngine.Component component)
        {
            try
            {
                var type = component.GetType();
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => f.GetCustomAttribute<SerializeField>() != null || (f.IsPublic && f.GetCustomAttribute<System.NonSerializedAttribute>() == null))
                    .ToArray();

                if (fields.Length == 0)
                    return null;

                var details = new System.Text.StringBuilder();
                
                foreach (var field in fields)
                {
                    var fieldValue = field.GetValue(component);
                    var isNull = fieldValue == null || (fieldValue is UnityEngine.Object unityObj && unityObj == null);
                    var nullStatus = isNull ? "[NULL]" : "[SET]";
                    var valueDisplay = isNull ? "null" : GetFieldValueDisplay(fieldValue);
                    
                    details.AppendLine($"      {field.Name} ({field.FieldType.Name}): {nullStatus} = {valueDisplay}");
                }

                return details.ToString();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Error getting SerializeField details for {component.GetType().Name}: {e.Message}");
                return $"      Error reading SerializeFields: {e.Message}";
            }
        }

        private string GetFieldValueDisplay(object value)
        {
            if (value == null)
                return "null";

            if (value is UnityEngine.Object unityObj)
            {
                if (unityObj == null)
                    return "null (Unity Object)";
                return $"'{unityObj.name}' ({unityObj.GetType().Name})";
            }

            if (value is string str)
                return $"\"{str}\"";

            if (value is Vector3 v3)
                return $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})";

            if (value is Color color)
                return $"({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})";

            if (value is bool || value.GetType().IsPrimitive)
                return value.ToString();

            if (value is System.Collections.ICollection collection)
                return $"Array/List (Count: {collection.Count})";

            return value.ToString();
        }

        // ConvertValueForField method moved to ComponentPropertyManager
        
        // IsNullableType method moved to ComponentPropertyManager

        // Helper method for layer validation (only for GameObject layer setting)
        private bool IsValidLayerIndex(int layerIndex)
        {
            return layerIndex >= 0 && layerIndex <= 31;
        }
    }
}