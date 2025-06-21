using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace Editor.McpTools
{
    /// <summary>
    /// Builder for registering McpParticleTool with the MCP server
    /// </summary>
    [CreateAssetMenu(fileName = "McpParticleToolBuilder", menuName = "UnityNaturalMCP/Particle Tool Builder")]
    public class McpParticleToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpParticleTool>();
        }
    }
}