using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace Editor.McpTools
{
    /// <summary>
    /// Builder for registering McpComponentPropertyTool with the MCP server
    /// </summary>
    [CreateAssetMenu(fileName = "McpComponentPropertyToolBuilder", menuName = "UnityNaturalMCP/Component Property Tool Builder")]
    public class McpComponentPropertyToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpComponentPropertyTool>();
        }
    }
}