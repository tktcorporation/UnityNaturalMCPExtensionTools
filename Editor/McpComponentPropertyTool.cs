using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;

namespace Editor.McpTools
{
    /// <summary>
    /// MCP tool for setting detailed properties on Unity components
    /// </summary>
    [McpServerToolType, Description("Component property manipulation tools for Unity")]
    internal sealed class McpComponentPropertyTool
    {
        [McpServerTool, Description("Set properties on a Collider component")]
        public async ValueTask<string> SetColliderProperties(
            [Description("Name of the GameObject")]
            string objectName,
            [Description("Set as trigger collider (optional)")]
            bool? isTrigger = null,
            [Description("Center offset (x,y,z) for BoxCollider/SphereCollider (optional)")]
            float[] center = null,
            [Description("Size (x,y,z) for BoxCollider (optional)")]
            float[] size = null,
            [Description("Radius for SphereCollider/CapsuleCollider (optional)")]
            float? radius = null,
            [Description("Height for CapsuleCollider (optional)")]
            float? height = null,
            [Description("Direction for CapsuleCollider (0=X, 1=Y, 2=Z) (optional)")]
            int? direction = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found";
                }

                var collider = gameObject.GetComponent<Collider>();
                if (collider == null)
                {
                    return $"Error: GameObject '{objectName}' does not have a Collider component";
                }

                var changes = new System.Collections.Generic.List<string>();

                // Set trigger
                if (isTrigger.HasValue)
                {
                    collider.isTrigger = isTrigger.Value;
                    changes.Add($"isTrigger: {isTrigger.Value}");
                }

                // Handle different collider types
                if (collider is BoxCollider boxCollider)
                {
                    if (center != null && center.Length >= 3)
                    {
                        boxCollider.center = new Vector3(center[0], center[1], center[2]);
                        changes.Add($"center: ({center[0]:F2}, {center[1]:F2}, {center[2]:F2})");
                    }
                    if (size != null && size.Length >= 3)
                    {
                        boxCollider.size = new Vector3(size[0], size[1], size[2]);
                        changes.Add($"size: ({size[0]:F2}, {size[1]:F2}, {size[2]:F2})");
                    }
                }
                else if (collider is SphereCollider sphereCollider)
                {
                    if (center != null && center.Length >= 3)
                    {
                        sphereCollider.center = new Vector3(center[0], center[1], center[2]);
                        changes.Add($"center: ({center[0]:F2}, {center[1]:F2}, {center[2]:F2})");
                    }
                    if (radius.HasValue)
                    {
                        sphereCollider.radius = radius.Value;
                        changes.Add($"radius: {radius.Value:F2}");
                    }
                }
                else if (collider is CapsuleCollider capsuleCollider)
                {
                    if (center != null && center.Length >= 3)
                    {
                        capsuleCollider.center = new Vector3(center[0], center[1], center[2]);
                        changes.Add($"center: ({center[0]:F2}, {center[1]:F2}, {center[2]:F2})");
                    }
                    if (radius.HasValue)
                    {
                        capsuleCollider.radius = radius.Value;
                        changes.Add($"radius: {radius.Value:F2}");
                    }
                    if (height.HasValue)
                    {
                        capsuleCollider.height = height.Value;
                        changes.Add($"height: {height.Value:F2}");
                    }
                    if (direction.HasValue && direction.Value >= 0 && direction.Value <= 2)
                    {
                        capsuleCollider.direction = direction.Value;
                        changes.Add($"direction: {direction.Value}");
                    }
                }

                EditorUtility.SetDirty(gameObject);

                if (changes.Count == 0)
                {
                    return $"No changes applied to {collider.GetType().Name} on '{objectName}'";
                }

                return $"Successfully updated {collider.GetType().Name} on '{objectName}': {string.Join(", ", changes)}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting collider properties: {e}");
                return $"Error setting collider properties: {e.Message}";
            }
        }

        [McpServerTool, Description("Set properties on an AudioSource component")]
        public async ValueTask<string> SetAudioSourceProperties(
            [Description("Name of the GameObject")]
            string objectName,
            [Description("Play audio on awake (optional)")]
            bool? playOnAwake = null,
            [Description("Loop audio (optional)")]
            bool? loop = null,
            [Description("Volume (0-1) (optional)")]
            float? volume = null,
            [Description("Pitch (0.1-3) (optional)")]
            float? pitch = null,
            [Description("Spatial blend (0=2D, 1=3D) (optional)")]
            float? spatialBlend = null,
            [Description("Min distance for 3D audio (optional)")]
            float? minDistance = null,
            [Description("Max distance for 3D audio (optional)")]
            float? maxDistance = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found";
                }

                var audioSource = gameObject.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    return $"Error: GameObject '{objectName}' does not have an AudioSource component";
                }

                var changes = new System.Collections.Generic.List<string>();

                if (playOnAwake.HasValue)
                {
                    audioSource.playOnAwake = playOnAwake.Value;
                    changes.Add($"playOnAwake: {playOnAwake.Value}");
                }

                if (loop.HasValue)
                {
                    audioSource.loop = loop.Value;
                    changes.Add($"loop: {loop.Value}");
                }

                if (volume.HasValue)
                {
                    audioSource.volume = Mathf.Clamp01(volume.Value);
                    changes.Add($"volume: {audioSource.volume:F2}");
                }

                if (pitch.HasValue)
                {
                    audioSource.pitch = Mathf.Clamp(pitch.Value, 0.1f, 3f);
                    changes.Add($"pitch: {audioSource.pitch:F2}");
                }

                if (spatialBlend.HasValue)
                {
                    audioSource.spatialBlend = Mathf.Clamp01(spatialBlend.Value);
                    changes.Add($"spatialBlend: {audioSource.spatialBlend:F2}");
                }

                if (minDistance.HasValue)
                {
                    audioSource.minDistance = Mathf.Max(0f, minDistance.Value);
                    changes.Add($"minDistance: {audioSource.minDistance:F2}");
                }

                if (maxDistance.HasValue)
                {
                    audioSource.maxDistance = Mathf.Max(audioSource.minDistance, maxDistance.Value);
                    changes.Add($"maxDistance: {audioSource.maxDistance:F2}");
                }

                EditorUtility.SetDirty(gameObject);

                if (changes.Count == 0)
                {
                    return $"No changes applied to AudioSource on '{objectName}'";
                }

                return $"Successfully updated AudioSource on '{objectName}': {string.Join(", ", changes)}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting audio source properties: {e}");
                return $"Error setting audio source properties: {e.Message}";
            }
        }

        [McpServerTool, Description("Set properties on a Renderer component")]
        public async ValueTask<string> SetRendererProperties(
            [Description("Name of the GameObject")]
            string objectName,
            [Description("Cast shadows (optional)")]
            bool? castShadows = null,
            [Description("Receive shadows (optional)")]
            bool? receiveShadows = null,
            [Description("Enabled (optional)")]
            bool? enabled = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found";
                }

                var renderer = gameObject.GetComponent<Renderer>();
                if (renderer == null)
                {
                    return $"Error: GameObject '{objectName}' does not have a Renderer component";
                }

                var changes = new System.Collections.Generic.List<string>();

                if (castShadows.HasValue)
                {
                    renderer.shadowCastingMode = castShadows.Value 
                        ? UnityEngine.Rendering.ShadowCastingMode.On 
                        : UnityEngine.Rendering.ShadowCastingMode.Off;
                    changes.Add($"castShadows: {castShadows.Value}");
                }

                if (receiveShadows.HasValue)
                {
                    renderer.receiveShadows = receiveShadows.Value;
                    changes.Add($"receiveShadows: {receiveShadows.Value}");
                }

                if (enabled.HasValue)
                {
                    renderer.enabled = enabled.Value;
                    changes.Add($"enabled: {enabled.Value}");
                }

                EditorUtility.SetDirty(gameObject);

                if (changes.Count == 0)
                {
                    return $"No changes applied to {renderer.GetType().Name} on '{objectName}'";
                }

                return $"Successfully updated {renderer.GetType().Name} on '{objectName}': {string.Join(", ", changes)}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting renderer properties: {e}");
                return $"Error setting renderer properties: {e.Message}";
            }
        }

        [McpServerTool, Description("Set transform properties on a GameObject")]
        public async ValueTask<string> SetTransformProperties(
            [Description("Name of the GameObject")]
            string objectName,
            [Description("World position (x,y,z) (optional)")]
            float[] position = null,
            [Description("Euler rotation (x,y,z) (optional)")]
            float[] rotation = null,
            [Description("Local scale (x,y,z) (optional)")]
            float[] scale = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found";
                }

                var changes = new System.Collections.Generic.List<string>();

                if (position != null && position.Length >= 3)
                {
                    gameObject.transform.position = new Vector3(position[0], position[1], position[2]);
                    changes.Add($"position: ({position[0]:F2}, {position[1]:F2}, {position[2]:F2})");
                }

                if (rotation != null && rotation.Length >= 3)
                {
                    gameObject.transform.eulerAngles = new Vector3(rotation[0], rotation[1], rotation[2]);
                    changes.Add($"rotation: ({rotation[0]:F2}, {rotation[1]:F2}, {rotation[2]:F2})");
                }

                if (scale != null && scale.Length >= 3)
                {
                    gameObject.transform.localScale = new Vector3(scale[0], scale[1], scale[2]);
                    changes.Add($"scale: ({scale[0]:F2}, {scale[1]:F2}, {scale[2]:F2})");
                }

                EditorUtility.SetDirty(gameObject);

                if (changes.Count == 0)
                {
                    return $"No changes applied to Transform on '{objectName}'";
                }

                return $"Successfully updated Transform on '{objectName}': {string.Join(", ", changes)}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting transform properties: {e}");
                return $"Error setting transform properties: {e.Message}";
            }
        }

        [McpServerTool, Description("Set parent-child relationship between GameObjects")]
        public async ValueTask<string> SetParentChild(
            [Description("Name of the child GameObject")]
            string childObjectName,
            [Description("Name of the parent GameObject (null to unparent)")]
            string parentObjectName = null,
            [Description("Keep world position when parenting (default: true)")]
            bool worldPositionStays = true)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var childObject = GameObject.Find(childObjectName);
                if (childObject == null)
                {
                    return $"Error: Child GameObject '{childObjectName}' not found";
                }

                Transform parentTransform = null;
                if (!string.IsNullOrEmpty(parentObjectName))
                {
                    var parentObject = GameObject.Find(parentObjectName);
                    if (parentObject == null)
                    {
                        return $"Error: Parent GameObject '{parentObjectName}' not found";
                    }
                    parentTransform = parentObject.transform;
                }

                var oldParent = childObject.transform.parent;
                childObject.transform.SetParent(parentTransform, worldPositionStays);

                EditorUtility.SetDirty(childObject);

                var parentInfo = parentTransform != null ? $"'{parentTransform.name}'" : "root";
                var oldParentInfo = oldParent != null ? $"'{oldParent.name}'" : "root";

                return $"Successfully moved '{childObjectName}' from {oldParentInfo} to {parentInfo} (worldPositionStays: {worldPositionStays})";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting parent-child relationship: {e}");
                return $"Error setting parent-child relationship: {e.Message}";
            }
        }
    }
}