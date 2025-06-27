using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace UnityNaturalMCPExtesion.Editor
{
    /// <summary>
    /// Builder for the Prefab edit mode MCP tool
    /// </summary>
    [CreateAssetMenu(menuName = "MCP/Tool Builder/Prefab Edit", fileName = "McpPrefabEditToolBuilder")]
    public class McpPrefabEditToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpPrefabEditTool>();
        }
    }
}