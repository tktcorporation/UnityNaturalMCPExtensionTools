using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Utility class containing common functionality for MCP tools
    /// </summary>
    internal static class McpToolUtilities
    {
        /// <summary>
        /// Finds a GameObject by name in the active scene, supporting both active and inactive objects
        /// </summary>
        /// <param name="objectName">Name of the GameObject to find</param>
        /// <returns>The GameObject if found, null otherwise</returns>
        public static GameObject FindGameObjectInScene(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return null;

            // Use Resources.FindObjectsOfTypeAll to find both active and inactive objects
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            
            foreach (var obj in allObjects)
            {
                // Skip objects not in the active scene
                if (obj.scene != EditorSceneManager.GetActiveScene())
                    continue;
                
                // Skip persistent objects (prefab assets)
                if (EditorUtility.IsPersistent(obj))
                    continue;
                
                // Check for exact name match first
                if (obj.name == objectName)
                    return obj;
            }
            
            // If no exact match found, try case-insensitive search
            foreach (var obj in allObjects)
            {
                if (obj.scene != EditorSceneManager.GetActiveScene())
                    continue;
                
                if (EditorUtility.IsPersistent(obj))
                    continue;
                
                if (string.Equals(obj.name, objectName, StringComparison.OrdinalIgnoreCase))
                    return obj;
            }
            
            return null;
        }

        /// <summary>
        /// Finds a GameObject in Prefab editing mode
        /// </summary>
        /// <param name="objectName">Name of the GameObject to find</param>
        /// <returns>The GameObject if found, null otherwise</returns>
        public static GameObject FindGameObjectInPrefabMode(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
                return null;

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
                return null;

            var rootObject = prefabStage.prefabContentsRoot;
            if (rootObject == null)
                return null;

            // Check root object first
            if (rootObject.name == objectName)
                return rootObject;

            // Search in all children
            var allTransforms = rootObject.GetComponentsInChildren<Transform>(true);
            foreach (var transform in allTransforms)
            {
                if (transform.name == objectName)
                    return transform.gameObject;
            }

            // If no exact match found, try case-insensitive search
            foreach (var transform in allTransforms)
            {
                if (string.Equals(transform.name, objectName, StringComparison.OrdinalIgnoreCase))
                    return transform.gameObject;
            }

            return null;
        }

        /// <summary>
        /// Finds a GameObject either in scene or in Prefab mode based on the context
        /// </summary>
        /// <param name="objectName">Name of the GameObject to find</param>
        /// <param name="inPrefabMode">Whether to search in Prefab mode</param>
        /// <returns>The GameObject if found, null otherwise</returns>
        public static GameObject FindGameObject(string objectName, bool inPrefabMode)
        {
            return inPrefabMode 
                ? FindGameObjectInPrefabMode(objectName) 
                : FindGameObjectInScene(objectName);
        }
    }
}