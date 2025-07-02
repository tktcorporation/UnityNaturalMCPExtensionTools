using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Unified MCP tool for material, asset, and folder management in Unity
    /// </summary>
    [McpServerToolType, Description("Unified material and asset management tools for Unity")]
    internal sealed class McpUnifiedAssetTool
    {
        [McpServerTool, Description("Create or update materials with multiple properties in one call")]
        public async ValueTask<string> ManageMaterial(
            [Description("Material name")]
            string materialName,
            [Description("Operation: 'create' or 'update'")]
            string operation,
            [Description("Shader name (for create, optional for update)")]
            string shaderName = null,
            [Description("Base color [R,G,B,A] (optional)")]
            float[] baseColor = null,
            [Description("Metallic value 0-1 (optional)")]
            float? metallic = null,
            [Description("Smoothness value 0-1 (optional)")]
            float? smoothness = null,
            [Description("Emission color [R,G,B] with optional intensity (optional)")]
            float[] emission = null,
            [Description("Emission intensity multiplier (optional, default: 1.0)")]
            float emissionIntensity = 1.0f,
            [Description("Additional properties as key-value pairs (optional)")]
            Dictionary<string, float> additionalFloatProperties = null,
            [Description("Additional color properties as key-value pairs (optional)")]
            Dictionary<string, float[]> additionalColorProperties = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                Material material = null;
                var changes = new List<string>();

                switch (operation?.ToLower())
                {
                    case "create":
                        if (string.IsNullOrEmpty(shaderName))
                            shaderName = "Universal Render Pipeline/Lit";

                        var shader = Shader.Find(shaderName);
                        if (shader == null)
                            return $"Error: Shader '{shaderName}' not found";

                        material = new Material(shader);
                        material.name = materialName;

                        var path = $"Assets/Materials/{materialName}.mat";

                        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                            AssetDatabase.CreateFolder("Assets", "Materials");

                        AssetDatabase.CreateAsset(material, path);
                        changes.Add($"created at '{path}' with shader '{shaderName}'");
                        break;

                    case "update":
                        material = FindMaterial(materialName);
                        if (material == null)
                            return $"Error: Material '{materialName}' not found";

                        if (!string.IsNullOrEmpty(shaderName))
                        {
                            var newShader = Shader.Find(shaderName);
                            if (newShader != null)
                            {
                                material.shader = newShader;
                                changes.Add($"shader: {shaderName}");
                            }
                        }
                        break;

                    default:
                        return "Error: operation must be 'create' or 'update'";
                }

                // Apply properties
                if (baseColor != null && baseColor.Length >= 3)
                {
                    var color = new Color(
                        baseColor[0],
                        baseColor[1],
                        baseColor[2],
                        baseColor.Length > 3 ? baseColor[3] : 1.0f
                    );

                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", color);
                    else if (material.HasProperty("_Color"))
                        material.SetColor("_Color", color);

                    changes.Add($"baseColor: ({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})");
                }

                if (metallic.HasValue && material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", metallic.Value);
                    changes.Add($"metallic: {metallic.Value:F2}");
                }

                if (smoothness.HasValue && material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", smoothness.Value);
                    changes.Add($"smoothness: {smoothness.Value:F2}");
                }

                if (emission != null && emission.Length >= 3 && material.HasProperty("_EmissionColor"))
                {
                    var emissionColor = new Color(
                        emission[0] * emissionIntensity,
                        emission[1] * emissionIntensity,
                        emission[2] * emissionIntensity,
                        1.0f
                    );
                    material.SetColor("_EmissionColor", emissionColor);
                    material.EnableKeyword("_EMISSION");
                    changes.Add($"emission: ({emissionColor.r:F2}, {emissionColor.g:F2}, {emissionColor.b:F2}) intensity: {emissionIntensity:F2}");
                }

                // Apply additional properties
                if (additionalFloatProperties != null)
                {
                    foreach (var prop in additionalFloatProperties)
                    {
                        if (material.HasProperty(prop.Key))
                        {
                            material.SetFloat(prop.Key, prop.Value);
                            changes.Add($"{prop.Key}: {prop.Value:F2}");
                        }
                    }
                }

                if (additionalColorProperties != null)
                {
                    foreach (var prop in additionalColorProperties)
                    {
                        if (material.HasProperty(prop.Key) && prop.Value.Length >= 3)
                        {
                            var color = new Color(
                                prop.Value[0],
                                prop.Value[1],
                                prop.Value[2],
                                prop.Value.Length > 3 ? prop.Value[3] : 1.0f
                            );
                            material.SetColor(prop.Key, color);
                            changes.Add($"{prop.Key}: ({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})");
                        }
                    }
                }

                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return changes.Count > 0
                    ? $"Successfully {operation}d material '{materialName}': {string.Join(", ", changes)}"
                    : $"Material '{materialName}' {operation}d with no property changes";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error managing material: {e}");
                return $"Error managing material: {e.Message}";
            }
        }

        [McpServerTool, Description("Assign material to a GameObject's renderer")]
        public async ValueTask<string> AssignMaterialToRenderer(
            [Description("Name of the GameObject")]
            string objectName,
            [Description("Name of the material asset")]
            string materialName,
            [Description("Material slot index (default: 0)")]
            int materialIndex = 0)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = McpToolUtilities.FindGameObjectInScene(objectName);
                if (gameObject == null)
                    return $"Error: GameObject '{objectName}' not found";

                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer == null)
                    return $"Error: GameObject '{objectName}' does not have a Renderer component";

                var material = FindMaterial(materialName);
                if (material == null)
                    return $"Error: Material '{materialName}' not found";

                if (materialIndex < 0 || materialIndex >= renderer.sharedMaterials.Length)
                    return $"Error: Material index {materialIndex} is out of range. GameObject has {renderer.sharedMaterials.Length} material slots";

                var materials = renderer.sharedMaterials;
                materials[materialIndex] = material;
                renderer.sharedMaterials = materials;

                EditorUtility.SetDirty(renderer);

                return $"Successfully assigned material '{materialName}' to GameObject '{objectName}' at slot {materialIndex}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error assigning material: {e}");
                return $"Error assigning material: {e.Message}";
            }
        }

        [McpServerTool, Description("List all materials in the project")]
        public async ValueTask<string> ListMaterials(
            [Description("Optional name filter")]
            string nameFilter = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var materialGuids = AssetDatabase.FindAssets("t:Material");
                var materials = new List<string>();

                foreach (var guid in materialGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                    if (material != null)
                    {
                        if (string.IsNullOrEmpty(nameFilter) ||
                            material.name.ToLowerInvariant().Contains(nameFilter.ToLowerInvariant()))
                        {
                            materials.Add($"'{material.name}' at '{path}' (Shader: {material.shader.name})");
                        }
                    }
                }

                if (materials.Count == 0)
                    return nameFilter != null
                        ? $"No materials found matching filter '{nameFilter}'"
                        : "No materials found in project";

                var result = $"Found {materials.Count} materials";
                if (!string.IsNullOrEmpty(nameFilter))
                    result += $" matching '{nameFilter}'";
                result += ":\n" + string.Join("\n", materials);

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error listing materials: {e}");
                return $"Error listing materials: {e.Message}";
            }
        }

        [McpServerTool, Description("Manage assets and folders (create folder, create prefab, delete asset)")]
        public async ValueTask<string> ManageAsset(
            [Description("Operation: 'createFolder', 'createPrefab', or 'delete'")]
            string operation,
            [Description("Path for the operation")]
            string path,
            [Description("For createFolder: Name of the new folder (optional)")]
            string folderName = null,
            [Description("For createPrefab: GameObject name in scene (optional)")]
            string objectName = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                switch (operation?.ToLower())
                {
                    case "createfolder":
                        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folderName))
                            return "Error: Both path and folderName are required for createFolder";

                        if (!AssetDatabase.IsValidFolder(path))
                            return $"Error: Parent path '{path}' does not exist";

                        var fullPath = Path.Combine(path, folderName).Replace('\\', '/');

                        if (AssetDatabase.IsValidFolder(fullPath))
                            return $"Warning: Folder '{fullPath}' already exists";

                        var guid = AssetDatabase.CreateFolder(path, folderName);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        return string.IsNullOrEmpty(guid)
                            ? $"Error: Failed to create folder '{fullPath}'"
                            : $"Successfully created folder '{fullPath}'";

                    case "createprefab":
                        if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(path))
                            return "Error: Both objectName and path are required for createPrefab";

                        var gameObject = McpToolUtilities.FindGameObjectInScene(objectName);
                        if (gameObject == null)
                            return $"Error: GameObject '{objectName}' not found in scene";

                        if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                            path += ".prefab";

                        if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                            path = Path.Combine("Assets", path).Replace('\\', '/');

                        // Create directory structure if needed
                        var directory = Path.GetDirectoryName(path).Replace('\\', '/');
                        EnsureDirectoryExists(directory);

                        var prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, path);

                        if (prefab == null)
                            return $"Error: Failed to create prefab at '{path}'";

                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        return $"Successfully created prefab '{prefab.name}' at '{path}' from GameObject '{objectName}'";

                    case "delete":
                        if (string.IsNullOrEmpty(path))
                            return "Error: Path is required for delete operation";

                        if (!File.Exists(path) && !Directory.Exists(path))
                            return $"Error: Asset at path '{path}' does not exist";

                        var assetName = Path.GetFileName(path);

                        if (AssetDatabase.DeleteAsset(path))
                        {
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                            return $"Successfully deleted asset '{assetName}' at '{path}'";
                        }
                        else
                        {
                            return $"Error: Failed to delete asset at '{path}'";
                        }

                    default:
                        return "Error: operation must be 'createFolder', 'createPrefab', or 'delete'";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error managing asset: {e}");
                return $"Error managing asset: {e.Message}";
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
                var prefabs = new List<string>();

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
                    return nameFilter != null
                        ? $"No prefabs found matching filter '{nameFilter}'"
                        : "No prefabs found in project";

                var result = $"Found {prefabs.Count} prefabs";
                if (!string.IsNullOrEmpty(nameFilter))
                    result += $" matching '{nameFilter}'";
                result += ":\n" + string.Join("\n", prefabs);

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error listing prefabs: {e}");
                return $"Error listing prefabs: {e.Message}";
            }
        }

        private Material FindMaterial(string materialName)
        {
            var materialGuids = AssetDatabase.FindAssets($"t:Material {materialName}");

            foreach (var guid in materialGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (material != null && material.name == materialName)
                    return material;
            }

            return null;
        }

        private void EnsureDirectoryExists(string directory)
        {
            if (!AssetDatabase.IsValidFolder(directory))
            {
                var pathParts = directory.Split('/');
                var currentPath = pathParts[0];

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
        }

        [McpServerTool, Description("Get detailed information about a prefab asset including variant status")]
        public async ValueTask<string> GetPrefabAssetInfo(
            [Description("Path to the prefab asset (e.g., Assets/Prefabs/MyPrefab.prefab)")]
            string prefabPath)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                if (string.IsNullOrEmpty(prefabPath))
                    return "Error: prefabPath is required";

                if (!File.Exists(prefabPath))
                    return $"Error: Prefab file '{prefabPath}' does not exist";

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    return $"Error: Could not load prefab at '{prefabPath}'";

                var assetType = PrefabUtility.GetPrefabAssetType(prefab);
                var isVariant = assetType == PrefabAssetType.Variant;
                
                var info = new List<string>
                {
                    $"Name: {prefab.name}",
                    $"Path: {prefabPath}",
                    $"AssetType: {assetType}",
                    $"IsVariant: {isVariant}"
                };

                // Get component count
                var componentCount = prefab.GetComponents<UnityEngine.Component>().Length;
                info.Add($"ComponentCount: {componentCount}");

                // If it's a variant, get source prefab information
                if (isVariant)
                {
                    try
                    {
                        var sourcePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(prefab);
                        if (sourcePrefab != null)
                        {
                            var sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
                            info.Add($"SourcePrefabPath: {sourcePath}");
                            info.Add($"SourcePrefabName: {sourcePrefab.name}");
                        }
                        else
                        {
                            info.Add("SourcePrefabPath: [Unable to determine]");
                        }
                    }
                    catch (Exception e)
                    {
                        info.Add($"SourcePrefabPath: [Error: {e.Message}]");
                    }
                }

                // Get file size information
                var fileInfo = new FileInfo(prefabPath);
                info.Add($"FileSize: {fileInfo.Length} bytes");
                info.Add($"LastModified: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

                // Get dependencies
                var dependencies = AssetDatabase.GetDependencies(prefabPath, false);
                var dependencyCount = dependencies.Length - 1; // Exclude self
                info.Add($"DependencyCount: {dependencyCount}");

                if (dependencyCount > 0)
                {
                    var dependencyList = dependencies
                        .Where(dep => dep != prefabPath)
                        .Take(5) // Show first 5 dependencies
                        .Select(dep => $"  - {dep}");
                    
                    info.Add("Dependencies (first 5):");
                    info.AddRange(dependencyList);
                    
                    if (dependencyCount > 5)
                        info.Add($"  ... and {dependencyCount - 5} more");
                }

                return string.Join("\n", info);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting prefab asset info: {e}");
                return $"Error getting prefab asset info: {e.Message}";
            }
        }

    }
}