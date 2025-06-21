using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace Editor.McpTools
{
    /// <summary>
    /// Builder for registering McpMaterialTool with the MCP server
    /// </summary>
    [CreateAssetMenu(fileName = "McpMaterialToolBuilder", menuName = "UnityNaturalMCP/Material Tool Builder")]
    public class McpMaterialToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpMaterialTool>();
        }
    }
}