using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace Editor.McpTools
{
    /// <summary>
    /// Builder for the unified effect (particle system) MCP tool
    /// </summary>
    [CreateAssetMenu(menuName = "MCP/Tool Builder/Unified Effect", fileName = "McpUnifiedEffectToolBuilder")]
    public class McpUnifiedEffectToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpUnifiedEffectTool>();
        }
    }
}