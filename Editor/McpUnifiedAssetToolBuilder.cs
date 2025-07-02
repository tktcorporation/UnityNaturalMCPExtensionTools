using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Builder for the unified asset management MCP tool
    /// </summary>
    [CreateAssetMenu(menuName = "MCP/Tool Builder/Unified Asset", fileName = "McpUnifiedAssetToolBuilder")]
    public class McpUnifiedAssetToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpUnifiedAssetTool>();
        }
    }
}