using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// MCP tool for Unity project settings management (layers, tags, etc.)
    /// </summary>
    [McpServerToolType, Description("Unity project settings management tools")]
    internal sealed class McpProjectSettingsTool
    {
        [McpServerTool, Description("Manage project layers (list, set names, remove names)")]
        public async ValueTask<string> ManageProjectLayers(
            [Description("Operation to perform: 'listlayers', 'setlayername', or 'removelayername'")]
            string operation,
            [Description("For setlayername/removelayername: Layer index (8-31, optional)")]
            int? layerIndex = null,
            [Description("For setlayername: New layer name (optional)")]
            string layerName = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                switch (operation?.ToLower())
                {
                    case "listlayers":
                        var layerInfo = new System.Text.StringBuilder();
                        layerInfo.AppendLine("Project Layers (0-31):");
                        layerInfo.AppendLine("Built-in layers (0-7, non-editable):");
                        
                        for (int i = 0; i < 8; i++)
                        {
                            var name = LayerMask.LayerToName(i);
                            layerInfo.AppendLine($"  {i}: {(string.IsNullOrEmpty(name) ? "<unnamed>" : name)}");
                        }
                        
                        layerInfo.AppendLine("User layers (8-31, editable):");
                        for (int i = 8; i < 32; i++)
                        {
                            var name = LayerMask.LayerToName(i);
                            layerInfo.AppendLine($"  {i}: {(string.IsNullOrEmpty(name) ? "<unnamed>" : name)}");
                        }
                        
                        return layerInfo.ToString();

                    case "setlayername":
                        if (!layerIndex.HasValue)
                            return "Error: layerIndex is required for setlayername operation";
                        
                        if (!IsEditableLayer(layerIndex.Value))
                            return $"Error: Layer {layerIndex.Value} is not editable. Only layers 8-31 can be modified";
                        
                        if (string.IsNullOrEmpty(layerName))
                            return "Error: layerName is required for setlayername operation";
                        
                        // Check if layer name already exists
                        for (int i = 0; i < 32; i++)
                        {
                            if (i != layerIndex.Value && LayerMask.LayerToName(i) == layerName)
                                return $"Error: Layer name '{layerName}' already exists on layer {i}";
                        }
                        
                        var result = SetProjectLayerName(layerIndex.Value, layerName);
                        return result ? $"Successfully set layer {layerIndex.Value} name to '{layerName}'" 
                                     : $"Error: Failed to set layer {layerIndex.Value} name to '{layerName}'";

                    case "removelayername":
                        if (!layerIndex.HasValue)
                            return "Error: layerIndex is required for removelayername operation";
                        
                        if (!IsEditableLayer(layerIndex.Value))
                            return $"Error: Layer {layerIndex.Value} is not editable. Only layers 8-31 can be modified";
                        
                        var oldLayerName = LayerMask.LayerToName(layerIndex.Value);
                        if (string.IsNullOrEmpty(oldLayerName))
                            return $"Layer {layerIndex.Value} already has no name";
                        
                        var removeResult = SetProjectLayerName(layerIndex.Value, "");
                        return removeResult ? $"Successfully removed layer name from layer {layerIndex.Value} (was '{oldLayerName}')" 
                                           : $"Error: Failed to remove layer name from layer {layerIndex.Value}";

                    default:
                        return "Error: operation must be 'listlayers', 'setlayername', or 'removelayername'";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error managing project layers: {e}");
                return $"Error managing project layers: {e.Message}";
            }
        }

        // Helper methods for layer management
        private bool IsValidLayerIndex(int layerIndex)
        {
            return layerIndex >= 0 && layerIndex <= 31;
        }

        private bool IsEditableLayer(int layerIndex)
        {
            return layerIndex >= 8 && layerIndex <= 31;
        }

        private bool SetProjectLayerName(int layerIndex, string layerName)
        {
            try
            {
                // Access TagManager asset
                var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
                if (tagManagerAssets == null || tagManagerAssets.Length == 0)
                {
                    Debug.LogError("Could not find TagManager asset");
                    return false;
                }

                var tagManager = new SerializedObject(tagManagerAssets[0]);
                var layersProp = tagManager.FindProperty("layers");
                
                if (layersProp == null)
                {
                    Debug.LogError("Could not find layers property in TagManager");
                    return false;
                }

                if (layerIndex < layersProp.arraySize)
                {
                    var layerProp = layersProp.GetArrayElementAtIndex(layerIndex);
                    layerProp.stringValue = layerName ?? "";
                    tagManager.ApplyModifiedProperties();
                    
                    // Force refresh of layer names
                    AssetDatabase.Refresh();
                    
                    Debug.Log($"Set layer {layerIndex} name to '{layerName}'");
                    return true;
                }
                else
                {
                    Debug.LogError($"Layer index {layerIndex} is out of range");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting project layer name: {e.Message}");
                return false;
            }
        }
    }
}