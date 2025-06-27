using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Unified MCP tool for particle system management in Unity
    /// </summary>
    [McpServerToolType, Description("Unified particle system management tools for Unity")]
    internal sealed class McpUnifiedEffectTool
    {
        [McpServerTool, Description("Configure particle system with comprehensive settings")]
        public async ValueTask<string> ConfigureParticleSystem(
            [Description("Name of the GameObject")]
            string objectName,
            [Description("Create new particle system if none exists")]
            bool createNew = false,
            [Description("Main module settings as JSON (optional)")]
            string mainSettings = null,
            [Description("Shape module settings as JSON (optional)")]
            string shapeSettings = null,
            [Description("Emission module settings as JSON (optional)")]
            string emissionSettings = null,
            [Description("Velocity over lifetime settings as JSON (optional)")]
            string velocitySettings = null,
            [Description("Name for new particle system (optional, defaults to 'Particle System')")]
            string particleSystemName = null)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var gameObject = GameObject.Find(objectName);
                if (gameObject == null)
                    return $"Error: GameObject '{objectName}' not found";

                var particleSystem = FindParticleSystem(objectName);

                if (particleSystem == null && createNew)
                {
                    // Create new particle system
                    var psGameObject = new GameObject(particleSystemName ?? "Particle System");
                    psGameObject.transform.SetParent(gameObject.transform);
                    psGameObject.transform.localPosition = Vector3.zero;
                    particleSystem = psGameObject.AddComponent<ParticleSystem>();

                    // Set basic defaults
                    var main = particleSystem.main;
                    main.playOnAwake = false;
                    main.loop = false;

                    // Assign appropriate material based on particle system type
                    AssignDefaultMaterial(particleSystem);
                }
                else if (particleSystem == null)
                {
                    return $"Error: No ParticleSystem found on GameObject '{objectName}'. Set createNew=true to create one.";
                }

                var changes = new List<string>();

                // Apply main settings
                if (!string.IsNullOrEmpty(mainSettings))
                {
                    try
                    {
                        var settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(mainSettings);
                        var main = particleSystem.main;

                        foreach (var setting in settings)
                        {
                            switch (setting.Key.ToLower())
                            {
                                case "duration":
                                    main.duration = Convert.ToSingle(setting.Value);
                                    changes.Add($"duration: {main.duration:F2}");
                                    break;
                                case "loop":
                                    main.loop = Convert.ToBoolean(setting.Value);
                                    changes.Add($"loop: {main.loop}");
                                    break;
                                case "playonawake":
                                    main.playOnAwake = Convert.ToBoolean(setting.Value);
                                    changes.Add($"playOnAwake: {main.playOnAwake}");
                                    break;
                                case "startlifetime":
                                    main.startLifetime = Convert.ToSingle(setting.Value);
                                    changes.Add($"startLifetime: {main.startLifetime.constant:F2}");
                                    break;
                                case "startspeed":
                                    main.startSpeed = Convert.ToSingle(setting.Value);
                                    changes.Add($"startSpeed: {main.startSpeed.constant:F2}");
                                    break;
                                case "startsize":
                                    main.startSize = Convert.ToSingle(setting.Value);
                                    changes.Add($"startSize: {main.startSize.constant:F2}");
                                    break;
                                case "startcolor":
                                    if (setting.Value is Newtonsoft.Json.Linq.JArray colorArray)
                                    {
                                        var values = colorArray.ToObject<float[]>();
                                        if (values.Length >= 3)
                                        {
                                            var color = new Color(values[0], values[1], values[2], values.Length > 3 ? values[3] : 1f);
                                            main.startColor = color;
                                            changes.Add($"startColor: ({color.r:F2}, {color.g:F2}, {color.b:F2}, {color.a:F2})");
                                        }
                                    }
                                    break;
                                case "maxparticles":
                                    main.maxParticles = Convert.ToInt32(setting.Value);
                                    changes.Add($"maxParticles: {main.maxParticles}");
                                    break;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        return "Error: Invalid JSON format for mainSettings";
                    }
                }

                // Apply shape settings
                if (!string.IsNullOrEmpty(shapeSettings))
                {
                    try
                    {
                        var settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(shapeSettings);
                        var shape = particleSystem.shape;

                        foreach (var setting in settings)
                        {
                            switch (setting.Key.ToLower())
                            {
                                case "shapetype":
                                    if (Enum.TryParse<ParticleSystemShapeType>(setting.Value.ToString(), true, out var shapeType))
                                    {
                                        shape.shapeType = shapeType;
                                        changes.Add($"shapeType: {shapeType}");
                                    }
                                    break;
                                case "radius":
                                    shape.radius = Convert.ToSingle(setting.Value);
                                    changes.Add($"radius: {shape.radius:F2}");
                                    break;
                                case "boxsize":
                                    if (setting.Value is Newtonsoft.Json.Linq.JArray boxArray)
                                    {
                                        var values = boxArray.ToObject<float[]>();
                                        if (values.Length >= 3)
                                        {
                                            shape.scale = new Vector3(values[0], values[1], values[2]);
                                            changes.Add($"boxSize: ({values[0]:F2}, {values[1]:F2}, {values[2]:F2})");
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        return "Error: Invalid JSON format for shapeSettings";
                    }
                }

                // Apply emission settings
                if (!string.IsNullOrEmpty(emissionSettings))
                {
                    try
                    {
                        var settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(emissionSettings);
                        var emission = particleSystem.emission;

                        foreach (var setting in settings)
                        {
                            switch (setting.Key.ToLower())
                            {
                                case "enabled":
                                    emission.enabled = Convert.ToBoolean(setting.Value);
                                    changes.Add($"emission.enabled: {emission.enabled}");
                                    break;
                                case "rateovertime":
                                    emission.rateOverTime = Convert.ToSingle(setting.Value);
                                    changes.Add($"rateOverTime: {emission.rateOverTime.constant:F2}");
                                    break;
                                case "burst":
                                    if (setting.Value is Newtonsoft.Json.Linq.JObject burstObj)
                                    {
                                        var count = burstObj["count"]?.ToObject<int>() ?? 10;
                                        var time = burstObj["time"]?.ToObject<float>() ?? 0f;
                                        var burst = new ParticleSystem.Burst(time, count);
                                        emission.SetBursts(new ParticleSystem.Burst[] { burst });
                                        changes.Add($"burst: {count} at time {time:F2}");
                                    }
                                    break;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        return "Error: Invalid JSON format for emissionSettings";
                    }
                }

                // Apply velocity settings
                if (!string.IsNullOrEmpty(velocitySettings))
                {
                    try
                    {
                        var settings = JsonConvert.DeserializeObject<Dictionary<string, object>>(velocitySettings);
                        var velocity = particleSystem.velocityOverLifetime;

                        foreach (var setting in settings)
                        {
                            switch (setting.Key.ToLower())
                            {
                                case "enabled":
                                    velocity.enabled = Convert.ToBoolean(setting.Value);
                                    changes.Add($"velocity.enabled: {velocity.enabled}");
                                    break;
                                case "linear":
                                    if (setting.Value is Newtonsoft.Json.Linq.JArray linearArray)
                                    {
                                        var values = linearArray.ToObject<float[]>();
                                        if (values.Length >= 3)
                                        {
                                            velocity.x = values[0];
                                            velocity.y = values[1];
                                            velocity.z = values[2];
                                            changes.Add($"linear: ({values[0]:F2}, {values[1]:F2}, {values[2]:F2})");
                                        }
                                    }
                                    break;
                                case "random":
                                    if (setting.Value is Newtonsoft.Json.Linq.JArray randomArray)
                                    {
                                        var values = randomArray.ToObject<float[]>();
                                        if (values.Length >= 3)
                                        {
                                            velocity.x = new ParticleSystem.MinMaxCurve(velocity.x.constant - values[0], velocity.x.constant + values[0]);
                                            velocity.y = new ParticleSystem.MinMaxCurve(velocity.y.constant - values[1], velocity.y.constant + values[1]);
                                            velocity.z = new ParticleSystem.MinMaxCurve(velocity.z.constant - values[2], velocity.z.constant + values[2]);
                                            changes.Add($"random: ({values[0]:F2}, {values[1]:F2}, {values[2]:F2})");
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        return "Error: Invalid JSON format for velocitySettings";
                    }
                }

                EditorUtility.SetDirty(particleSystem);

                if (changes.Count == 0)
                    return createNew
                        ? $"Created ParticleSystem on '{objectName}' with default settings"
                        : $"No changes applied to ParticleSystem on '{objectName}'";

                return $"Successfully configured ParticleSystem on '{objectName}': {string.Join(", ", changes)}";
            }
            catch (Exception e)
            {
                Debug.LogError($"Error configuring particle system: {e}");
                return $"Error configuring particle system: {e.Message}";
            }
        }

        [McpServerTool, Description("Control particle system playback")]
        public async ValueTask<string> ControlParticleSystem(
            [Description("Name of the GameObject containing the particle system")]
            string objectName,
            [Description("Action to perform: 'play' or 'stop'")]
            string action)
        {
            try
            {
                await UniTask.SwitchToMainThread();

                var particleSystem = FindParticleSystem(objectName);
                if (particleSystem == null)
                    return $"Error: No ParticleSystem found on GameObject '{objectName}'";

                switch (action?.ToLower())
                {
                    case "play":
                        particleSystem.Play();
                        return $"Successfully started ParticleSystem on '{objectName}'";

                    case "stop":
                        particleSystem.Stop();
                        return $"Successfully stopped ParticleSystem on '{objectName}'";

                    default:
                        return "Error: action must be 'play' or 'stop'";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error controlling particle system: {e}");
                return $"Error controlling particle system: {e.Message}";
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

        private void AssignDefaultMaterial(ParticleSystem particleSystem)
        {
            try
            {
                var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                if (renderer == null)
                    return;

                // Check if material is already assigned
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.name != "Default-Material")
                    return;

                // Try to find appropriate default materials in the project
                Material defaultMaterial = null;

                // First, try to find a specific particle material
                var particleMaterialGUIDs = AssetDatabase.FindAssets("t:Material Particle", new[] { "Assets" });
                if (particleMaterialGUIDs.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(particleMaterialGUIDs[0]);
                    defaultMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                }

                // If no particle material found, try Default-Particle material
                if (defaultMaterial == null)
                {
                    defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
                }

                // If still no material, try Sprites-Default material which works well for particles
                if (defaultMaterial == null)
                {
                    defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                }

                // As a last resort, create a simple unlit material
                if (defaultMaterial == null)
                {
                    defaultMaterial = new Material(Shader.Find("Sprites/Default"));
                    defaultMaterial.name = "Particle Default Material";

                    // Save it as an asset
                    if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                    {
                        AssetDatabase.CreateFolder("Assets", "Materials");
                    }
                    AssetDatabase.CreateAsset(defaultMaterial, "Assets/Materials/ParticleDefaultMaterial.mat");
                }

                if (defaultMaterial != null)
                {
                    renderer.sharedMaterial = defaultMaterial;
                    EditorUtility.SetDirty(renderer);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to assign default material to particle system: {e.Message}");
            }
        }
    }
}