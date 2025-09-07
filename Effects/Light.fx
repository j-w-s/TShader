sampler uImage0 : register(s0);
texture2D tex0;
sampler2D uColor = sampler_state
{
    AddressU = Clamp;
    AddressV = Clamp;
    MagFilter = Anisotropic;
    MinFilter = Anisotropic;
    MipFilter = Linear;
    Texture = <tex0>;
};

float2 uScreenResolution;
float t;
float intensity;
float2 uPos;

static const float A = 0.20;
static const float B = 0.58;
static const float C = 0.13;
static const float D = 0.26; 
static const float E = 0.028; 
static const float F = 0.36;
static const float W = 11.7; 

static const float3 TINT_COLOR = float3(1.0, 0.85, 1.25);
static const float COLOR_SATURATION = 1.45;
static const float GLOW_INTENSITY = 1.8;

float3 Uncharted2Tonemap(float3 x)
{
    return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

float3 TonemapFilmic(float3 color)
{
    float exposure_bias = 2.6f;
    float3 curr = Uncharted2Tonemap(exposure_bias * color);
    float3 whiteScale = 1.0f / Uncharted2Tonemap(float3(W, W, W));
    return curr * whiteScale;
}

float3 ApplyColorGrade(float3 color)
{
    float lum = dot(color, float3(0.2126, 0.7152, 0.0722));
    float3 saturated = lerp(float3(lum, lum, lum), color, COLOR_SATURATION);
    return saturated * TINT_COLOR;
}

float4 PSFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float2 pos = uPos;
    float2 offset = (coords - pos);
    float2 rpos = offset * float2(uScreenResolution.x / uScreenResolution.y, 1);
    float ls = length(rpos);
    
    float i = pow(max(0, (4.0 - ls) / 4.0), 2.4) * GLOW_INTENSITY;
    
    float4 baseColor = tex2D(uColor, float2(t, 0)) * i * intensity;
    
    float3 gradedColor = ApplyColorGrade(baseColor.rgb);
    float3 tonemapped = TonemapFilmic(gradedColor);
    
    return float4(tonemapped, baseColor.a);
}

technique Technique1
{
    pass Light
    {
        PixelShader = compile ps_2_0 PSFunction();
    }
}