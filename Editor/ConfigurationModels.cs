using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace UnityNaturalMCPExtension.Editor
{
    [Serializable]
    public class ParticleSystemConfiguration
    {
        public MainModuleSettings main { get; set; }
        public EmissionSettings emission { get; set; }
        public ShapeSettings shape { get; set; }
        public VelocitySettings velocityOverLifetime { get; set; }
    }

    [Serializable]
    public class MainModuleSettings
    {
        [DefaultValue(5.0f)]
        public float duration { get; set; } = 5.0f;
        
        [DefaultValue(true)]
        public bool looping { get; set; } = true;
        
        [DefaultValue(false)]
        public bool prewarm { get; set; } = false;
        
        [DefaultValue(10.0f)]
        public float startLifetime { get; set; } = 10.0f;
        
        [DefaultValue(5.0f)]
        public float startSpeed { get; set; } = 5.0f;
        
        [DefaultValue(1.0f)]
        public float startSize { get; set; } = 1.0f;
        
        public float[] startColor { get; set; } = { 1.0f, 1.0f, 1.0f, 1.0f };
        
        [Range(0f, 360f)]
        [DefaultValue(0f)]
        public float startRotation { get; set; } = 0f;
        
        [Range(0f, 1f)]
        [DefaultValue(0f)]
        public float gravityModifier { get; set; } = 0f;
        
        [Required]
        [DefaultValue(10)]
        public int maxParticles { get; set; } = 10;
    }

    [Serializable]
    public class EmissionSettings
    {
        [DefaultValue(true)]
        public bool enabled { get; set; } = true;
        
        [DefaultValue(10f)]
        public float rateOverTime { get; set; } = 10f;
        
        [DefaultValue(0f)]
        public float rateOverDistance { get; set; } = 0f;
        
        public BurstSettings[] bursts { get; set; } = Array.Empty<BurstSettings>();
    }

    [Serializable]
    public class BurstSettings
    {
        [DefaultValue(0f)]
        public float time { get; set; } = 0f;
        
        [DefaultValue(30)]
        public int count { get; set; } = 30;
        
        [DefaultValue(1)]
        public int cycles { get; set; } = 1;
        
        [DefaultValue(0f)]
        public float interval { get; set; } = 0f;
        
        [DefaultValue(1f)]
        public float probability { get; set; } = 1f;
    }

    [Serializable]
    public class ShapeSettings
    {
        [DefaultValue(true)]
        public bool enabled { get; set; } = true;
        
        [DefaultValue("Cone")]
        public string shapeType { get; set; } = "Cone";
        
        [DefaultValue(25f)]
        public float angle { get; set; } = 25f;
        
        [DefaultValue(1f)]
        public float radius { get; set; } = 1f;
        
        [DefaultValue(0f)]
        public float radiusThickness { get; set; } = 0f;
        
        [DefaultValue(0f)]
        public float arc { get; set; } = 0f;
        
        [DefaultValue(1f)]
        public float length { get; set; } = 1f;
        
        public float[] position { get; set; } = { 0f, 0f, 0f };
        
        public float[] rotation { get; set; } = { 0f, 0f, 0f };
        
        public float[] scale { get; set; } = { 1f, 1f, 1f };
    }

    [Serializable]
    public class VelocitySettings
    {
        [DefaultValue(false)]
        public bool enabled { get; set; } = false;
        
        public float[] linear { get; set; } = { 0f, 0f, 0f };
        
        public float[] orbital { get; set; } = { 0f, 0f, 0f };
        
        public float[] offset { get; set; } = { 0f, 0f, 0f };
        
        [DefaultValue(1f)]
        public float radial { get; set; } = 1f;
        
        [DefaultValue(1f)]
        public float speedModifier { get; set; } = 1f;
        
        [DefaultValue("Local")]
        public string space { get; set; } = "Local";
    }

    [Serializable]
    public class MaterialConfiguration
    {
        [Required]
        public string materialName { get; set; }
        
        [Required]
        public string shaderName { get; set; }
        
        public float[] baseColor { get; set; } = { 1f, 1f, 1f, 1f };
        
        [Range(0f, 1f)]
        [DefaultValue(0f)]
        public float metallic { get; set; } = 0f;
        
        [Range(0f, 1f)]
        [DefaultValue(0.5f)]
        public float smoothness { get; set; } = 0.5f;
        
        public float[] emission { get; set; } = { 0f, 0f, 0f };
        
        [DefaultValue(1f)]
        public float emissionIntensity { get; set; } = 1f;
        
        public Dictionary<string, object> additionalProperties { get; set; } = new();
    }

    [Serializable]
    public class ObjectConfiguration
    {
        [Required]
        public string objectName { get; set; }
        
        [Required]
        public string type { get; set; }
        
        public float[] position { get; set; } = { 0f, 0f, 0f };
        
        public float[] rotation { get; set; } = { 0f, 0f, 0f };
        
        public float[] scale { get; set; } = { 1f, 1f, 1f };
        
        public string parentName { get; set; }
        
        public string primitiveType { get; set; }
        
        public string prefabName { get; set; }
        
        public ComponentConfiguration[] components { get; set; } = Array.Empty<ComponentConfiguration>();
    }

    [Serializable]
    public class ComponentConfiguration
    {
        [Required]
        public string componentType { get; set; }
        
        public Dictionary<string, object> properties { get; set; } = new();
    }
}