using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace Editor.McpTools
{
    /// <summary>
    /// Builder for registering McpSceneObjectTool with the MCP server
    /// </summary>
    [CreateAssetMenu(fileName = "McpSceneObjectToolBuilder", menuName = "UnityNaturalMCP/Scene Object Tool Builder")]
    public class McpSceneObjectToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpSceneObjectTool>();
        }
    }
}