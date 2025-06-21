using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace Editor.McpTools
{
    /// <summary>
    /// Builder for registering McpAssetTool with the MCP server
    /// </summary>
    [CreateAssetMenu(fileName = "McpAssetToolBuilder", menuName = "UnityNaturalMCP/Asset Tool Builder")]
    public class McpAssetToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpAssetTool>();
        }
    }
}