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
    internal sealed class McpUnifiedAssetTool : McpToolBase
    {
        [McpServerTool, Description("Create or update materials using structured configuration")]
        public async ValueTask<string> ManageMaterial(
            [Description("Material configuration JSON or structured settings")]
            string configurationJson,
            [Description("Operation: 'create' or 'update'")]
            string operation)
        {
            return await ExecuteOperation(async () =>
            {
                // Parse and validate configuration
                if (!McpConfigurationManager.TryParseConfiguration<MaterialConfiguration>(configurationJson, out var config, out var validationResult))
                {
                    return McpToolUtilities.CreateErrorMessage("ManageMaterial", $"Configuration validation failed: {validationResult}");
                }

                Material material = null;
                var changes = new List<string>();

                switch (operation?.ToLower())
                {
                    case "create":
                        var shader = Shader.Find(config.shaderName);
                        if (shader == null)
                            return McpToolUtilities.CreateErrorMessage("ManageMaterial", $"Shader '{config.shaderName}' not found");

                        material = new Material(shader);
                        material.name = config.materialName;

                        var path = $"Assets/Materials/{config.materialName}.mat";

                        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                            AssetDatabase.CreateFolder("Assets", "Materials");

                        AssetDatabase.CreateAsset(material, path);
                        changes.Add($"created at '{path}' with shader '{config.shaderName}'");
                        break;

                    case "update":
                        material = FindMaterial(config.materialName);
                        if (material == null)
                            return McpToolUtilities.CreateErrorMessage("ManageMaterial", $"Material '{config.materialName}' not found");
                        break;

                    default:
                        return McpToolUtilities.CreateErrorMessage("ManageMaterial", "operation must be 'create' or 'update'");
                }

                // Apply base color
                if (config.baseColor != null && config.baseColor.Length >= 3)
                {
                    var color = new Color(
                        config.baseColor[0],
                        config.baseColor[1],
                        config.baseColor[2],
                        config.baseColor.Length > 3 ? config.baseColor[3] : 1.0f
                    );

                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", color);
                    else if (material.HasProperty("_Color"))
                        material.SetColor("_Color", color);

                    changes.Add($"baseColor: ({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})");
                }

                // Apply metallic
                if (material.HasProperty("_Metallic"))
                {
                    material.SetFloat("_Metallic", config.metallic);
                    changes.Add($"metallic: {config.metallic:F2}");
                }

                // Apply smoothness
                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", config.smoothness);
                    changes.Add($"smoothness: {config.smoothness:F2}");
                }

                // Apply emission
                if (config.emission != null && config.emission.Length >= 3 && material.HasProperty("_EmissionColor"))
                {
                    var emissionColor = new Color(
                        config.emission[0] * config.emissionIntensity,
                        config.emission[1] * config.emissionIntensity,
                        config.emission[2] * config.emissionIntensity,
                        1.0f
                    );
                    material.SetColor("_EmissionColor", emissionColor);
                    material.EnableKeyword("_EMISSION");
                    changes.Add($"emission: ({emissionColor.r:F2}, {emissionColor.g:F2}, {emissionColor.b:F2}) intensity: {config.emissionIntensity:F2}");
                }

                // Apply additional properties
                if (config.additionalProperties != null)
                {
                    foreach (var prop in config.additionalProperties)
                    {
                        if (!material.HasProperty(prop.Key))
                            continue;

                        var shader = material.shader;
                        var propertyIndex = shader.FindPropertyIndex(prop.Key);
                        if (propertyIndex == -1)
                            continue;

                        var propertyType = shader.GetPropertyType(propertyIndex);

                        try
                        {
                            switch (propertyType)
                            {
                                case UnityEngine.Rendering.ShaderPropertyType.Float:
                                case UnityEngine.Rendering.ShaderPropertyType.Range:
                                    if (prop.Value is float floatVal)
                                    {
                                        material.SetFloat(prop.Key, floatVal);
                                        changes.Add($"{prop.Key}: {floatVal:F2}");
                                    }
                                    else if (float.TryParse(prop.Value?.ToString(), out floatVal))
                                    {
                                        material.SetFloat(prop.Key, floatVal);
                                        changes.Add($"{prop.Key}: {floatVal:F2}");
                                    }
                                    break;

                                case UnityEngine.Rendering.ShaderPropertyType.Color:
                                    if (prop.Value is float[] colorArray && colorArray.Length >= 3)
                                    {
                                        var color = new Color(
                                            colorArray[0],
                                            colorArray[1],
                                            colorArray[2],
                                            colorArray.Length > 3 ? colorArray[3] : 1.0f
                                        );
                                        material.SetColor(prop.Key, color);
                                        changes.Add($"{prop.Key}: ({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})");
                                    }
                                    break;

                                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                                    if (prop.Value is float[] vectorArray && vectorArray.Length >= 4)
                                    {
                                        var vector = new Vector4(vectorArray[0], vectorArray[1], vectorArray[2], vectorArray[3]);
                                        material.SetVector(prop.Key, vector);
                                        changes.Add($"{prop.Key}: ({vector.x:F2}, {vector.y:F2}, {vector.z:F2}, {vector.w:F2})");
                                    }
                                    break;

                                case UnityEngine.Rendering.ShaderPropertyType.Int:
                                    if (prop.Value is int intVal)
                                    {
                                        material.SetInt(prop.Key, intVal);
                                        changes.Add($"{prop.Key}: {intVal}");
                                    }
                                    else if (int.TryParse(prop.Value?.ToString(), out intVal))
                                    {
                                        material.SetInt(prop.Key, intVal);
                                        changes.Add($"{prop.Key}: {intVal}");
                                    }
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"Failed to set property '{prop.Key}': {ex.Message}");
                        }
                    }
                }

                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return changes.Count > 0
                    ? McpToolUtilities.CreateSuccessMessage($"{operation}d material", config.materialName, string.Join(", ", changes))
                    : McpToolUtilities.CreateSuccessMessage($"Material {operation}d", config.materialName, "no property changes");
            }, "ManageMaterial");
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
            return await ExecuteWithErrorHandling(async () =>
            {
                var gameObject = await FindGameObjectSafe(objectName, false);
                if (gameObject == null)
                    return McpToolUtilities.CreateErrorMessage("AssignMaterialToRenderer", "GameObject not found", objectName);

                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer == null)
                    return McpToolUtilities.CreateErrorMessage("AssignMaterialToRenderer", "GameObject does not have a Renderer component", objectName);

                var material = FindMaterial(materialName);
                if (material == null)
                    return McpToolUtilities.CreateErrorMessage("AssignMaterialToRenderer", "Material not found", materialName);

                if (materialIndex < 0 || materialIndex >= renderer.sharedMaterials.Length)
                    return McpToolUtilities.CreateErrorMessage("AssignMaterialToRenderer", $"Material index {materialIndex} is out of range. GameObject has {renderer.sharedMaterials.Length} material slots");

                var materials = renderer.sharedMaterials;
                materials[materialIndex] = material;
                renderer.sharedMaterials = materials;

                EditorUtility.SetDirty(renderer);
                MarkSceneDirty(false);

                return McpToolUtilities.CreateSuccessMessage("Assigned material", materialName, $"to GameObject '{objectName}' at slot {materialIndex}");
            }, "AssignMaterialToRenderer");
        }

        [McpServerTool, Description("List all materials in the project")]
        public async ValueTask<string> ListMaterials(
            [Description("Optional name filter")]
            string nameFilter = null)
        {
            return await ExecuteWithErrorHandling(async () =>
            {

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
            }, "ListMaterials");
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
            return await ExecuteWithErrorHandling(async () =>
            {

                switch (operation?.ToLower())
                {
                    case "createfolder":
                        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folderName))
                            return McpToolUtilities.CreateErrorMessage("ManageAsset", "Both path and folderName are required for createFolder");

                        if (!AssetDatabase.IsValidFolder(path))
                            return McpToolUtilities.CreateErrorMessage("ManageAsset", "Parent path does not exist", path);

                        var fullPath = Path.Combine(path, folderName).Replace('\\', '/');

                        if (AssetDatabase.IsValidFolder(fullPath))
                            return $"Warning: Folder '{fullPath}' already exists";

                        var guid = AssetDatabase.CreateFolder(path, folderName);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        return string.IsNullOrEmpty(guid)
                            ? McpToolUtilities.CreateErrorMessage("ManageAsset", "Failed to create folder", fullPath)
                            : McpToolUtilities.CreateSuccessMessage("Created folder", fullPath);

                    case "createprefab":
                        if (string.IsNullOrEmpty(objectName) || string.IsNullOrEmpty(path))
                            return McpToolUtilities.CreateErrorMessage("ManageAsset", "Both objectName and path are required for createPrefab");

                        var gameObject = await FindGameObjectSafe(objectName, false);
                        if (gameObject == null)
                            return McpToolUtilities.CreateErrorMessage("ManageAsset", "GameObject not found in scene", objectName);

                        if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                            path += ".prefab";

                        if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                            path = Path.Combine("Assets", path).Replace('\\', '/');

                        // Create directory structure if needed
                        var directory = Path.GetDirectoryName(path).Replace('\\', '/');
                        EnsureDirectoryExists(directory);

                        var prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, path);

                        if (prefab == null)
                            return McpToolUtilities.CreateErrorMessage("ManageAsset", "Failed to create prefab", path);

                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        return McpToolUtilities.CreateSuccessMessage("Created prefab", prefab.name, $"at '{path}' from GameObject '{objectName}'");

                    case "delete":
                        if (string.IsNullOrEmpty(path))
                            return McpToolUtilities.CreateErrorMessage("ManageAsset", "Path is required for delete operation");

                        if (!File.Exists(path) && !Directory.Exists(path))
                            return McpToolUtilities.CreateErrorMessage("ManageAsset", "Asset does not exist", path);

                        var assetName = Path.GetFileName(path);

                        if (AssetDatabase.DeleteAsset(path))
                        {
                            AssetDatabase.SaveAssets();
                            AssetDatabase.Refresh();
                            return McpToolUtilities.CreateSuccessMessage("Deleted asset", assetName, $"at '{path}'");
                        }
                        else
                        {
                            return McpToolUtilities.CreateErrorMessage("ManageAsset", "Failed to delete asset", path);
                        }

                    default:
                        return McpToolUtilities.CreateErrorMessage("ManageAsset", "operation must be 'createFolder', 'createPrefab', or 'delete'");
                }
            }, "ManageAsset");
        }

        [McpServerTool, Description("List all prefabs in the project")]
        public async ValueTask<string> ListPrefabs(
            [Description("Optional name filter")]
            string nameFilter = null)
        {
            return await ExecuteWithErrorHandling(async () =>
            {

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
            }, "ListPrefabs");
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
            return await ExecuteWithErrorHandling(async () =>
            {

                if (string.IsNullOrEmpty(prefabPath))
                    return "Error: prefabPath is required";

                if (!File.Exists(prefabPath))
                    return McpToolUtilities.CreateErrorMessage("GetPrefabAssetInfo", "Prefab file does not exist", prefabPath);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    return McpToolUtilities.CreateErrorMessage("GetPrefabAssetInfo", "Could not load prefab", prefabPath);

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
            }, "GetPrefabAssetInfo");
        }

    }
}