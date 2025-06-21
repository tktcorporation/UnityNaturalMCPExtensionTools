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
    /// MCP tool for creating and manipulating particle systems in Unity
    /// </summary>
    [McpServerToolType, Description("Particle system creation and manipulation tools for Unity")]
    internal sealed class McpParticleTool
    {
        [McpServerTool, Description("Create a particle system on a GameObject")]
        public async ValueTask<string> CreateParticleSystem(
            [Description("Name of the GameObject to add particle system to")]
            string objectName,
            [Description("Name for the particle system (optional, defaults to 'Particle System')")]
            string particleSystemName = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                {
                    return $"Error: GameObject '{objectName}' not found";
                }

                // Check if particle system already exists
                var existingParticleSystem = gameObject.GetComponentInChildren<ParticleSystem>();
                if (existingParticleSystem != null)
                {
                    return $"Warning: GameObject '{objectName}' already has a ParticleSystem component";
                }

                // Create particle system as child object
                var particleSystemGameObject = new GameObject(particleSystemName ?? "Particle System");
                particleSystemGameObject.transform.SetParent(gameObject.transform);
                particleSystemGameObject.transform.localPosition = Vector3.zero;

                var particleSystem = particleSystemGameObject.AddComponent<ParticleSystem>();

                // Set some basic defaults
                var main = particleSystem.main;
                main.playOnAwake = false;
                main.loop = false;

                EditorUtility.SetDirty(gameObject);
                EditorUtility.SetDirty(particleSystemGameObject);

                return $"Successfully created ParticleSystem '{particleSystemGameObject.name}' on GameObject '{objectName}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error creating particle system: {e}");
                return $"Error creating particle system: {e.Message}";
            }
        }

        [McpServerTool, Description("Set main properties of a particle system")]
        public async ValueTask<string> SetParticleSystemMain(
            [Description("Name of the GameObject containing the particle system")]
            string objectName,
            [Description("Duration of the particle system (optional)")]
            float? duration = null,
            [Description("Loop the particle system (optional)")]
            bool? loop = null,
            [Description("Play on awake (optional)")]
            bool? playOnAwake = null,
            [Description("Start lifetime (optional)")]
            float? startLifetime = null,
            [Description("Start speed (optional)")]
            float? startSpeed = null,
            [Description("Start size (optional)")]
            float? startSize = null,
            [Description("Start color as array [R, G, B, A] (optional)")]
            float[] startColor = null,
            [Description("Max particles (optional)")]
            int? maxParticles = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var particleSystem = FindParticleSystem(objectName);
                if (particleSystem == null)
                {
                    return $"Error: No ParticleSystem found on GameObject '{objectName}'";
                }

                var main = particleSystem.main;
                var changes = new System.Collections.Generic.List<string>();

                if (duration.HasValue)
                {
                    main.duration = duration.Value;
                    changes.Add($"duration: {duration.Value:F2}");
                }

                if (loop.HasValue)
                {
                    main.loop = loop.Value;
                    changes.Add($"loop: {loop.Value}");
                }

                if (playOnAwake.HasValue)
                {
                    main.playOnAwake = playOnAwake.Value;
                    changes.Add($"playOnAwake: {playOnAwake.Value}");
                }

                if (startLifetime.HasValue)
                {
                    main.startLifetime = startLifetime.Value;
                    changes.Add($"startLifetime: {startLifetime.Value:F2}");
                }

                if (startSpeed.HasValue)
                {
                    main.startSpeed = startSpeed.Value;
                    changes.Add($"startSpeed: {startSpeed.Value:F2}");
                }

                if (startSize.HasValue)
                {
                    main.startSize = startSize.Value;
                    changes.Add($"startSize: {startSize.Value:F2}");
                }

                if (startColor != null && startColor.Length >= 3)
                {
                    var color = new Color(
                        startColor[0],
                        startColor[1],
                        startColor[2],
                        startColor.Length > 3 ? startColor[3] : 1.0f
                    );
                    main.startColor = color;
                    changes.Add($"startColor: ({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})");
                }

                if (maxParticles.HasValue)
                {
                    main.maxParticles = maxParticles.Value;
                    changes.Add($"maxParticles: {maxParticles.Value}");
                }

                EditorUtility.SetDirty(particleSystem);

                if (changes.Count == 0)
                {
                    return $"No changes applied to ParticleSystem main module on '{objectName}'";
                }

                return $"Successfully updated ParticleSystem main module on '{objectName}': {string.Join(", ", changes)}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting particle system main properties: {e}");
                return $"Error setting particle system main properties: {e.Message}";
            }
        }

        [McpServerTool, Description("Set emission properties of a particle system")]
        public async ValueTask<string> SetParticleSystemEmission(
            [Description("Name of the GameObject containing the particle system")]
            string objectName,
            [Description("Enable emission (optional)")]
            bool? enabled = null,
            [Description("Rate over time (optional)")]
            float? rateOverTime = null,
            [Description("Burst count (optional)")]
            int? burstCount = null,
            [Description("Burst time (optional)")]
            float? burstTime = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var particleSystem = FindParticleSystem(objectName);
                if (particleSystem == null)
                {
                    return $"Error: No ParticleSystem found on GameObject '{objectName}'";
                }

                var emission = particleSystem.emission;
                var changes = new System.Collections.Generic.List<string>();

                if (enabled.HasValue)
                {
                    emission.enabled = enabled.Value;
                    changes.Add($"enabled: {enabled.Value}");
                }

                if (rateOverTime.HasValue)
                {
                    emission.rateOverTime = rateOverTime.Value;
                    changes.Add($"rateOverTime: {rateOverTime.Value:F2}");
                }

                if (burstCount.HasValue && burstTime.HasValue)
                {
                    var burst = new ParticleSystem.Burst(burstTime.Value, burstCount.Value);
                    emission.SetBursts(new ParticleSystem.Burst[] { burst });
                    changes.Add($"burst: {burstCount.Value} at time {burstTime.Value:F2}");
                }

                EditorUtility.SetDirty(particleSystem);

                if (changes.Count == 0)
                {
                    return $"No changes applied to ParticleSystem emission module on '{objectName}'";
                }

                return $"Successfully updated ParticleSystem emission module on '{objectName}': {string.Join(", ", changes)}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting particle system emission properties: {e}");
                return $"Error setting particle system emission properties: {e.Message}";
            }
        }

        [McpServerTool, Description("Set shape properties of a particle system")]
        public async ValueTask<string> SetParticleSystemShape(
            [Description("Name of the GameObject containing the particle system")]
            string objectName,
            [Description("Shape type (Circle, Sphere, Box, etc.) (optional)")]
            string shapeType = null,
            [Description("Radius for Circle/Sphere (optional)")]
            float? radius = null,
            [Description("Box size as array [X, Y, Z] (optional)")]
            float[] boxSize = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var particleSystem = FindParticleSystem(objectName);
                if (particleSystem == null)
                {
                    return $"Error: No ParticleSystem found on GameObject '{objectName}'";
                }

                var shape = particleSystem.shape;
                var changes = new System.Collections.Generic.List<string>();

                if (!string.IsNullOrEmpty(shapeType))
                {
                    if (Enum.TryParse<ParticleSystemShapeType>(shapeType, true, out var parsedShapeType))
                    {
                        shape.shapeType = parsedShapeType;
                        changes.Add($"shapeType: {parsedShapeType}");
                    }
                    else
                    {
                        return $"Error: Invalid shape type '{shapeType}'. Valid types include: Circle, Sphere, Box, Cone, etc.";
                    }
                }

                if (radius.HasValue)
                {
                    shape.radius = radius.Value;
                    changes.Add($"radius: {radius.Value:F2}");
                }

                if (boxSize != null && boxSize.Length >= 3)
                {
                    shape.scale = new Vector3(boxSize[0], boxSize[1], boxSize[2]);
                    changes.Add($"boxSize: ({boxSize[0]:F2}, {boxSize[1]:F2}, {boxSize[2]:F2})");
                }

                EditorUtility.SetDirty(particleSystem);

                if (changes.Count == 0)
                {
                    return $"No changes applied to ParticleSystem shape module on '{objectName}'";
                }

                return $"Successfully updated ParticleSystem shape module on '{objectName}': {string.Join(", ", changes)}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting particle system shape properties: {e}");
                return $"Error setting particle system shape properties: {e.Message}";
            }
        }

        [McpServerTool, Description("Set velocity over lifetime properties of a particle system")]
        public async ValueTask<string> SetParticleSystemVelocity(
            [Description("Name of the GameObject containing the particle system")]
            string objectName,
            [Description("Enable velocity over lifetime (optional)")]
            bool? enabled = null,
            [Description("Linear velocity as array [X, Y, Z] (optional)")]
            float[] linear = null,
            [Description("Random velocity as array [X, Y, Z] (optional)")]
            float[] random = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var particleSystem = FindParticleSystem(objectName);
                if (particleSystem == null)
                {
                    return $"Error: No ParticleSystem found on GameObject '{objectName}'";
                }

                var velocity = particleSystem.velocityOverLifetime;
                var changes = new System.Collections.Generic.List<string>();

                if (enabled.HasValue)
                {
                    velocity.enabled = enabled.Value;
                    changes.Add($"enabled: {enabled.Value}");
                }

                if (linear != null && linear.Length >= 3)
                {
                    velocity.x = linear[0];
                    velocity.y = linear[1];
                    velocity.z = linear[2];
                    changes.Add($"linear: ({linear[0]:F2}, {linear[1]:F2}, {linear[2]:F2})");
                }

                if (random != null && random.Length >= 3)
                {
                    velocity.x = new ParticleSystem.MinMaxCurve(velocity.x.constant - random[0], velocity.x.constant + random[0]);
                    velocity.y = new ParticleSystem.MinMaxCurve(velocity.y.constant - random[1], velocity.y.constant + random[1]);
                    velocity.z = new ParticleSystem.MinMaxCurve(velocity.z.constant - random[2], velocity.z.constant + random[2]);
                    changes.Add($"random: ({random[0]:F2}, {random[1]:F2}, {random[2]:F2})");
                }

                EditorUtility.SetDirty(particleSystem);

                if (changes.Count == 0)
                {
                    return $"No changes applied to ParticleSystem velocity module on '{objectName}'";
                }

                return $"Successfully updated ParticleSystem velocity module on '{objectName}': {string.Join(", ", changes)}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting particle system velocity properties: {e}");
                return $"Error setting particle system velocity properties: {e.Message}";
            }
        }

        [McpServerTool, Description("Play a particle system")]
        public async ValueTask<string> PlayParticleSystem(
            [Description("Name of the GameObject containing the particle system")]
            string objectName)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var particleSystem = FindParticleSystem(objectName);
                if (particleSystem == null)
                {
                    return $"Error: No ParticleSystem found on GameObject '{objectName}'";
                }

                particleSystem.Play();

                return $"Successfully started ParticleSystem on '{objectName}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error playing particle system: {e}");
                return $"Error playing particle system: {e.Message}";
            }
        }

        [McpServerTool, Description("Stop a particle system")]
        public async ValueTask<string> StopParticleSystem(
            [Description("Name of the GameObject containing the particle system")]
            string objectName)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var particleSystem = FindParticleSystem(objectName);
                if (particleSystem == null)
                {
                    return $"Error: No ParticleSystem found on GameObject '{objectName}'";
                }

                particleSystem.Stop();

                return $"Successfully stopped ParticleSystem on '{objectName}'";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error stopping particle system: {e}");
                return $"Error stopping particle system: {e.Message}";
            }
        }

        private ParticleSystem FindParticleSystem(string objectName)
        {
            var gameObject = GameObject.Find(objectName);
            if (gameObject == null)
                return null;

            // First check the object itself
            var particleSystem = gameObject.GetComponent<ParticleSystem>();
            if (particleSystem != null)
                return particleSystem;

            // Then check children
            return gameObject.GetComponentInChildren<ParticleSystem>();
        }
    }
}