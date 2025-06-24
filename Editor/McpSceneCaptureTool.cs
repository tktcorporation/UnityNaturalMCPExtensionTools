using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Editor.McpTools
{
    /// <summary>
    /// MCP tool for capturing Unity scene views to image files
    /// </summary>
    [McpServerToolType, Description("Scene capture tool for Unity Editor")]
    internal sealed class McpSceneCaptureTool
    {
        [McpServerTool, Description("Capture scene from camera view and save as PNG")]
        public async ValueTask<string> CaptureScene(
            [Description("Name of the camera to use for capture")]
            string cameraName,
            [Description("Camera position [x,y,z] (optional)")]
            float[] position = null,
            [Description("Camera rotation in euler angles [x,y,z] (optional)")]
            float[] rotation = null,
            [Description("Capture width in pixels (optional, default: 1920)")]
            int width = 1920,
            [Description("Capture height in pixels (optional, default: 1080)")]
            int height = 1080)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                // Find the camera
                Camera targetCamera = null;
                if (!string.IsNullOrEmpty(cameraName))
                {
                    var cameraObj = GameObject.Find(cameraName);
                    if (cameraObj != null)
                        targetCamera = cameraObj.GetComponent<Camera>();
                }

                // Fallback to main camera if not found
                if (targetCamera == null)
                {
                    targetCamera = Camera.main;
                    if (targetCamera == null)
                    {
                        // Find any camera in the scene
                        targetCamera = GameObject.FindObjectOfType<Camera>();
                        if (targetCamera == null)
                            return "Error: No camera found in the scene";
                    }
                }

                // Store original transform
                var originalPosition = targetCamera.transform.position;
                var originalRotation = targetCamera.transform.eulerAngles;

                try
                {
                    // Set camera transform if specified
                    if (position != null && position.Length >= 3)
                        targetCamera.transform.position = new Vector3(position[0], position[1], position[2]);

                    if (rotation != null && rotation.Length >= 3)
                        targetCamera.transform.eulerAngles = new Vector3(rotation[0], rotation[1], rotation[2]);

                    // Create render texture
                    var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    renderTexture.Create();

                    // Store original target texture
                    var originalTargetTexture = targetCamera.targetTexture;

                    // Render to texture
                    targetCamera.targetTexture = renderTexture;
                    targetCamera.Render();

                    // Create Texture2D and read pixels
                    RenderTexture.active = renderTexture;
                    var texture2D = new Texture2D(width, height, TextureFormat.RGB24, false);
                    texture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture2D.Apply();

                    // Reset render texture
                    targetCamera.targetTexture = originalTargetTexture;
                    RenderTexture.active = null;

                    // Create output directory if it doesn't exist
                    var projectPath = Path.GetDirectoryName(Application.dataPath);
                    var outputDirectory = Path.Combine(projectPath, "SceneCapture");
                    if (!Directory.Exists(outputDirectory))
                        Directory.CreateDirectory(outputDirectory);

                    // Generate filename with timestamp
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                    var filename = $"capture_{timestamp}.png";
                    var fullPath = Path.Combine(outputDirectory, filename);

                    // Save PNG
                    var pngData = texture2D.EncodeToPNG();
                    File.WriteAllBytes(fullPath, pngData);

                    // Cleanup
                    GameObject.DestroyImmediate(texture2D);
                    GameObject.DestroyImmediate(renderTexture);

                    Debug.Log($"Scene captured successfully: {fullPath}");
                    return $"Scene captured successfully to: {fullPath}";
                }
                finally
                {
                    // Restore original transform
                    targetCamera.transform.position = originalPosition;
                    targetCamera.transform.eulerAngles = originalRotation;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to capture scene: {e}");
                return $"Error: Failed to capture scene - {e.Message}";
            }
        }

        [McpServerTool, Description("List all captured screenshots in SceneCapture folder")]
        public async ValueTask<string> ListCapturedScreenshots()
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var projectPath = Path.GetDirectoryName(Application.dataPath);
                var captureDirectory = Path.Combine(projectPath, "SceneCapture");

                if (!Directory.Exists(captureDirectory))
                    return "No captures found. SceneCapture folder does not exist.";

                var files = Directory.GetFiles(captureDirectory, "*.png");
                if (files.Length == 0)
                    return "No PNG files found in SceneCapture folder.";

                var fileList = "Captured screenshots:\n";
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    fileList += $"- {fileInfo.Name} (Size: {fileInfo.Length / 1024}KB, Modified: {fileInfo.LastWriteTime})\n";
                }

                return fileList;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to list screenshots: {e}");
                return $"Error: Failed to list screenshots - {e.Message}";
            }
        }
    }
}