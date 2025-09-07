using System;
using System.Drawing;
using System.Numerics;

namespace TShader.Core
{
    public static class ShaderConstants
    {
        // render target scale factors
        public const int SCREEN_SCALE_DIVISOR = 3;
        public const float BLOOM_SCALE = 0.333f;
        public const float SHADOW_SCALE = 0.333f;
        
        // time constants from Terraria
        public const float DAY_TIME_TOTAL = 54000f;
        public const float NIGHT_TIME_TOTAL = 32400f;
        
        // enhanced lighting constants for better visual quality
        public const float BASE_LIGHT_INTENSITY = 0.82f;  // slightly increased
        public const float LIGHT_INTENSITY_MULTIPLIER = 1.15f;  // enhanced multiplier
        public const float NIGHT_SHADER_INTENSITY = 180f;  // increased night intensity
        
        // snow background IDs that affect lighting
        public static readonly int[] SNOW_BG_IDS = { 263, 258, 267 };
        
        // desert background ID
        public const int DESERT_BG_ID = 248;
        
        // surface calculation offset
        public const double SURFACE_OFFSET = 800.0;
        
        // enhanced bloom effect constants for better quality
        public const float BLOOM_BASE_M = 0.72f;  // adjusted for better threshold
        public const float BLOOM_M_MODIFIER = 0.025f;  // slight increase
        public const float BLOOM_BLEND_MULTIPLIER = 1.65f;  // enhanced blending
        public const float BLOOM_SCATTER_STRENGTH = 1.85f;  // stronger scattering
        
        // enhanced shader effect constants
        public const int NIGHT_ITERATIONS = 22;  // slight increase for smoother gradients
        public const float SHADOW_ITERATION_SCALE = 0.018f;  // refined scaling
        public const float DAY_SHADOW_BASE = 1.05f;  // subtle enhancement
        public const float NIGHT_SHADOW_BASE = 0.025f;  // improved night shadows
    }
}