sampler uImage0 : register(s0);
texture2D tex0;
sampler2D uImage1 = sampler_state
{
    AddressU = Clamp;
    AddressV = Clamp;
    MagFilter = Anisotropic;
    MinFilter = Anisotropic;
    MipFilter = Linear;
    Texture = <tex0>;
};

float m;
float p; 
float m2; 
float2 uScreenResolution;
float bloomScatter; // new parameter for light scattering bloom

// enhanced bloom constants
static const float SOFT_KNEE = 0.65; 
static const float BLOOM_SATURATION = 1.3;
static const float BLOOM_SPREAD = 4.0;
static const float3 BLOOM_TINT = float3(1.05, 1.02, 1.15);
static const float SCATTER_STRENGTH = 1.2;

// luminance weights (more accurate)
static const float3 LUMINANCE_WEIGHTS = float3(0.2126, 0.7152, 0.0722);

float GetLuminance(float3 color)
{
    return dot(color, LUMINANCE_WEIGHTS);
}

// improved threshold with better falloff
float SmoothThreshold(float value, float threshold, float knee)
{
    float softness = clamp(knee, 0.0, 1.0);
    float lower = threshold * (1.0 - softness);
    float upper = threshold * (1.0 + softness);
    
    // smoother curve for better bloom distribution
    float t = smoothstep(lower, upper, value);
    return t * t * (3.0 - 2.0 * t); // smootherstep for even better curve
}

float3 AdjustContrast(float3 color, float contrast)
{
    const float midpoint = 0.18;
    return (color - midpoint) * contrast + midpoint;
}

float3 AdjustSaturation(float3 color, float saturation)
{
    float lum = GetLuminance(color);
    return lerp(float3(lum, lum, lum), color, saturation);
}

// enhanced bloom extraction with better light source detection
float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0, coords);
    float lum = GetLuminance(color.rgb);
    
    // multi-threshold bloom for different light sources
    float mainBloom = SmoothThreshold(lum, m, SOFT_KNEE);
    
    // additional bloom for very bright areas (light sources)
    float brightBloom = SmoothThreshold(lum, m * 1.5, SOFT_KNEE * 0.5);
    
    // combine bloom contributions
    float totalBloom = mainBloom + brightBloom * 0.5;
    
    // enhance colors for light sources
    float3 bloomColor = color.rgb * totalBloom;
    
    // apply light scattering effect
    bloomColor *= (1.0 + bloomScatter * SCATTER_STRENGTH);
    
    return float4(bloomColor * BLOOM_TINT, color.a);
}

// enhanced bloom blend with better gaussian approximation
float4 Blend(float2 coords : TEXCOORD0) : COLOR0
{
    float4 originalColor = tex2D(uImage1, coords);
    float2 texelSize = 1.0 / uScreenResolution;
    
    float4 bloomColor = 0;
    float weightSum = 0;
    float sigma = BLOOM_SPREAD;
    
    // optimized 13-tap gaussian blur for ps_2_0
    const int SAMPLE_COUNT = 13;
    
    // pre-calculated offsets for better performance
    float2 offsets[13] =
    {
        float2(0, 0),
        // cardinal directions
        float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
        // diagonal directions  
        float2(1, 1), float2(-1, -1), float2(-1, 1), float2(1, -1),
        // extended samples for better blur
        float2(2, 0), float2(-2, 0), float2(0, 2), float2(0, -2)
    };
    
    // weights for gaussian distribution
    float weights[13] =
    {
        0.196, // center
        0.144, 0.144, 0.144, 0.144, // cardinal
        0.078, 0.078, 0.078, 0.078, // diagonal
        0.039, 0.039, 0.039, 0.039  // extended
    };
    
    [unroll(13)]
    for (int i = 0; i < SAMPLE_COUNT; i++)
    {
        float2 offset = offsets[i] * texelSize * sigma;
        float weight = weights[i];
        bloomColor += tex2D(uImage0, coords + offset) * weight;
        weightSum += weight;
    }
    
    bloomColor /= weightSum;
    
    // enhanced bloom processing
    float3 bloomFinal = bloomColor.rgb * m2;
    bloomFinal = AdjustSaturation(bloomFinal, BLOOM_SATURATION);
    
    // improved alpha calculation for better bloom blending
    float originalLum = GetLuminance(originalColor.rgb);
    float3 alpha = pow(abs(1.0 - originalLum), p); // use abs() to avoid negative pow
    alpha = clamp(alpha, 0.1, 1.0); // prevent complete bloom override
    
    // adaptive bloom strength based on scene brightness
    float sceneBrightness = GetLuminance(originalColor.rgb);
    float adaptiveStrength = 1.0 - sceneBrightness * 0.3;
    bloomFinal *= adaptiveStrength;
    
    // final color composition
    float4 finalColor;
    finalColor.rgb = originalColor.rgb + (bloomFinal * alpha);
    finalColor.a = originalColor.a;
    
    // subtle contrast enhancement
    finalColor.rgb = AdjustContrast(finalColor.rgb, 1.1);
    
    return finalColor;
}

technique Technique1
{
    pass Bloom
    {
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
    pass Blend
    {
        PixelShader = compile ps_2_0 Blend();
    }
}