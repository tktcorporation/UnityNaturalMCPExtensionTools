using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Editor.McpTools
{
    /// <summary>
    /// MCP tool for creating and manipulating materials in Unity
    /// </summary>
    [McpServerToolType, Description("Material creation and manipulation tools for Unity")]
    internal sealed class McpMaterialTool
    {
        [McpServerTool, Description("Create a new material asset")]
        public async ValueTask<string> CreateMaterial(
            [Description("Name for the material")]
            string materialName,
            [Description("Shader name (default: Universal Render Pipeline/Lit)")]
            string shaderName = "Universal Render Pipeline/Lit")
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // Find the shader
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    return $"Error: Shader '{shaderName}' not found";
                }

                // Create the material
                var material = new Material(shader);
                material.name = materialName;

                // Create the asset path
                var path = $"Assets/Materials/{materialName}.mat";
                
                // Ensure the Materials directory exists
                var materialsDir = "Assets/Materials";
                if (!AssetDatabase.IsValidFolder(materialsDir))
                {
                    AssetDatabase.CreateFolder("Assets", "Materials");
                }

                // Create the asset
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return $"Successfully created material '{materialName}' at '{path}' with shader '{shaderName}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating material: {e}");
                return $"Error creating material: {e.Message}";
            }
        }

        [McpServerTool, Description("Set material color property")]
        public async ValueTask<string> SetMaterialColor(
            [Description("Name of the material asset")]
            string materialName,
            [Description("Property name (e.g., '_BaseColor', '_Color')")]
            string propertyName,
            [Description("Color values as array [R, G, B, A] (0-1 range)")]
            float[] colorValues)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                if (colorValues == null || colorValues.Length < 3)
                {
                    return "Error: Color values must be an array of at least 3 values [R, G, B] or 4 values [R, G, B, A]";
                }

                var material = FindMaterial(materialName);
                if (material == null)
                {
                    return $"Error: Material '{materialName}' not found";
                }

                var color = new Color(
                    colorValues[0],
                    colorValues[1], 
                    colorValues[2],
                    colorValues.Length > 3 ? colorValues[3] : 1.0f
                );

                if (!material.HasProperty(propertyName))
                {
                    return $"Error: Material '{materialName}' does not have property '{propertyName}'";
                }

                material.SetColor(propertyName, color);
                
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return $"Successfully set color property '{propertyName}' on material '{materialName}' to ({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting material color: {e}");
                return $"Error setting material color: {e.Message}";
            }
        }

        [McpServerTool, Description("Set material float property (Metallic, Smoothness, etc.)")]
        public async ValueTask<string> SetMaterialFloat(
            [Description("Name of the material asset")]
            string materialName,
            [Description("Property name (e.g., '_Metallic', '_Smoothness')")]
            string propertyName,
            [Description("Float value (typically 0-1 range)")]
            float value)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var material = FindMaterial(materialName);
                if (material == null)
                {
                    return $"Error: Material '{materialName}' not found";
                }

                if (!material.HasProperty(propertyName))
                {
                    return $"Error: Material '{materialName}' does not have property '{propertyName}'";
                }

                material.SetFloat(propertyName, value);
                
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return $"Successfully set float property '{propertyName}' on material '{materialName}' to {value:F3}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting material float: {e}");
                return $"Error setting material float: {e.Message}";
            }
        }

        [McpServerTool, Description("Set material emission property")]
        public async ValueTask<string> SetMaterialEmission(
            [Description("Name of the material asset")]
            string materialName,
            [Description("Emission color values as array [R, G, B] (0-1 range)")]
            float[] emissionColor,
            [Description("Emission intensity multiplier (default: 1.0)")]
            float intensity = 1.0f)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                if (emissionColor == null || emissionColor.Length < 3)
                {
                    return "Error: Emission color must be an array of at least 3 values [R, G, B]";
                }

                var material = FindMaterial(materialName);
                if (material == null)
                {
                    return $"Error: Material '{materialName}' not found";
                }

                var emission = new Color(
                    emissionColor[0] * intensity,
                    emissionColor[1] * intensity,
                    emissionColor[2] * intensity,
                    1.0f
                );

                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", emission);
                    material.EnableKeyword("_EMISSION");
                }
                else
                {
                    return $"Error: Material '{materialName}' does not support emission";
                }
                
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return $"Successfully set emission on material '{materialName}' to ({emission.r:F2}, {emission.g:F2}, {emission.b:F2}) with intensity {intensity:F2}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting material emission: {e}");
                return $"Error setting material emission: {e.Message}";
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

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found";
                }

                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer == null)
                {
                    return $"Error: GameObject '{objectName}' does not have a Renderer component";
                }

                var material = FindMaterial(materialName);
                if (material == null)
                {
                    return $"Error: Material '{materialName}' not found";
                }

                if (materialIndex < 0 || materialIndex >= renderer.materials.Length)
                {
                    return $"Error: Material index {materialIndex} is out of range. GameObject has {renderer.materials.Length} material slots";
                }

                var materials = renderer.materials;
                materials[materialIndex] = material;
                renderer.materials = materials;

                EditorUtility.SetDirty(renderer);

                return $"Successfully assigned material '{materialName}' to GameObject '{objectName}' at slot {materialIndex}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error assigning material: {e}");
                return $"Error assigning material: {e.Message}";
            }
        }

        [McpServerTool, Description("Set material shader")]
        public async ValueTask<string> SetMaterialShader(
            [Description("Name of the material asset")]
            string materialName,
            [Description("Shader name (e.g., 'Universal Render Pipeline/Lit')")]
            string shaderName)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var material = FindMaterial(materialName);
                if (material == null)
                {
                    return $"Error: Material '{materialName}' not found";
                }

                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    return $"Error: Shader '{shaderName}' not found";
                }

                material.shader = shader;
                
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();

                return $"Successfully set shader '{shaderName}' on material '{materialName}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting material shader: {e}");
                return $"Error setting material shader: {e.Message}";
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
                var materials = new System.Collections.Generic.List<string>();

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
                {
                    return nameFilter != null 
                        ? $"No materials found matching filter '{nameFilter}'" 
                        : "No materials found in project";
                }

                var result = $"Found {materials.Count} materials";
                if (!string.IsNullOrEmpty(nameFilter))
                {
                    result += $" matching '{nameFilter}'";
                }
                result += ":\n" + string.Join("\n", materials);

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error listing materials: {e}");
                return $"Error listing materials: {e.Message}";
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
                {
                    return material;
                }
            }
            
            return null;
        }
    }
}