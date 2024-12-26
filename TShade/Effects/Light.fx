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

static const float A = 0.18;
static const float B = 0.55;
static const float C = 0.12;
static const float D = 0.25; 
static const float E = 0.03; 
static const float F = 0.35;
static const float W = 12.8; 

static const float3 TINT_COLOR = float3(1.05, 1.02, 1.15);
static const float COLOR_SATURATION = 1.4;
static const float GLOW_INTENSITY = 1.2;

float3 Uncharted2Tonemap(float3 x)
{
    return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

float3 TonemapFilmic(float3 color)
{
    float exposure_bias = 2.2f;
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
    
    float i = pow(max(0, (3 - ls) / 3), 1.5) * GLOW_INTENSITY;
    
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