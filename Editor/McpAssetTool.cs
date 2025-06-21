using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Editor.McpTools
{
    /// <summary>
    /// MCP tool for asset and prefab operations in Unity
    /// </summary>
    [McpServerToolType, Description("Asset and prefab manipulation tools for Unity")]
    internal sealed class McpAssetTool
    {
        [McpServerTool, Description("Create a folder in the project")]
        public async ValueTask<string> CreateFolder(
            [Description("Parent folder path (e.g., 'Assets' or 'Assets/Materials')")]
            string parentPath,
            [Description("Name of the new folder")]
            string folderName)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(folderName))
                {
                    return "Error: Parent path and folder name cannot be empty";
                }

                // Ensure parent path exists
                if (!AssetDatabase.IsValidFolder(parentPath))
                {
                    return $"Error: Parent path '{parentPath}' does not exist";
                }

                var fullPath = Path.Combine(parentPath, folderName).Replace('\\', '/');
                
                // Check if folder already exists
                if (AssetDatabase.IsValidFolder(fullPath))
                {
                    return $"Warning: Folder '{fullPath}' already exists";
                }

                var guid = AssetDatabase.CreateFolder(parentPath, folderName);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (string.IsNullOrEmpty(guid))
                {
                    return $"Error: Failed to create folder '{fullPath}'";
                }

                return $"Successfully created folder '{fullPath}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating folder: {e}");
                return $"Error creating folder: {e.Message}";
            }
        }

        [McpServerTool, Description("Create a prefab from a GameObject in the scene")]
        public async ValueTask<string> CreatePrefabFromGameObject(
            [Description("Name of the GameObject in the scene")]
            string objectName,
            [Description("Path where to save the prefab (e.g., 'Assets/Prefabs/MyPrefab.prefab')")]
            string prefabPath)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found in scene";
                }

                if (string.IsNullOrEmpty(prefabPath))
                {
                    return "Error: Prefab path cannot be empty";
                }

                // Ensure path has .prefab extension
                if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    prefabPath += ".prefab";
                }

                // Ensure path starts with Assets/
                if (!prefabPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                {
                    prefabPath = Path.Combine("Assets", prefabPath).Replace('\\', '/');
                }

                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    var pathParts = directory.Split('/');
                    var currentPath = pathParts[0]; // Start with 'Assets'
                    
                    for (int i = 1; i < pathParts.Length; i++)
                    {
                        var nextPath = Path.Combine(currentPath, pathParts[i]).Replace('\\', '/');
                        if (!AssetDatabase.IsValidFolder(nextPath))
                        {
                            AssetDatabase.CreateFolder(currentPath, pathParts[i]);
                        }
                        currentPath = nextPath;
                    }
                }

                // Create the prefab
                var prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
                
                if (prefab == null)
                {
                    return $"Error: Failed to create prefab at '{prefabPath}'";
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return $"Successfully created prefab '{prefab.name}' at '{prefabPath}' from GameObject '{objectName}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating prefab: {e}");
                return $"Error creating prefab: {e.Message}";
            }
        }

        [McpServerTool, Description("Duplicate a GameObject in the scene")]
        public async ValueTask<string> DuplicateGameObject(
            [Description("Name of the GameObject to duplicate")]
            string objectName,
            [Description("Name for the duplicated object (optional)")]
            string newObjectName = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found in scene";
                }

                var duplicatedObject = GameObject.Instantiate(gameObject);
                
                if (!string.IsNullOrEmpty(newObjectName))
                {
                    duplicatedObject.name = newObjectName;
                }
                else
                {
                    // Remove "(Clone)" suffix if present and add "_Copy"
                    var baseName = gameObject.name.Replace("(Clone)", "").Trim();
                    duplicatedObject.name = baseName + "_Copy";
                }

                // Ensure it's placed in the same parent as the original
                duplicatedObject.transform.SetParent(gameObject.transform.parent);

                // Register undo
                Undo.RegisterCreatedObjectUndo(duplicatedObject, $"Duplicate {objectName}");
                
                EditorUtility.SetDirty(duplicatedObject);

                var pos = duplicatedObject.transform.position;
                var parentInfo = duplicatedObject.transform.parent != null 
                    ? $" (child of {duplicatedObject.transform.parent.name})" 
                    : " (root)";

                return $"Successfully duplicated '{objectName}' as '{duplicatedObject.name}' at position ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}){parentInfo}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error duplicating GameObject: {e}");
                return $"Error duplicating GameObject: {e.Message}";
            }
        }

        [McpServerTool, Description("Instantiate a prefab into the scene")]
        public async ValueTask<string> InstantiatePrefab(
            [Description("Name of the prefab asset")]
            string prefabName,
            [Description("Position in world space (x,y,z) (optional)")]
            float[] position = null,
            [Description("Rotation in euler angles (x,y,z) (optional)")]
            float[] rotation = null,
            [Description("Parent object name (optional)")]
            string parentName = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // Find the prefab asset
                var prefabGuids = AssetDatabase.FindAssets($"t:Prefab {prefabName}");
                GameObject prefabAsset = null;

                foreach (var guid in prefabGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (asset != null && asset.name == prefabName)
                    {
                        prefabAsset = asset;
                        break;
                    }
                }

                if (prefabAsset == null)
                {
                    return $"Error: Prefab '{prefabName}' not found in project";
                }

                // Instantiate the prefab
                var instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
                
                if (instance == null)
                {
                    return $"Error: Failed to instantiate prefab '{prefabName}'";
                }

                // Set position
                if (position != null && position.Length >= 3)
                {
                    instance.transform.position = new Vector3(position[0], position[1], position[2]);
                }

                // Set rotation
                if (rotation != null && rotation.Length >= 3)
                {
                    instance.transform.eulerAngles = new Vector3(rotation[0], rotation[1], rotation[2]);
                }

                // Set parent
                if (!string.IsNullOrEmpty(parentName))
                {
                    var parent = GameObject.Find(parentName);
                    if (parent != null)
                    {
                        instance.transform.SetParent(parent.transform);
                    }
                    else
                    {
                        Debug.LogWarning($"Parent object '{parentName}' not found. Instantiated as root object.");
                    }
                }

                // Register undo
                Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {prefabName}");

                var pos = instance.transform.position;
                var rot = instance.transform.eulerAngles;
                var parentInfo = instance.transform.parent != null 
                    ? $" (child of {instance.transform.parent.name})" 
                    : " (root)";

                return $"Successfully instantiated prefab '{prefabName}' as '{instance.name}' at position ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}), rotation ({rot.x:F2}, {rot.y:F2}, {rot.z:F2}){parentInfo}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error instantiating prefab: {e}");
                return $"Error instantiating prefab: {e.Message}";
            }
        }

        [McpServerTool, Description("List all prefabs in the project")]
        public async ValueTask<string> ListPrefabs(
            [Description("Optional name filter")]
            string nameFilter = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                var prefabs = new System.Collections.Generic.List<string>();

                foreach (var guid in prefabGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    
                    if (prefab != null)
                    {
                        if (string.IsNullOrEmpty(nameFilter) || 
                            prefab.name.ToLowerInvariant().Contains(nameFilter.ToLowerInvariant()))
                        {
                            var componentCount = prefab.GetComponents<UnityEngine.Component>().Length;
                            prefabs.Add($"'{prefab.name}' at '{path}' - {componentCount} components");
                        }
                    }
                }

                if (prefabs.Count == 0)
                {
                    return nameFilter != null 
                        ? $"No prefabs found matching filter '{nameFilter}'" 
                        : "No prefabs found in project";
                }

                var result = $"Found {prefabs.Count} prefabs";
                if (!string.IsNullOrEmpty(nameFilter))
                {
                    result += $" matching '{nameFilter}'";
                }
                result += ":\n" + string.Join("\n", prefabs);

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error listing prefabs: {e}");
                return $"Error listing prefabs: {e.Message}";
            }
        }

        [McpServerTool, Description("Delete an asset from the project")]
        public async ValueTask<string> DeleteAsset(
            [Description("Path to the asset to delete")]
            string assetPath)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                if (string.IsNullOrEmpty(assetPath))
                {
                    return "Error: Asset path cannot be empty";
                }

                if (!File.Exists(assetPath) && !Directory.Exists(assetPath))
                {
                    return $"Error: Asset at path '{assetPath}' does not exist";
                }

                var assetName = Path.GetFileName(assetPath);
                
                if (AssetDatabase.DeleteAsset(assetPath))
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    return $"Successfully deleted asset '{assetName}' at '{assetPath}'";
                }
                else
                {
                    return $"Error: Failed to delete asset at '{assetPath}'";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error deleting asset: {e}");
                return $"Error deleting asset: {e.Message}";
            }
        }
    }
}