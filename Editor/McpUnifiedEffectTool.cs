using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ModelContextProtocol.Server;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor.SceneManagement;

namespace UnityNaturalMCPExtension.Editor
{
    /// <summary>
    /// Unified MCP tool for particle system management in Unity
    /// </summary>
    [McpServerToolType, Description("Unified particle system management tools for Unity")]
    internal sealed class McpUnifiedEffectTool : McpToolBase
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
            string particleSystemName = null,
            [Description("Configure in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            return await ExecuteOperation(async () =>
            {
                await ValidatePrefabMode(inPrefabMode);

                var gameObject = await FindGameObjectSafe(objectName, inPrefabMode);
                var particleSystem = FindParticleSystem(objectName, inPrefabMode);

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

                // Stop particle system if it's playing to avoid errors when changing certain properties
                bool wasPlaying = particleSystem.isPlaying;
                if (wasPlaying)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

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
                MarkSceneDirty(inPrefabMode);

                // Restart particle system if it was playing
                if (wasPlaying)
                {
                    particleSystem.Play();
                }

                if (changes.Count == 0)
                    return createNew
                        ? McpToolUtilities.CreateSuccessMessage($"Created ParticleSystem on '{objectName}' with default settings")
                        : McpToolUtilities.CreateSuccessMessage($"No changes applied to ParticleSystem on '{objectName}'");

                return McpToolUtilities.CreateSuccessMessage($"Configured ParticleSystem on '{objectName}': {string.Join(", ", changes)}");
            }, "configuring particle system");
        }

        [McpServerTool, Description("Control particle system playback")]
        public async ValueTask<string> ControlParticleSystem(
            [Description("Name of the GameObject containing the particle system")]
            string objectName,
            [Description("Action to perform: 'play' or 'stop'")]
            string action,
            [Description("Control in Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            return await ExecuteOperation(async () =>
            {
                await ValidatePrefabMode(inPrefabMode);

                var particleSystem = FindParticleSystem(objectName, inPrefabMode);
                if (particleSystem == null)
                    return McpToolUtilities.CreateErrorMessage($"No ParticleSystem found on GameObject '{objectName}'{McpToolUtilities.GetContextDescription(inPrefabMode)}");

                switch (action?.ToLower())
                {
                    case "play":
                        particleSystem.Play();
                        return McpToolUtilities.CreateSuccessMessage($"Started ParticleSystem on '{objectName}'");

                    case "stop":
                        particleSystem.Stop();
                        return McpToolUtilities.CreateSuccessMessage($"Stopped ParticleSystem on '{objectName}'");

                    default:
                        return McpToolUtilities.CreateErrorMessage("action must be 'play' or 'stop'");
                }
            }, "controlling particle system");
        }

        [McpServerTool, Description("Get detailed information about a particle system")]
        public async ValueTask<string> GetParticleSystemInfo(
            [Description("Name of the GameObject containing the particle system")]
            string objectName,
            [Description("Include detailed module information (optional, default: true)")]
            bool includeModuleDetails = true,
            [Description("Get info from Prefab mode context instead of scene (optional, default: false)")]
            bool inPrefabMode = false)
        {
            return await ExecuteOperation(async () =>
            {
                await ValidatePrefabMode(inPrefabMode);

                var particleSystem = FindParticleSystem(objectName, inPrefabMode);
                if (particleSystem == null)
                    return McpToolUtilities.CreateErrorMessage($"No ParticleSystem found on GameObject '{objectName}'{McpToolUtilities.GetContextDescription(inPrefabMode)}");

                var info = GetBasicParticleSystemInfo(particleSystem, objectName);

                if (includeModuleDetails)
                {
                    AddModuleDetails(info, particleSystem);
                }

                return JsonConvert.SerializeObject(info, Formatting.Indented);
            }, "getting particle system info");
        }

        private Dictionary<string, object> GetBasicParticleSystemInfo(ParticleSystem particleSystem, string objectName)
        {
            return new Dictionary<string, object>
            {
                ["objectName"] = objectName,
                ["particleCount"] = particleSystem.particleCount,
                ["isPlaying"] = particleSystem.isPlaying,
                ["isPaused"] = particleSystem.isPaused,
                ["isStopped"] = particleSystem.isStopped,
                ["isEmitting"] = particleSystem.isEmitting,
                ["time"] = particleSystem.time
            };
        }

        private void AddModuleDetails(Dictionary<string, object> info, ParticleSystem particleSystem)
        {
            info["mainModule"] = GetMainModuleInfo(particleSystem.main);

            info["shapeModule"] = GetShapeModuleInfo(particleSystem.shape);

            info["emissionModule"] = GetEmissionModuleInfo(particleSystem.emission);

            info["velocityOverLifetimeModule"] = GetVelocityOverLifetimeModuleInfo(particleSystem.velocityOverLifetime);

            info["colorOverLifetimeModule"] = GetColorOverLifetimeModuleInfo(particleSystem.colorOverLifetime);
            info["sizeOverLifetimeModule"] = GetSizeOverLifetimeModuleInfo(particleSystem.sizeOverLifetime);
            info["rotationOverLifetimeModule"] = GetRotationOverLifetimeModuleInfo(particleSystem.rotationOverLifetime);
            info["forceOverLifetimeModule"] = GetForceOverLifetimeModuleInfo(particleSystem.forceOverLifetime);

            info["noiseModule"] = GetNoiseModuleInfo(particleSystem.noise);

            info["externalForcesModule"] = GetExternalForcesModuleInfo(particleSystem.externalForces);

            info["collisionModule"] = GetCollisionModuleInfo(particleSystem.collision);

            info["subEmittersModule"] = GetSubEmittersModuleInfo(particleSystem.subEmitters);
            info["textureSheetAnimationModule"] = GetTextureSheetAnimationModuleInfo(particleSystem.textureSheetAnimation);
            info["lightsModule"] = GetLightsModuleInfo(particleSystem.lights);
            info["trailsModule"] = GetTrailsModuleInfo(particleSystem.trails);
            info["customDataModule"] = GetCustomDataModuleInfo(particleSystem.customData);

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                info["renderer"] = GetRendererInfo(renderer);
            }
        }

        private object GetMinMaxCurveValue(ParticleSystem.MinMaxCurve curve)
        {
            var result = new Dictionary<string, object>
            {
                ["mode"] = curve.mode.ToString()
            };

            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    result["constant"] = curve.constant;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    result["constantMin"] = curve.constantMin;
                    result["constantMax"] = curve.constantMax;
                    break;
                case ParticleSystemCurveMode.Curve:
                    result["curveMultiplier"] = curve.curveMultiplier;
                    result["curveKeys"] = curve.curve?.keys?.Length ?? 0;
                    break;
                case ParticleSystemCurveMode.TwoCurves:
                    result["curveMultiplier"] = curve.curveMultiplier;
                    result["curveMinKeys"] = curve.curveMin?.keys?.Length ?? 0;
                    result["curveMaxKeys"] = curve.curveMax?.keys?.Length ?? 0;
                    break;
            }

            return result;
        }

        private object GetMinMaxGradientValue(ParticleSystem.MinMaxGradient gradient)
        {
            var result = new Dictionary<string, object>
            {
                ["mode"] = gradient.mode.ToString()
            };

            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Color:
                    var color = gradient.color;
                    result["color"] = new float[] { color.r, color.g, color.b, color.a };
                    break;
                case ParticleSystemGradientMode.TwoColors:
                    var minColor = gradient.colorMin;
                    var maxColor = gradient.colorMax;
                    result["colorMin"] = new float[] { minColor.r, minColor.g, minColor.b, minColor.a };
                    result["colorMax"] = new float[] { maxColor.r, maxColor.g, maxColor.b, maxColor.a };
                    break;
                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.TwoGradients:
                    result["gradientKeys"] = gradient.gradient?.colorKeys?.Length ?? 0;
                    break;
                case ParticleSystemGradientMode.RandomColor:
                    result["randomColor"] = true;
                    break;
            }

            return result;
        }

        private object Vector3ToArray(Vector3 v)
        {
            return new float[] { v.x, v.y, v.z };
        }

        private object Vector2ToArray(Vector2 v)
        {
            return new float[] { v.x, v.y };
        }

        private ParticleSystem FindParticleSystem(string objectName, bool inPrefabMode = false)
        {
            GameObject gameObject = McpToolUtilities.FindGameObject(objectName, inPrefabMode);
            
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

        // Module info methods
        private Dictionary<string, object> GetMainModuleInfo(ParticleSystem.MainModule main)
        {
            return new Dictionary<string, object>
            {
                ["duration"] = main.duration,
                ["loop"] = main.loop,
                ["prewarm"] = main.prewarm,
                ["startDelay"] = GetMinMaxCurveValue(main.startDelay),
                ["startLifetime"] = GetMinMaxCurveValue(main.startLifetime),
                ["startSpeed"] = GetMinMaxCurveValue(main.startSpeed),
                ["startSize"] = GetMinMaxCurveValue(main.startSize),
                ["startRotation"] = GetMinMaxCurveValue(main.startRotation),
                ["flipRotation"] = main.flipRotation,
                ["startColor"] = GetMinMaxGradientValue(main.startColor),
                ["gravityModifier"] = GetMinMaxCurveValue(main.gravityModifier),
                ["simulationSpace"] = main.simulationSpace.ToString(),
                ["simulationSpeed"] = main.simulationSpeed,
                ["scalingMode"] = main.scalingMode.ToString(),
                ["playOnAwake"] = main.playOnAwake,
                ["maxParticles"] = main.maxParticles,
                ["emitterVelocityMode"] = main.emitterVelocityMode.ToString(),
                ["stopAction"] = main.stopAction.ToString()
            };
        }

        private Dictionary<string, object> GetShapeModuleInfo(ParticleSystem.ShapeModule shape)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = shape.enabled,
                ["shapeType"] = shape.shapeType.ToString(),
                ["angle"] = shape.angle,
                ["radius"] = shape.radius,
                ["radiusThickness"] = shape.radiusThickness,
                ["arc"] = shape.arc,
                ["arcMode"] = shape.arcMode.ToString(),
                ["arcSpread"] = shape.arcSpread,
                ["rotation"] = Vector3ToArray(shape.rotation),
                ["scale"] = Vector3ToArray(shape.scale),
                ["position"] = Vector3ToArray(shape.position),
                ["alignToDirection"] = shape.alignToDirection,
                ["randomDirectionAmount"] = shape.randomDirectionAmount,
                ["sphericalDirectionAmount"] = shape.sphericalDirectionAmount
            };
        }

        private Dictionary<string, object> GetEmissionModuleInfo(ParticleSystem.EmissionModule emission)
        {
            var emissionInfo = new Dictionary<string, object>
            {
                ["enabled"] = emission.enabled,
                ["rateOverTime"] = GetMinMaxCurveValue(emission.rateOverTime),
                ["rateOverDistance"] = GetMinMaxCurveValue(emission.rateOverDistance),
                ["burstCount"] = emission.burstCount
            };
            
            // Get burst information
            if (emission.burstCount > 0)
            {
                var bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);
                var burstList = new List<Dictionary<string, object>>();
                foreach (var burst in bursts)
                {
                    burstList.Add(new Dictionary<string, object>
                    {
                        ["time"] = burst.time,
                        ["count"] = GetMinMaxCurveValue(burst.count),
                        ["cycleCount"] = burst.cycleCount,
                        ["repeatInterval"] = burst.repeatInterval,
                        ["probability"] = burst.probability
                    });
                }
                emissionInfo["bursts"] = burstList;
            }
            return emissionInfo;
        }

        private Dictionary<string, object> GetVelocityOverLifetimeModuleInfo(ParticleSystem.VelocityOverLifetimeModule velocity)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = velocity.enabled,
                ["space"] = velocity.space.ToString(),
                ["x"] = GetMinMaxCurveValue(velocity.x),
                ["y"] = GetMinMaxCurveValue(velocity.y),
                ["z"] = GetMinMaxCurveValue(velocity.z),
                ["speedModifier"] = GetMinMaxCurveValue(velocity.speedModifier),
                ["orbitalX"] = GetMinMaxCurveValue(velocity.orbitalX),
                ["orbitalY"] = GetMinMaxCurveValue(velocity.orbitalY),
                ["orbitalZ"] = GetMinMaxCurveValue(velocity.orbitalZ),
                ["orbitalOffsetX"] = GetMinMaxCurveValue(velocity.orbitalOffsetX),
                ["orbitalOffsetY"] = GetMinMaxCurveValue(velocity.orbitalOffsetY),
                ["orbitalOffsetZ"] = GetMinMaxCurveValue(velocity.orbitalOffsetZ),
                ["radial"] = GetMinMaxCurveValue(velocity.radial)
            };
        }

        private Dictionary<string, object> GetColorOverLifetimeModuleInfo(ParticleSystem.ColorOverLifetimeModule colorOverLifetime)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = colorOverLifetime.enabled,
                ["color"] = GetMinMaxGradientValue(colorOverLifetime.color)
            };
        }

        private Dictionary<string, object> GetSizeOverLifetimeModuleInfo(ParticleSystem.SizeOverLifetimeModule sizeOverLifetime)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = sizeOverLifetime.enabled,
                ["size"] = GetMinMaxCurveValue(sizeOverLifetime.size),
                ["sizeMultiplier"] = sizeOverLifetime.sizeMultiplier,
                ["x"] = GetMinMaxCurveValue(sizeOverLifetime.x),
                ["y"] = GetMinMaxCurveValue(sizeOverLifetime.y),
                ["z"] = GetMinMaxCurveValue(sizeOverLifetime.z),
                ["separateAxes"] = sizeOverLifetime.separateAxes
            };
        }

        private Dictionary<string, object> GetRotationOverLifetimeModuleInfo(ParticleSystem.RotationOverLifetimeModule rotationOverLifetime)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = rotationOverLifetime.enabled,
                ["x"] = GetMinMaxCurveValue(rotationOverLifetime.x),
                ["y"] = GetMinMaxCurveValue(rotationOverLifetime.y),
                ["z"] = GetMinMaxCurveValue(rotationOverLifetime.z),
                ["separateAxes"] = rotationOverLifetime.separateAxes
            };
        }

        private Dictionary<string, object> GetForceOverLifetimeModuleInfo(ParticleSystem.ForceOverLifetimeModule forceOverLifetime)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = forceOverLifetime.enabled,
                ["space"] = forceOverLifetime.space.ToString(),
                ["x"] = GetMinMaxCurveValue(forceOverLifetime.x),
                ["y"] = GetMinMaxCurveValue(forceOverLifetime.y),
                ["z"] = GetMinMaxCurveValue(forceOverLifetime.z),
                ["randomized"] = forceOverLifetime.randomized
            };
        }

        private Dictionary<string, object> GetNoiseModuleInfo(ParticleSystem.NoiseModule noise)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = noise.enabled,
                ["strength"] = GetMinMaxCurveValue(noise.strength),
                ["strengthX"] = GetMinMaxCurveValue(noise.strengthX),
                ["strengthY"] = GetMinMaxCurveValue(noise.strengthY),
                ["strengthZ"] = GetMinMaxCurveValue(noise.strengthZ),
                ["frequency"] = noise.frequency,
                ["damping"] = noise.damping,
                ["octaveCount"] = noise.octaveCount,
                ["octaveMultiplier"] = noise.octaveMultiplier,
                ["octaveScale"] = noise.octaveScale,
                ["quality"] = noise.quality.ToString(),
                ["scrollSpeed"] = GetMinMaxCurveValue(noise.scrollSpeed),
                ["remapEnabled"] = noise.remapEnabled,
                ["remap"] = GetMinMaxCurveValue(noise.remap),
                ["remapX"] = GetMinMaxCurveValue(noise.remapX),
                ["remapY"] = GetMinMaxCurveValue(noise.remapY),
                ["remapZ"] = GetMinMaxCurveValue(noise.remapZ),
                ["positionAmount"] = GetMinMaxCurveValue(noise.positionAmount),
                ["rotationAmount"] = GetMinMaxCurveValue(noise.rotationAmount),
                ["sizeAmount"] = GetMinMaxCurveValue(noise.sizeAmount)
            };
        }

        private Dictionary<string, object> GetExternalForcesModuleInfo(ParticleSystem.ExternalForcesModule externalForces)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = externalForces.enabled,
                ["multiplier"] = externalForces.multiplier,
                ["multiplierCurve"] = GetMinMaxCurveValue(externalForces.multiplierCurve),
                ["influenceFilter"] = externalForces.influenceFilter.ToString(),
                ["influenceCount"] = externalForces.influenceCount
            };
        }

        private Dictionary<string, object> GetCollisionModuleInfo(ParticleSystem.CollisionModule collision)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = collision.enabled,
                ["type"] = collision.type.ToString(),
                ["mode"] = collision.mode.ToString(),
                ["dampen"] = GetMinMaxCurveValue(collision.dampen),
                ["bounce"] = GetMinMaxCurveValue(collision.bounce),
                ["lifetimeLoss"] = GetMinMaxCurveValue(collision.lifetimeLoss),
                ["minKillSpeed"] = collision.minKillSpeed,
                ["maxKillSpeed"] = collision.maxKillSpeed,
                ["collidesWith"] = collision.collidesWith.value,
                ["enableDynamicColliders"] = collision.enableDynamicColliders,
                ["maxCollisionShapes"] = collision.maxCollisionShapes,
                ["quality"] = collision.quality.ToString(),
                ["voxelSize"] = collision.voxelSize,
                ["radiusScale"] = collision.radiusScale,
                ["sendCollisionMessages"] = collision.sendCollisionMessages
            };
        }

        private Dictionary<string, object> GetSubEmittersModuleInfo(ParticleSystem.SubEmittersModule subEmitters)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = subEmitters.enabled,
                ["subEmittersCount"] = subEmitters.subEmittersCount
            };
        }

        private Dictionary<string, object> GetTextureSheetAnimationModuleInfo(ParticleSystem.TextureSheetAnimationModule textureSheetAnimation)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = textureSheetAnimation.enabled,
                ["mode"] = textureSheetAnimation.mode.ToString(),
                ["numTilesX"] = textureSheetAnimation.numTilesX,
                ["numTilesY"] = textureSheetAnimation.numTilesY,
                ["animation"] = textureSheetAnimation.animation.ToString(),
                ["frameOverTime"] = GetMinMaxCurveValue(textureSheetAnimation.frameOverTime),
                ["startFrame"] = GetMinMaxCurveValue(textureSheetAnimation.startFrame),
                ["cycleCount"] = textureSheetAnimation.cycleCount,
                ["rowIndex"] = textureSheetAnimation.rowIndex,
                ["uvChannelMask"] = textureSheetAnimation.uvChannelMask.ToString()
            };
        }

        private Dictionary<string, object> GetLightsModuleInfo(ParticleSystem.LightsModule lights)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = lights.enabled,
                ["ratio"] = lights.ratio,
                ["useRandomDistribution"] = lights.useRandomDistribution,
                ["light"] = lights.light != null ? lights.light.name : "None",
                ["useParticleColor"] = lights.useParticleColor,
                ["sizeAffectsRange"] = lights.sizeAffectsRange,
                ["alphaAffectsIntensity"] = lights.alphaAffectsIntensity,
                ["range"] = GetMinMaxCurveValue(lights.range),
                ["rangeMultiplier"] = lights.rangeMultiplier,
                ["intensity"] = GetMinMaxCurveValue(lights.intensity),
                ["intensityMultiplier"] = lights.intensityMultiplier,
                ["maxLights"] = lights.maxLights
            };
        }

        private Dictionary<string, object> GetTrailsModuleInfo(ParticleSystem.TrailModule trails)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = trails.enabled,
                ["mode"] = trails.mode.ToString(),
                ["ratio"] = trails.ratio,
                ["lifetime"] = GetMinMaxCurveValue(trails.lifetime),
                ["lifetimeMultiplier"] = trails.lifetimeMultiplier,
                ["minVertexDistance"] = trails.minVertexDistance,
                ["textureMode"] = trails.textureMode.ToString(),
                ["worldSpace"] = trails.worldSpace,
                ["dieWithParticles"] = trails.dieWithParticles,
                ["sizeAffectsWidth"] = trails.sizeAffectsWidth,
                ["sizeAffectsLifetime"] = trails.sizeAffectsLifetime,
                ["inheritParticleColor"] = trails.inheritParticleColor,
                ["colorOverLifetime"] = GetMinMaxGradientValue(trails.colorOverLifetime),
                ["widthOverTrail"] = GetMinMaxCurveValue(trails.widthOverTrail),
                ["widthOverTrailMultiplier"] = trails.widthOverTrailMultiplier,
                ["colorOverTrail"] = GetMinMaxGradientValue(trails.colorOverTrail),
                ["generateLightingData"] = trails.generateLightingData,
                ["ribbonCount"] = trails.ribbonCount,
                ["shadowBias"] = trails.shadowBias,
                ["splitSubEmitterRibbons"] = trails.splitSubEmitterRibbons
            };
        }

        private Dictionary<string, object> GetCustomDataModuleInfo(ParticleSystem.CustomDataModule customData)
        {
            return new Dictionary<string, object>
            {
                ["enabled"] = customData.enabled
            };
        }

        private Dictionary<string, object> GetRendererInfo(ParticleSystemRenderer renderer)
        {
            return new Dictionary<string, object>
            {
                ["renderMode"] = renderer.renderMode.ToString(),
                ["material"] = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "None",
                ["trailMaterial"] = renderer.trailMaterial != null ? renderer.trailMaterial.name : "None",
                ["sortMode"] = renderer.sortMode.ToString(),
                ["sortingFudge"] = renderer.sortingFudge,
                ["minParticleSize"] = renderer.minParticleSize,
                ["maxParticleSize"] = renderer.maxParticleSize,
                ["alignment"] = renderer.alignment.ToString(),
                ["flip"] = Vector3ToArray(renderer.flip),
                ["allowRoll"] = renderer.allowRoll,
                ["pivot"] = Vector3ToArray(renderer.pivot),
                ["maskInteraction"] = renderer.maskInteraction.ToString(),
                ["enableGPUInstancing"] = renderer.enableGPUInstancing,
                ["shadowCastingMode"] = renderer.shadowCastingMode.ToString(),
                ["receiveShadows"] = renderer.receiveShadows,
                ["shadowBias"] = renderer.shadowBias,
                ["motionVectorGenerationMode"] = renderer.motionVectorGenerationMode.ToString(),
                ["sortingLayerID"] = renderer.sortingLayerID,
                ["sortingOrder"] = renderer.sortingOrder,
                ["lightProbeUsage"] = renderer.lightProbeUsage.ToString(),
                ["reflectionProbeUsage"] = renderer.reflectionProbeUsage.ToString()
            };
        }

    }
}