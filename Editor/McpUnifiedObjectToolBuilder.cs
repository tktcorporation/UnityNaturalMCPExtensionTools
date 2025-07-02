using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Builder for the unified object manipulation MCP tool
    /// </summary>
    [CreateAssetMenu(menuName = "MCP/Tool Builder/Unified Object", fileName = "McpUnifiedObjectToolBuilder")]
    public class McpUnifiedObjectToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpUnifiedObjectTool>();
        }
    }
}