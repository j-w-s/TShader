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

static const float SOFT_KNEE = 0.65; 
static const float BLOOM_SATURATION = 1.2;
static const float BLOOM_SPREAD = 4.0;
static const float3 BLOOM_TINT = float3(1.05, 1.02, 1.15);

float GetLuminance(float3 color)
{
    return dot(color, float3(0.2126, 0.7152, 0.0722));
}

float SmoothThreshold(float value, float threshold, float knee)
{
    float softness = clamp(knee, 0.0, 1.0);
    float lower = threshold * (1.0 - softness);
    float upper = threshold * (1.0 + softness);
    return smoothstep(lower, upper, value);
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

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0, coords);
    float lum = GetLuminance(color.rgb);
    
    float bloomAmount = SmoothThreshold(lum, m, SOFT_KNEE);
    
    return float4(color.rgb * bloomAmount * BLOOM_TINT, color.a);
}

float4 Blend(float2 coords : TEXCOORD0) : COLOR0
{
    float4 originalColor = tex2D(uImage1, coords);
    float2 texelSize = 1.0 / uScreenResolution;
    
    float4 bloomColor = 0;
    float weightSum = 0;
    float sigma = BLOOM_SPREAD;
    
    const int SAMPLE_COUNT = 13;
    
    float2 offsets[13] =
    {
        float2(0, 0),
        float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
        float2(1, 1), float2(-1, -1), float2(-1, 1), float2(1, -1),
        float2(2, 0), float2(-2, 0), float2(0, 2), float2(0, -2)
    };
    
    [unroll(13)]
    for (int i = 0; i < SAMPLE_COUNT; i++)
    {
        float2 offset = offsets[i] * texelSize * sigma;
        float weight = exp(-dot(offset, offset) / (2.0 * sigma * sigma));
        bloomColor += tex2D(uImage0, coords + offset) * weight;
        weightSum += weight;
    }
    
    bloomColor /= weightSum;
    
    float3 bloomFinal = bloomColor.rgb * m2;
    bloomFinal = AdjustSaturation(bloomFinal, BLOOM_SATURATION);
    
    float lum = GetLuminance(originalColor.rgb);
    float3 alpha = pow(1 - lum, p);
    alpha = clamp(alpha, 0, 1);
    
    float4 finalColor;
    finalColor.rgb = originalColor.rgb + (bloomFinal * alpha);
    finalColor.a = originalColor.a;
    
    finalColor.rgb = AdjustContrast(finalColor.rgb, 1.15);
    
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