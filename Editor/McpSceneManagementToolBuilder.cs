using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace UnityNaturalMCPExtesion.Editor
{
    /// <summary>
    /// Builder for the scene management MCP tool
    /// </summary>
    [CreateAssetMenu(menuName = "MCP/Tool Builder/Scene Management", fileName = "McpSceneManagementToolBuilder")]
    public class McpSceneManagementToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpSceneManagementTool>();
        }
    }
}