using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace Editor.McpTools
{
    /// <summary>
    /// MCP tool for adding and manipulating objects in Unity scenes
    /// </summary>
    [McpServerToolType, Description("Scene object manipulation tools for Unity")]
    internal sealed class McpSceneObjectTool
    {
        [McpServerTool, Description("Add a primitive object (Cube, Sphere, Cylinder, Capsule, Plane, Quad) to the active scene")]
        public async ValueTask<string> AddPrimitiveToScene(
            [Description("Type of primitive to create: Cube, Sphere, Cylinder, Capsule, Plane, Quad")]
            string primitiveType,
            [Description("Position in world space (x,y,z). Default is (0,0,0)")]
            float[] position = null,
            [Description("Scale of the object (x,y,z). Default is (1,1,1)")]
            float[] scale = null,
            [Description("Rotation in euler angles (x,y,z). Default is (0,0,0)")]
            float[] rotation = null,
            [Description("Name for the created object. If not specified, uses default primitive name")]
            string objectName = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // Parse primitive type
                if (!Enum.TryParse<PrimitiveType>(primitiveType, true, out var primType))
                {
                    return $"Error: Invalid primitive type '{primitiveType}'. Valid types: Cube, Sphere, Cylinder, Capsule, Plane, Quad";
                }

                // Create the primitive
                var gameObject = GameObject.CreatePrimitive(primType);
                
                // Set name if provided
                if (!string.IsNullOrEmpty(objectName))
                {
                    gameObject.name = objectName;
                }

                // Set position
                if (position != null && position.Length >= 3)
                {
                    gameObject.transform.position = new Vector3(position[0], position[1], position[2]);
                }

                // Set scale
                if (scale != null && scale.Length >= 3)
                {
                    gameObject.transform.localScale = new Vector3(scale[0], scale[1], scale[2]);
                }

                // Set rotation
                if (rotation != null && rotation.Length >= 3)
                {
                    gameObject.transform.eulerAngles = new Vector3(rotation[0], rotation[1], rotation[2]);
                }

                // Mark scene as dirty
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                // Register undo
                Undo.RegisterCreatedObjectUndo(gameObject, $"Create {primType}");

                var pos = gameObject.transform.position;
                var scl = gameObject.transform.localScale;
                var rot = gameObject.transform.eulerAngles;

                return $"Successfully created {primType} '{gameObject.name}' at position ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}), scale ({scl.x:F2}, {scl.y:F2}, {scl.z:F2}), rotation ({rot.x:F2}, {rot.y:F2}, {rot.z:F2})";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating primitive: {e}");
                return $"Error creating primitive: {e.Message}";
            }
        }

        [McpServerTool, Description("Add an empty GameObject to the active scene")]
        public async ValueTask<string> AddEmptyGameObject(
            [Description("Name for the GameObject")]
            string objectName,
            [Description("Position in world space (x,y,z). Default is (0,0,0)")]
            float[] position = null,
            [Description("Rotation in euler angles (x,y,z). Default is (0,0,0)")]
            float[] rotation = null,
            [Description("Name of parent object to attach to (optional)")]
            string parentName = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = new GameObject(objectName);

                // Set position
                if (position != null && position.Length >= 3)
                {
                    gameObject.transform.position = new Vector3(position[0], position[1], position[2]);
                }

                // Set rotation
                if (rotation != null && rotation.Length >= 3)
                {
                    gameObject.transform.eulerAngles = new Vector3(rotation[0], rotation[1], rotation[2]);
                }

                // Set parent if specified
                if (!string.IsNullOrEmpty(parentName))
                {
                    var parent = GameObject.Find(parentName);
                    if (parent != null)
                    {
                        gameObject.transform.SetParent(parent.transform);
                    }
                    else
                    {
                        Debug.LogWarning($"Parent object '{parentName}' not found. Creating as root object.");
                    }
                }

                // Mark scene as dirty
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                // Register undo
                Undo.RegisterCreatedObjectUndo(gameObject, $"Create GameObject");

                var pos = gameObject.transform.position;
                var rot = gameObject.transform.eulerAngles;
                var parentInfo = gameObject.transform.parent != null ? $" (child of {gameObject.transform.parent.name})" : " (root)";

                return $"Successfully created GameObject '{objectName}' at position ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}), rotation ({rot.x:F2}, {rot.y:F2}, {rot.z:F2}){parentInfo}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating GameObject: {e}");
                return $"Error creating GameObject: {e.Message}";
            }
        }

        [McpServerTool, Description("Add a component to an existing GameObject by name")]
        public async ValueTask<string> AddComponentToObject(
            [Description("Name of the GameObject to add component to")]
            string objectName,
            [Description("Type name of the component to add (e.g., 'Rigidbody', 'BoxCollider', 'Light')")]
            string componentTypeName)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found in scene";
                }

                // Try to find the component type
                var componentType = Type.GetType($"UnityEngine.{componentTypeName}, UnityEngine") ??
                                   Type.GetType($"UnityEngine.{componentTypeName}, UnityEngine.CoreModule") ??
                                   Type.GetType(componentTypeName);

                if (componentType == null)
                {
                    return $"Error: Component type '{componentTypeName}' not found";
                }

                if (!typeof(UnityEngine.Component).IsAssignableFrom(componentType))
                {
                    return $"Error: '{componentTypeName}' is not a valid Component type";
                }

                // Check if component already exists
                if (gameObject.GetComponent(componentType) != null)
                {
                    return $"Warning: GameObject '{objectName}' already has component '{componentTypeName}'";
                }

                // Add the component
                var component = gameObject.AddComponent(componentType);

                // Mark scene as dirty
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                // Register undo
                Undo.RegisterCreatedObjectUndo(component, $"Add {componentTypeName}");

                return $"Successfully added {componentTypeName} component to '{objectName}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error adding component: {e}");
                return $"Error adding component: {e.Message}";
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
                var sceneObjects = new System.Collections.Generic.List<string>();

                foreach (var obj in allObjects)
                {
                    // Skip objects that are not in the active scene
                    if (obj.scene != EditorSceneManager.GetActiveScene())
                        continue;

                    // Skip prefabs and assets
                    if (EditorUtility.IsPersistent(obj))
                        continue;

                    // Apply name filter if provided
                    if (!string.IsNullOrEmpty(nameFilter) && 
                        !obj.name.ToLowerInvariant().Contains(nameFilter.ToLowerInvariant()))
                        continue;

                    var pos = obj.transform.position;
                    var parentInfo = obj.transform.parent != null ? $" (child of {obj.transform.parent.name})" : " (root)";
                    var componentCount = obj.GetComponents<UnityEngine.Component>().Length;
                    
                    sceneObjects.Add($"'{obj.name}' at ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}){parentInfo} - {componentCount} components");
                }

                if (sceneObjects.Count == 0)
                {
                    return nameFilter != null 
                        ? $"No objects found matching filter '{nameFilter}'" 
                        : "No objects found in active scene";
                }

                var result = $"Found {sceneObjects.Count} objects in active scene";
                if (!string.IsNullOrEmpty(nameFilter))
                {
                    result += $" matching '{nameFilter}'";
                }
                result += ":\n" + string.Join("\n", sceneObjects);

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error listing scene objects: {e}");
                return $"Error listing scene objects: {e.Message}";
            }
        }

        [McpServerTool, Description("Get information about a specific GameObject in the scene")]
        public async ValueTask<string> GetObjectInfo(
            [Description("Name of the GameObject to inspect")]
            string objectName)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found in scene";
                }

                var info = new System.Text.StringBuilder();
                info.AppendLine($"GameObject: {gameObject.name}");
                info.AppendLine($"Active: {gameObject.activeInHierarchy}");
                info.AppendLine($"Position: {gameObject.transform.position}");
                info.AppendLine($"Rotation: {gameObject.transform.eulerAngles}");
                info.AppendLine($"Scale: {gameObject.transform.localScale}");
                
                if (gameObject.transform.parent != null)
                {
                    info.AppendLine($"Parent: {gameObject.transform.parent.name}");
                }

                var childCount = gameObject.transform.childCount;
                info.AppendLine($"Children: {childCount}");

                info.AppendLine("Components:");
                var components = gameObject.GetComponents<UnityEngine.Component>();
                foreach (var component in components)
                {
                    if (component != null)
                    {
                        info.AppendLine($"  - {component.GetType().Name}");
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

        [McpServerTool, Description("Delete a GameObject from the scene")]
        public async ValueTask<string> DeleteObject(
            [Description("Name of the GameObject to delete")]
            string objectName)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found in scene";
                }

                // Register undo before destroying
                Undo.DestroyObjectImmediate(gameObject);

                // Mark scene as dirty
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

                return $"Successfully deleted GameObject '{objectName}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error deleting object: {e}");
                return $"Error deleting object: {e.Message}";
            }
        }
    }
}