using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Gets all GameObjects in the current context (scene or prefab mode)
        /// </summary>
        /// <param name="inPrefabMode">Whether to get objects from prefab mode</param>
        /// <param name="includeInactive">Whether to include inactive objects</param>
        /// <returns>List of GameObjects in the current context</returns>
        public static List<GameObject> GetAllGameObjects(bool inPrefabMode, bool includeInactive = true)
        {
            var result = new List<GameObject>();

            if (inPrefabMode)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage?.prefabContentsRoot != null)
                {
                    result.Add(prefabStage.prefabContentsRoot);
                    var childTransforms = prefabStage.prefabContentsRoot.GetComponentsInChildren<Transform>(includeInactive);
                    foreach (var transform in childTransforms)
                    {
                        if (transform.gameObject != prefabStage.prefabContentsRoot)
                        {
                            result.Add(transform.gameObject);
                        }
                    }
                }
            }
            else
            {
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.scene != EditorSceneManager.GetActiveScene())
                        continue;
                    
                    if (EditorUtility.IsPersistent(obj))
                        continue;

                    if (!includeInactive && !obj.activeInHierarchy)
                        continue;

                    result.Add(obj);
                }
            }

            return result;
        }

        /// <summary>
        /// Validates if the current context supports the requested operation mode
        /// </summary>
        /// <param name="inPrefabMode">Whether prefab mode is required</param>
        /// <returns>True if context is valid, false otherwise</returns>
        public static bool ValidateContext(bool inPrefabMode)
        {
            if (inPrefabMode)
            {
                return PrefabStageUtility.GetCurrentPrefabStage() != null;
            }
            
            return EditorSceneManager.GetActiveScene().IsValid();
        }

        /// <summary>
        /// Gets a descriptive string about the current context
        /// </summary>
        /// <param name="inPrefabMode">Whether in prefab mode</param>
        /// <returns>Context description string</returns>
        public static string GetContextDescription(bool inPrefabMode)
        {
            if (inPrefabMode)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null)
                {
                    return $"Prefab Mode: {prefabStage.assetPath}";
                }
                return "Prefab Mode (Invalid)";
            }
            
            var activeScene = EditorSceneManager.GetActiveScene();
            return $"Scene Mode: {activeScene.name}";
        }

        /// <summary>
        /// Finds a component of specified type on a GameObject
        /// </summary>
        /// <typeparam name="T">Component type to find</typeparam>
        /// <param name="gameObject">GameObject to search on</param>
        /// <param name="includeInactive">Whether to search on inactive objects</param>
        /// <returns>The component if found, null otherwise</returns>
        public static T FindComponent<T>(GameObject gameObject, bool includeInactive = false) where T : Component
        {
            if (gameObject == null)
                return null;

            var component = gameObject.GetComponent<T>();
            if (component != null)
                return component;

            if (includeInactive)
            {
                return gameObject.GetComponentInChildren<T>(true);
            }

            return gameObject.GetComponentInChildren<T>();
        }

        /// <summary>
        /// Creates a standardized success message
        /// </summary>
        /// <param name="operation">The operation that succeeded</param>
        /// <param name="objectName">Name of the object operated on</param>
        /// <param name="additionalInfo">Additional information to include</param>
        /// <returns>Formatted success message</returns>
        public static string CreateSuccessMessage(string operation, string objectName = null, string additionalInfo = null)
        {
            var message = $"{operation} completed successfully";
            
            if (!string.IsNullOrEmpty(objectName))
            {
                message += $" for '{objectName}'";
            }
            
            if (!string.IsNullOrEmpty(additionalInfo))
            {
                message += $". {additionalInfo}";
            }
            
            return message;
        }

        /// <summary>
        /// Creates a standardized error message
        /// </summary>
        /// <param name="operation">The operation that failed</param>
        /// <param name="reason">The reason for failure</param>
        /// <param name="objectName">Name of the object operated on</param>
        /// <returns>Formatted error message</returns>
        public static string CreateErrorMessage(string operation, string reason, string objectName = null)
        {
            var message = $"Error: {operation} failed";
            
            if (!string.IsNullOrEmpty(objectName))
            {
                message += $" for '{objectName}'";
            }
            
            message += $". {reason}";
            
            return message;
        }
    }
}