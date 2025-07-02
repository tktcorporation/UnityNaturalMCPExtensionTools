using UnityEngine;
using UnityNaturalMCP.Editor;
using Microsoft.Extensions.DependencyInjection;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Builder for the scene capture MCP tool
    /// </summary>
    [CreateAssetMenu(menuName = "MCP/Tool Builder/Scene Capture", fileName = "McpSceneCaptureToolBuilder")]
    public class McpSceneCaptureToolBuilder : McpBuilderScriptableObject
    {
        public override void Build(IMcpServerBuilder builder)
        {
            builder.WithTools<McpSceneCaptureTool>();
        }
    }
}