using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Base class providing common functionality for all MCP tools
    /// Eliminates code duplication and provides standardized error handling, prefab mode validation, and scene management
    /// </summary>
    internal abstract class McpToolBase
    {
        /// <summary>
        /// Executes an operation with standardized error handling and main thread switching
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">The operation to execute</param>
        /// <param name="operationName">Name of the operation for error logging</param>
        /// <returns>The result of the operation or an error message</returns>
        protected async ValueTask<T> ExecuteWithErrorHandling<T>(Func<ValueTask<T>> operation, string operationName)
        {
            try
            {
                await UniTask.SwitchToMainThread();
                return await operation();
            }
            catch (Exception e)
            {
                var errorMessage = $"Error in {operationName}: {e.Message}";
                Debug.LogError($"{GetType().Name} - {errorMessage}\n{e}");
                
                // Return appropriate error response based on type T
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)errorMessage;
                }
                
                return default(T);
            }
        }

        /// <summary>
        /// Executes an operation with standard error handling
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="operationName">Name of the operation for error messages</param>
        /// <returns>Result string from the operation</returns>
        protected async ValueTask<string> ExecuteOperation(Func<ValueTask<string>> operation, string operationName)
        {
            try
            {
                await UniTask.SwitchToMainThread();
                return await operation();
            }
            catch (Exception e)
            {
                Debug.LogError($"{GetType().Name} - Error {operationName}: {e}");
                return McpToolUtilities.CreateErrorMessage($"{operationName}: {e.Message}");
            }
        }

        /// <summary>
        /// Validates prefab mode state and returns appropriate error if invalid
        /// </summary>
        /// <param name="inPrefabMode">Whether operation should be in prefab mode</param>
        /// <returns>Error message if validation fails, null if valid</returns>
        protected async ValueTask<string> ValidatePrefabMode(bool inPrefabMode)
        {
            await UniTask.SwitchToMainThread();
            
            if (inPrefabMode)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage == null)
                {
                    return "Error: Prefab mode is not active. Please open a prefab first.";
                }
            }
            
            return null; // Validation passed
        }

        /// <summary>
        /// Marks the appropriate scene as dirty (prefab scene or active scene)
        /// </summary>
        /// <param name="inPrefabMode">Whether to mark prefab scene or active scene</param>
        protected void MarkSceneDirty(bool inPrefabMode)
        {
            if (inPrefabMode)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null)
                {
                    EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                }
            }
            else
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        /// <summary>
        /// Safely finds a GameObject with proper error handling and context awareness
        /// </summary>
        /// <param name="objectName">Name of the GameObject to find</param>
        /// <param name="inPrefabMode">Whether to search in prefab mode</param>
        /// <returns>The found GameObject or null if not found</returns>
        protected async ValueTask<GameObject> FindGameObjectSafe(string objectName, bool inPrefabMode)
        {
            await UniTask.SwitchToMainThread();
            
            if (string.IsNullOrEmpty(objectName))
            {
                Debug.LogWarning($"{GetType().Name} - GameObject name is null or empty");
                return null;
            }

            var gameObject = McpToolUtilities.FindGameObject(objectName, inPrefabMode);
            
            if (gameObject == null)
            {
                var context = inPrefabMode ? "Prefab mode" : "active scene";
                Debug.LogWarning($"{GetType().Name} - GameObject '{objectName}' not found in {context}");
            }
            
            return gameObject;
        }

        /// <summary>
        /// Executes an operation that returns a string result with full error handling and validation
        /// </summary>
        /// <param name="operation">The operation to execute</param>
        /// <param name="operationName">Name of the operation for logging</param>
        /// <param name="inPrefabMode">Whether operation requires prefab mode validation</param>
        /// <returns>Success message or error message</returns>
        protected async ValueTask<string> ExecuteOperation(Func<ValueTask<string>> operation, string operationName, bool inPrefabMode = false)
        {
            return await ExecuteWithErrorHandling(async () =>
            {
                // Validate prefab mode if required
                if (inPrefabMode)
                {
                    var validationError = await ValidatePrefabMode(inPrefabMode);
                    if (validationError != null)
                    {
                        return validationError;
                    }
                }

                // Execute the operation
                var result = await operation();
                
                // Mark scene dirty after successful operation
                MarkSceneDirty(inPrefabMode);
                
                return result;
                
            }, operationName);
        }

        /// <summary>
        /// Gets the current prefab stage for prefab mode operations
        /// </summary>
        /// <returns>The current prefab stage or null if not in prefab mode</returns>
        protected PrefabStage GetCurrentPrefabStage()
        {
            return PrefabStageUtility.GetCurrentPrefabStage();
        }

        /// <summary>
        /// Gets the root GameObject based on context (prefab root or scene root)
        /// </summary>
        /// <param name="inPrefabMode">Whether to get prefab root or scene root</param>
        /// <returns>The appropriate root GameObject</returns>
        protected GameObject GetContextRoot(bool inPrefabMode)
        {
            if (inPrefabMode)
            {
                var prefabStage = GetCurrentPrefabStage();
                return prefabStage?.prefabContentsRoot;
            }
            
            return null; // For scene mode, there's no single root
        }

        /// <summary>
        /// Logs a success message with consistent formatting
        /// </summary>
        /// <param name="operation">The operation that succeeded</param>
        /// <param name="details">Additional details about the success</param>
        protected void LogSuccess(string operation, string details = null)
        {
            var message = $"{GetType().Name} - {operation} completed successfully";
            if (!string.IsNullOrEmpty(details))
            {
                message += $": {details}";
            }
            Debug.Log(message);
        }

        /// <summary>
        /// Logs a warning message with consistent formatting
        /// </summary>
        /// <param name="operation">The operation that generated the warning</param>
        /// <param name="warning">The warning message</param>
        protected void LogWarning(string operation, string warning)
        {
            Debug.LogWarning($"{GetType().Name} - {operation}: {warning}");
        }
    }
}