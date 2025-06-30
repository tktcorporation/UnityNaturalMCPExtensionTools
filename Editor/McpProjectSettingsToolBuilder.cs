using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Builder for the Unity project settings MCP tool
    /// </summary>
    [CreateAssetMenu(menuName = "MCP/Tool Builder/Project Settings", fileName = "McpProjectSettingsToolBuilder")]
    public class McpProjectSettingsToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpProjectSettingsTool>();
        }
    }
}