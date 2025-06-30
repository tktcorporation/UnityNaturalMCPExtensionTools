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
    internal sealed class McpUnifiedObjectTool
    {
        private static readonly ConcurrentDictionary<string, Type> _typeCache = new ConcurrentDictionary<string, Type>();
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
            try
            {
                await UniTask.SwitchToMainThread();

                // Check if we're in Prefab mode when requested
                if (inPrefabMode)
                {
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (prefabStage == null)
                        return "Error: Prefab mode is not active. Please open a prefab first.";
                }

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
                    var parent = FindGameObjectInContext(parentName, inPrefabMode);
                    if (parent != null)
                        gameObject.transform.SetParent(parent.transform);
                }

                if (inPrefabMode)
                {
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (prefabStage != null)
                    {
                        EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                    }
                }
                else
                {
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                }
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

        [McpServerTool, Description("Manipulate existing objects (transform, parent, duplicate, rename, delete, or setactive)")]
        public async ValueTask<string> ManipulateObject(
            [Description("Name of the GameObject to manipulate")]
            string objectName,
            [Description("Operation to perform: 'transform', 'parent', 'duplicate', 'rename', 'delete', or 'setactive'")]
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
            [Description("Operate in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // Check if we're in Prefab mode when requested
                if (inPrefabMode)
                {
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (prefabStage == null)
                        return "Error: Prefab mode is not active. Please open a prefab first.";
                }

                var gameObject = FindGameObjectInContext(objectName, inPrefabMode);
                if (gameObject == null)
                    return $"Error: GameObject '{objectName}' not found{(inPrefabMode ? " in Prefab mode" : " in scene")}";

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
                            var parentObj = FindGameObjectInContext(parentName, inPrefabMode);
                            if (parentObj == null)
                                return $"Error: Parent GameObject '{parentName}' not found{(inPrefabMode ? " in Prefab mode" : " in scene")}";
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

                    case "rename":
                        if (string.IsNullOrEmpty(newName))
                            return "Error: newName is required for rename operation";
                        
                        var oldName = gameObject.name;
                        Undo.RecordObject(gameObject, $"Rename {oldName}");
                        gameObject.name = newName;
                        EditorUtility.SetDirty(gameObject);
                        
                        if (inPrefabMode)
                        {
                            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                            if (prefabStage != null)
                            {
                                EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                            }
                        }
                        else
                        {
                            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                        }
                        
                        return $"Successfully renamed GameObject from '{oldName}' to '{newName}'";

                    case "delete":
                        Undo.DestroyObjectImmediate(gameObject);
                        if (inPrefabMode)
                        {
                            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                            if (prefabStage != null)
                            {
                                EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                            }
                        }
                        else
                        {
                            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                        }
                        return $"Successfully deleted GameObject '{objectName}'";

                    case "setactive":
                        if (!isActive.HasValue)
                            return "Error: isActive is required for setactive operation";
                        
                        var oldActiveState = gameObject.activeSelf;
                        Undo.RecordObject(gameObject, $"Set Active {objectName}");
                        gameObject.SetActive(isActive.Value);
                        EditorUtility.SetDirty(gameObject);
                        
                        if (inPrefabMode)
                        {
                            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                            if (prefabStage != null)
                            {
                                EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                            }
                        }
                        else
                        {
                            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                        }
                        
                        return $"Successfully changed GameObject '{objectName}' active state from {oldActiveState} to {isActive.Value}";

                    default:
                        return "Error: operation must be 'transform', 'parent', 'duplicate', 'rename', 'delete', or 'setactive'";
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
            [Description("JSON string with component properties to set. For Unity object references, use object names as strings (e.g., {\"groundCheck\": \"GroundCheck\"}) (optional)")]
            string properties = null,
            [Description("Configure in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // Check if we're in Prefab mode when requested
                if (inPrefabMode)
                {
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (prefabStage == null)
                        return "Error: Prefab mode is not active. Please open a prefab first.";
                }

                var gameObject = FindGameObjectInContext(objectName, inPrefabMode);
                if (gameObject == null)
                    return $"Error: GameObject '{objectName}' not found{(inPrefabMode ? " in Prefab mode" : " in scene")}";

                UnityEngine.Component component = null;

                // Resolve component type using enhanced method
                var compType = ResolveComponentType(componentType);

                if (compType == null)
                {
                    // Try to suggest similar component names
                    var suggestions = GetComponentSuggestions(componentType);
                    var suggestionText = suggestions.Any() ? $" Did you mean: {string.Join(", ", suggestions)}?" : "";
                    return $"Error: Component type '{componentType}' not found.{suggestionText}";
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
                        Undo.RegisterCreatedObjectUndo(component, $"Add {componentType}");
                        wasAdded = true;
                    }
                    catch (Exception e)
                    {
                        return $"Error: Failed to add component '{componentType}': {e.Message}";
                    }
                }

                // Apply properties if provided
                if (!string.IsNullOrEmpty(properties))
                {
                    try
                    {
                        var propsDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(properties);
                        var changes = new List<string>();
                        var errors = new List<string>();

                        foreach (var prop in propsDict)
                        {
                            var result = SetComponentPropertyWithDetails(component, prop.Key, prop.Value, inPrefabMode);
                            if (result.Success)
                            {
                                changes.Add($"{prop.Key}: {prop.Value}");
                            }
                            else
                            {
                                errors.Add($"{prop.Key}: {result.ErrorMessage}");
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
                    }
                    catch (JsonException)
                    {
                        return $"Error: Invalid JSON format for properties. Expected format: {{\"propertyName\": value}}";
                    }
                }
                else
                {
                    EditorUtility.SetDirty(gameObject);
                    var actionText = wasAdded ? "added" : "found existing";
                    return $"Successfully {actionText} {compType.Name} on '{objectName}'";
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
            string objectName,
            [Description("Get info from Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false,
            [Description("Include SerializeField null checks in component details (optional, default: false)")]
            bool checkSerializeFields = false)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // Check if we're in Prefab mode when requested
                if (inPrefabMode)
                {
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (prefabStage == null)
                        return "Error: Prefab mode is not active. Please open a prefab first.";
                }

                var gameObject = FindGameObjectInContext(objectName, inPrefabMode);
                if (gameObject == null)
                    return $"Error: GameObject '{objectName}' not found{(inPrefabMode ? " in Prefab mode" : " in scene")}";

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
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting object info: {e}");
                return $"Error getting object info: {e.Message}";
            }
        }

        [McpServerTool, Description("List all GameObjects in the active scene or Prefab mode")]
        public async ValueTask<string> ListSceneObjects(
            [Description("Optional filter to match object names (case-insensitive)")]
            string nameFilter = null,
            [Description("List objects in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var sceneObjects = new List<string>();

                if (inPrefabMode)
                {
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (prefabStage == null)
                        return "Error: Prefab mode is not active. Please open a prefab first.";

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
                    var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

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

        private List<string> GetComponentSuggestions(string componentType)
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

        private int LevenshteinDistance(string s1, string s2)
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

        private Type ResolveComponentType(string componentType)
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
                            // Cache the result before returning
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

        private string NormalizeComponentName(string componentType)
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

        private bool IsValidComponentType(Type type)
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

        private bool SetComponentProperty(UnityEngine.Component component, string propertyName, object value, bool inPrefabMode = false)
        {
            try
            {
                var type = component.GetType();

                // First try to find as a property (public properties)
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    Debug.Log($"Found property '{propertyName}' of type {property.PropertyType} on {type.Name}");
                    return SetPropertyValue(component, property.PropertyType, propertyName, value, inPrefabMode,
                        (val) => property.SetValue(component, val));
                }

                // Then try to find as a field (including private SerializeFields)
                var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    Debug.Log($"Found field '{propertyName}' of type {field.FieldType} on {type.Name} (Access: {field.Attributes})");
                    return SetPropertyValue(component, field.FieldType, propertyName, value, inPrefabMode,
                        (val) => field.SetValue(component, val));
                }

                // If we get here, the property/field was not found
                var availableFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => f.GetCustomAttribute<SerializeField>() != null || f.IsPublic)
                    .Select(f => $"{f.Name} ({f.FieldType.Name})")
                    .ToArray();

                var availableProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite)
                    .Select(p => $"{p.Name} ({p.PropertyType.Name})")
                    .ToArray();

                Debug.LogWarning($"Property/field '{propertyName}' not found on {type.Name}. " +
                    $"Available serialized fields: [{string.Join(", ", availableFields)}]. " +
                    $"Available properties: [{string.Join(", ", availableProperties)}]");
            }
            catch (Exception e)
            {
                Debug.LogError($"Exception while setting property {propertyName} on {component.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }

            return false;
        }

        private bool SetPropertyValue(UnityEngine.Component component, Type targetType, string propertyName, object value, bool inPrefabMode, Action<object> setValue)
        {
            Debug.Log($"Setting property '{propertyName}' of type {targetType.Name} with value '{value}' (type: {value?.GetType().Name})");

            // Handle Unity object references (Transform, GameObject, Component types)
            if (value is string stringValue && IsUnityObjectType(targetType))
            {
                Debug.Log($"Resolving Unity object reference '{stringValue}' for type {targetType.Name}");
                var objectReference = ResolveObjectReference(stringValue, targetType, inPrefabMode);
                if (objectReference != null)
                {
                    Debug.Log($"Successfully resolved object '{stringValue}' to {objectReference.GetType().Name}");
                    setValue(objectReference);
                    return true;
                }
                else
                {
                    Debug.LogWarning($"Could not find object '{stringValue}' for property '{propertyName}' of type {targetType.Name}");
                    return false;
                }
            }

            // Handle arrays (Vector3, Color)
            if (value is Newtonsoft.Json.Linq.JArray jArray)
            {
                if (targetType == typeof(Vector3))
                {
                    var values = jArray.ToObject<float[]>();
                    if (values.Length >= 3)
                    {
                        setValue(new Vector3(values[0], values[1], values[2]));
                        return true;
                    }
                }
                else if (targetType == typeof(Color))
                {
                    var values = jArray.ToObject<float[]>();
                    if (values.Length >= 3)
                    {
                        setValue(new Color(values[0], values[1], values[2], values.Length > 3 ? values[3] : 1f));
                        return true;
                    }
                }
            }

            // Handle LayerMask
            if (targetType == typeof(LayerMask))
            {
                if (value is string layerName)
                {
                    int layerIndex = LayerMask.NameToLayer(layerName);
                    if (layerIndex != -1)
                    {
                        setValue((LayerMask)(1 << layerIndex));
                        return true;
                    }
                }
                else if (value is long || value is int)
                {
                    setValue((LayerMask)Convert.ToInt32(value));
                    return true;
                }
            }

            // Handle primitive types
            try
            {
                var convertedValue = Convert.ChangeType(value, targetType);
                setValue(convertedValue);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to convert value for property {propertyName}: {e.Message}");
                return false;
            }
        }

        private bool IsUnityObjectType(Type type)
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        private struct PropertySetResult
        {
            public bool Success;
            public string ErrorMessage;
            public string Details;

            public PropertySetResult(bool success, string errorMessage = null, string details = null)
            {
                Success = success;
                ErrorMessage = errorMessage;
                Details = details;
            }
        }

        private PropertySetResult SetComponentPropertyWithDetails(UnityEngine.Component component, string propertyName, object value, bool inPrefabMode = false)
        {
            try
            {
                var type = component.GetType();
                var details = new List<string>();

                // First try to find as a property (public properties)
                var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    details.Add($"Found property '{propertyName}' of type {property.PropertyType} on {type.Name}");
                    var result = SetPropertyValueWithDetails(component, property.PropertyType, propertyName, value, inPrefabMode,
                        (val) => property.SetValue(component, val));
                    if (result.Success)
                    {
                        return new PropertySetResult(true, null, string.Join("; ", details.Concat(new[] { result.Details })));
                    }
                    else
                    {
                        return new PropertySetResult(false, result.ErrorMessage, string.Join("; ", details.Concat(new[] { result.Details })));
                    }
                }

                // Then try to find as a field (including private SerializeFields)
                var field = type.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    details.Add($"Found field '{propertyName}' of type {field.FieldType} on {type.Name} (Access: {field.Attributes})");
                    var result = SetPropertyValueWithDetails(component, field.FieldType, propertyName, value, inPrefabMode,
                        (val) => field.SetValue(component, val));
                    if (result.Success)
                    {
                        return new PropertySetResult(true, null, string.Join("; ", details.Concat(new[] { result.Details })));
                    }
                    else
                    {
                        return new PropertySetResult(false, result.ErrorMessage, string.Join("; ", details.Concat(new[] { result.Details })));
                    }
                }

                // If we get here, the property/field was not found
                var availableFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => f.GetCustomAttribute<SerializeField>() != null || f.IsPublic)
                    .Select(f => $"{f.Name} ({f.FieldType.Name})")
                    .ToArray();

                var availableProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanWrite)
                    .Select(p => $"{p.Name} ({p.PropertyType.Name})")
                    .ToArray();

                var errorMsg = $"Property/field '{propertyName}' not found on {type.Name}. " +
                    $"Available serialized fields: [{string.Join(", ", availableFields)}]. " +
                    $"Available properties: [{string.Join(", ", availableProperties)}]";

                return new PropertySetResult(false, errorMsg);
            }
            catch (Exception e)
            {
                var errorMsg = $"Exception while setting property {propertyName} on {component.GetType().Name}: {e.Message}";
                return new PropertySetResult(false, errorMsg, e.StackTrace);
            }
        }

        private PropertySetResult SetPropertyValueWithDetails(UnityEngine.Component component, Type targetType, string propertyName, object value, bool inPrefabMode, Action<object> setValue)
        {
            var details = new List<string>();
            details.Add($"Setting property '{propertyName}' of type {targetType.Name} with value '{value}' (type: {value?.GetType().Name})");

            // Handle Unity object references (Transform, GameObject, Component types)
            if (value is string stringValue && IsUnityObjectType(targetType))
            {
                details.Add($"Resolving Unity object reference '{stringValue}' for type {targetType.Name}");
                var objectReference = ResolveObjectReference(stringValue, targetType, inPrefabMode);
                if (objectReference != null)
                {
                    details.Add($"Successfully resolved object '{stringValue}' to {objectReference.GetType().Name}");
                    setValue(objectReference);
                    return new PropertySetResult(true, null, string.Join("; ", details));
                }
                else
                {
                    var errorMsg = $"Could not find object '{stringValue}' for property '{propertyName}' of type {targetType.Name}";
                    return new PropertySetResult(false, errorMsg, string.Join("; ", details));
                }
            }

            // Handle arrays (Vector3, Color)
            if (value is Newtonsoft.Json.Linq.JArray jArray)
            {
                if (targetType == typeof(Vector3))
                {
                    var values = jArray.ToObject<float[]>();
                    if (values.Length >= 3)
                    {
                        setValue(new Vector3(values[0], values[1], values[2]));
                        details.Add($"Set Vector3 value ({values[0]}, {values[1]}, {values[2]})");
                        return new PropertySetResult(true, null, string.Join("; ", details));
                    }
                }
                else if (targetType == typeof(Color))
                {
                    var values = jArray.ToObject<float[]>();
                    if (values.Length >= 3)
                    {
                        setValue(new Color(values[0], values[1], values[2], values.Length > 3 ? values[3] : 1f));
                        details.Add($"Set Color value ({values[0]}, {values[1]}, {values[2]}, {(values.Length > 3 ? values[3] : 1f)})");
                        return new PropertySetResult(true, null, string.Join("; ", details));
                    }
                }
            }

            // Handle LayerMask
            if (targetType == typeof(LayerMask))
            {
                if (value is string layerName)
                {
                    int layerIndex = LayerMask.NameToLayer(layerName);
                    if (layerIndex != -1)
                    {
                        setValue((LayerMask)(1 << layerIndex));
                        details.Add($"Set LayerMask for layer '{layerName}' (index {layerIndex})");
                        return new PropertySetResult(true, null, string.Join("; ", details));
                    }
                    else
                    {
                        var errorMsg = $"Layer '{layerName}' not found";
                        return new PropertySetResult(false, errorMsg, string.Join("; ", details));
                    }
                }
                else if (value is long || value is int)
                {
                    setValue((LayerMask)Convert.ToInt32(value));
                    details.Add($"Set LayerMask to value {value}");
                    return new PropertySetResult(true, null, string.Join("; ", details));
                }
            }

            // Handle primitive types
            try
            {
                var convertedValue = Convert.ChangeType(value, targetType);
                setValue(convertedValue);
                details.Add($"Set {targetType.Name} value to {convertedValue}");
                return new PropertySetResult(true, null, string.Join("; ", details));
            }
            catch (Exception e)
            {
                var errorMsg = $"Failed to convert value '{value}' to type {targetType.Name}: {e.Message}";
                return new PropertySetResult(false, errorMsg, string.Join("; ", details));
            }
        }

        private GameObject FindGameObjectInContext(string objectName, bool inPrefabMode)
        {
            if (string.IsNullOrEmpty(objectName))
                return null;

            if (inPrefabMode)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage == null)
                {
                    Debug.LogError("Prefab mode is not active");
                    return null;
                }

                var root = prefabStage.prefabContentsRoot;
                if (root.name == objectName)
                    return root;

                // Search in children
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == objectName)
                        return child.gameObject;
                }

                return null;
            }
            else
            {
                return GameObject.Find(objectName);
            }
        }

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
                var gameObject = FindGameObjectInContext(objectName, inPrefabMode);
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
                var gameObject = FindGameObjectInContext(objectName, inPrefabMode);
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
    }
}