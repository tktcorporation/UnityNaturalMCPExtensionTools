using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace UnityNaturalMCPExtesion.Editor
{
    /// <summary>
    /// MCP tool for Unity Scene creation and management
    /// </summary>
    [McpServerToolType, Description("Unity Scene creation and management tools")]
    internal sealed class McpSceneManagementTool
    {
        [McpServerTool, Description("Create a new Unity Scene")]
        public async ValueTask<string> CreateScene(
            [Description("Name for the new scene")]
            string sceneName,
            [Description("Directory path to save the scene (optional, defaults to Assets/Scenes/)")]
            string scenePath = null,
            [Description("Scene template: 'Empty', '3D', '2D', 'UI' (optional, defaults to 'Empty')")]
            string template = "Empty")
        {
            try
            {
                await UniTask.SwitchToMainThread();

                if (string.IsNullOrEmpty(sceneName))
                    return "Error: sceneName is required";

                // Validate and set default path
                if (string.IsNullOrEmpty(scenePath))
                    scenePath = "Assets/Scenes/";

                // Ensure path ends with slash
                if (!scenePath.EndsWith("/"))
                    scenePath += "/";

                // Create directory if it doesn't exist
                var fullPath = Path.Combine(Application.dataPath, scenePath.Replace("Assets/", ""));
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    AssetDatabase.Refresh();
                }

                // Create new scene based on template
                var newScene = CreateSceneByTemplate(template);
                if (!newScene.IsValid())
                    return $"Error: Failed to create scene with template '{template}'";

                // Save the scene
                var sceneFilePath = scenePath + sceneName + ".unity";
                bool saved = EditorSceneManager.SaveScene(newScene, sceneFilePath);

                if (!saved)
                    return $"Error: Failed to save scene to '{sceneFilePath}'";

                AssetDatabase.Refresh();

                return $"Successfully created scene '{sceneName}' at '{sceneFilePath}' using template '{template}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating scene: {e}");
                return $"Error creating scene: {e.Message}";
            }
        }

        [McpServerTool, Description("Save the current scene or save as")]
        public async ValueTask<string> SaveScene(
            [Description("New name for the scene (optional, if not provided saves current scene)")]
            string sceneName = null,
            [Description("Directory path to save the scene (optional, uses current scene path if not provided)")]
            string scenePath = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var activeScene = EditorSceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                    return "Error: No active scene to save";

                bool isSaveAs = !string.IsNullOrEmpty(sceneName) || !string.IsNullOrEmpty(scenePath);

                if (isSaveAs)
                {
                    // Save As operation
                    var currentName = Path.GetFileNameWithoutExtension(activeScene.path);
                    var currentPath = Path.GetDirectoryName(activeScene.path);

                    var newName = !string.IsNullOrEmpty(sceneName) ? sceneName : currentName;
                    var newPath = !string.IsNullOrEmpty(scenePath) ? scenePath : currentPath;

                    // Ensure path ends with slash and starts with Assets/
                    if (!newPath.EndsWith("/"))
                        newPath += "/";
                    if (!newPath.StartsWith("Assets/"))
                        newPath = "Assets/" + newPath;

                    // Create directory if it doesn't exist
                    var fullPath = Path.Combine(Application.dataPath, newPath.Replace("Assets/", ""));
                    if (!Directory.Exists(fullPath))
                    {
                        Directory.CreateDirectory(fullPath);
                        AssetDatabase.Refresh();
                    }

                    var newScenePath = newPath + newName + ".unity";
                    bool saved = EditorSceneManager.SaveScene(activeScene, newScenePath);

                    if (!saved)
                        return $"Error: Failed to save scene as '{newScenePath}'";

                    AssetDatabase.Refresh();
                    return $"Successfully saved scene as '{newName}' at '{newScenePath}'";
                }
                else
                {
                    // Regular save operation
                    if (string.IsNullOrEmpty(activeScene.path))
                        return "Error: Scene has no path. Use sceneName parameter to save as new scene";

                    bool saved = EditorSceneManager.SaveScene(activeScene);
                    if (!saved)
                        return $"Error: Failed to save scene '{activeScene.name}'";

                    return $"Successfully saved scene '{activeScene.name}' at '{activeScene.path}'";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error saving scene: {e}");
                return $"Error saving scene: {e.Message}";
            }
        }

        [McpServerTool, Description("Load an existing scene")]
        public async ValueTask<string> LoadScene(
            [Description("Name or path of the scene to load")]
            string sceneName,
            [Description("Load mode: 'Single' or 'Additive' (optional, defaults to 'Single')")]
            string loadMode = "Single")
        {
            try
            {
                await UniTask.SwitchToMainThread();

                if (string.IsNullOrEmpty(sceneName))
                    return "Error: sceneName is required";

                // Parse load mode
                if (!Enum.TryParse<OpenSceneMode>(loadMode, true, out var mode))
                    return $"Error: Invalid load mode '{loadMode}'. Valid modes: Single, Additive";

                // Check if current scene has unsaved changes
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    bool save = EditorUtility.DisplayDialog(
                        "Save Scene",
                        "Current scene has unsaved changes. Save before loading new scene?",
                        "Save", "Don't Save");

                    if (save)
                    {
                        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                    }
                }

                // Find scene path
                string scenePath = FindScenePath(sceneName);
                if (string.IsNullOrEmpty(scenePath))
                    return $"Error: Scene '{sceneName}' not found in project";

                // Load scene
                var scene = EditorSceneManager.OpenScene(scenePath, mode);
                if (!scene.IsValid())
                    return $"Error: Failed to load scene '{sceneName}'";

                return $"Successfully loaded scene '{scene.name}' from '{scenePath}' in {loadMode} mode";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading scene: {e}");
                return $"Error loading scene: {e.Message}";
            }
        }

        [McpServerTool, Description("List all scenes in the project")]
        public async ValueTask<string> ListScenes(
            [Description("Filter scenes by name (optional)")]
            string nameFilter = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var sceneGuids = AssetDatabase.FindAssets("t:Scene");
                var scenes = new List<string>();

                foreach (var guid in sceneGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var sceneName = Path.GetFileNameWithoutExtension(path);

                    if (!string.IsNullOrEmpty(nameFilter) &&
                        !sceneName.ToLowerInvariant().Contains(nameFilter.ToLowerInvariant()))
                        continue;

                    var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    if (sceneAsset != null)
                    {
                        var sizeInfo = GetFileSizeString(path);
                        scenes.Add($"'{sceneName}' - {path} ({sizeInfo})");
                    }
                }

                if (scenes.Count == 0)
                    return nameFilter != null
                        ? $"No scenes found matching filter '{nameFilter}'"
                        : "No scenes found in project";

                var result = $"Found {scenes.Count} scene(s)";
                if (!string.IsNullOrEmpty(nameFilter))
                    result += $" matching '{nameFilter}'";
                result += ":\n" + string.Join("\n", scenes.OrderBy(s => s));

                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Error listing scenes: {e}");
                return $"Error listing scenes: {e.Message}";
            }
        }

        [McpServerTool, Description("Get information about the active scene")]
        public async ValueTask<string> GetActiveSceneInfo()
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var activeScene = EditorSceneManager.GetActiveScene();
                if (!activeScene.IsValid())
                    return "Error: No active scene";

                var info = new System.Text.StringBuilder();
                info.AppendLine($"Scene Name: {activeScene.name}");
                info.AppendLine($"Scene Path: {activeScene.path}");
                info.AppendLine($"Is Loaded: {activeScene.isLoaded}");
                info.AppendLine($"Is Dirty: {activeScene.isDirty}");
                info.AppendLine($"Build Index: {activeScene.buildIndex}");
                info.AppendLine($"Root Object Count: {activeScene.rootCount}");

                if (activeScene.rootCount > 0)
                {
                    info.AppendLine("Root GameObjects:");
                    var rootObjects = activeScene.GetRootGameObjects();
                    foreach (var obj in rootObjects)
                    {
                        var componentCount = obj.GetComponentsInChildren<UnityEngine.Component>(true).Length;
                        info.AppendLine($"  - {obj.name} ({componentCount} components in hierarchy)");
                    }
                }

                // Multi-scene info
                var sceneCount = EditorSceneManager.sceneCount;
                if (sceneCount > 1)
                {
                    info.AppendLine($"\nMulti-Scene Setup ({sceneCount} scenes loaded):");
                    for (int i = 0; i < sceneCount; i++)
                    {
                        var scene = EditorSceneManager.GetSceneAt(i);
                        var activeMarker = scene == activeScene ? " [ACTIVE]" : "";
                        info.AppendLine($"  - {scene.name}{activeMarker}");
                    }
                }

                return info.ToString();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error getting scene info: {e}");
                return $"Error getting scene info: {e.Message}";
            }
        }

        [McpServerTool, Description("Close a scene (useful in multi-scene setups)")]
        public async ValueTask<string> CloseScene(
            [Description("Name of the scene to close")]
            string sceneName,
            [Description("Remove the scene from hierarchy (optional, defaults to true)")]
            bool removeScene = true)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                if (string.IsNullOrEmpty(sceneName))
                    return "Error: sceneName is required";

                // Find the scene
                UnityEngine.SceneManagement.Scene sceneToClose = default;
                bool found = false;

                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    var scene = EditorSceneManager.GetSceneAt(i);
                    if (scene.name.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
                    {
                        sceneToClose = scene;
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return $"Error: Scene '{sceneName}' not found in currently loaded scenes";

                // Check if it's the only scene
                if (EditorSceneManager.sceneCount == 1)
                    return "Error: Cannot close the only loaded scene";

                // Check if scene has unsaved changes
                if (sceneToClose.isDirty)
                {
                    bool save = EditorUtility.DisplayDialog(
                        "Save Scene",
                        $"Scene '{sceneName}' has unsaved changes. Save before closing?",
                        "Save", "Don't Save");

                    if (save)
                    {
                        EditorSceneManager.SaveScene(sceneToClose);
                    }
                }

                // Close the scene
                bool closed = EditorSceneManager.CloseScene(sceneToClose, removeScene);
                if (!closed)
                    return $"Error: Failed to close scene '{sceneName}'";

                var action = removeScene ? "closed and removed" : "closed";
                return $"Successfully {action} scene '{sceneName}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error closing scene: {e}");
                return $"Error closing scene: {e.Message}";
            }
        }

        private UnityEngine.SceneManagement.Scene CreateSceneByTemplate(string template)
        {
            switch (template?.ToLower())
            {
                case "empty":
                    return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                case "3d":
                    return EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

                case "2d":
                    var scene2D = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    // Add 2D-specific setup
                    var camera = new GameObject("Main Camera");
                    var cam = camera.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    cam.orthographic = true;
                    cam.orthographicSize = 5;
                    cam.transform.position = new Vector3(0, 0, -10);
                    camera.AddComponent<AudioListener>();
                    camera.tag = "MainCamera";
                    return scene2D;

                case "ui":
                    var sceneUI = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    // Add UI-specific setup
                    var cameraUI = new GameObject("Main Camera");
                    var camUI = cameraUI.AddComponent<Camera>();
                    camUI.clearFlags = CameraClearFlags.SolidColor;
                    camUI.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
                    camUI.transform.position = new Vector3(0, 1, -10);
                    cameraUI.AddComponent<AudioListener>();
                    cameraUI.tag = "MainCamera";

                    // Add Canvas
                    var canvas = new GameObject("Canvas");
                    var canvasComp = canvas.AddComponent<Canvas>();
                    canvasComp.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.AddComponent<UnityEngine.UI.CanvasScaler>();
                    canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                    // Add EventSystem
                    var eventSystem = new GameObject("EventSystem");
                    eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                    eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

                    return sceneUI;

                default:
                    return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        private string FindScenePath(string sceneName)
        {
            // First try exact match
            var sceneGuids = AssetDatabase.FindAssets($"t:Scene {sceneName}");
            
            foreach (var guid in sceneGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                
                if (name.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
                    return path;
            }

            // If sceneName contains path, try direct path
            if (sceneName.Contains("/") || sceneName.Contains("\\"))
            {
                var normalizedPath = sceneName.Replace("\\", "/");
                if (!normalizedPath.EndsWith(".unity"))
                    normalizedPath += ".unity";
                if (!normalizedPath.StartsWith("Assets/"))
                    normalizedPath = "Assets/" + normalizedPath;

                if (File.Exists(Path.Combine(Application.dataPath, normalizedPath.Replace("Assets/", ""))))
                    return normalizedPath;
            }

            return null;
        }

        private string GetFileSizeString(string path)
        {
            try
            {
                var fullPath = Path.Combine(Application.dataPath, path.Replace("Assets/", ""));
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    var sizeInBytes = fileInfo.Length;
                    
                    if (sizeInBytes < 1024)
                        return $"{sizeInBytes} B";
                    else if (sizeInBytes < 1024 * 1024)
                        return $"{sizeInBytes / 1024.0:F1} KB";
                    else
                        return $"{sizeInBytes / (1024.0 * 1024.0):F1} MB";
                }
            }
            catch
            {
                // Ignore file access errors
            }
            
            return "unknown size";
        }
    }
}