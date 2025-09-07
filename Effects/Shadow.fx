sampler uImage0 : register(s0);     // specifies sampler
texture2D tex0;      // retrieves color of shadow
sampler2D uImage1 = sampler_state{

    AddressU = Clamp; 
    AddressV = Clamp;
    MagFilter = Anisotropic;
    MinFilter = Anisotropic;
    MipFilter = Linear;
    Texture = <tex0>;

};

float2 uScreenResolution;
float m;
float uIntensity;
float2 uPos;
float uCloudDiff;
float uCloudMul;

float colorDiff(float4 c1, float4 c2)
{
    float3 c = c1.rgb - c2.rgb;
    return sqrt(dot(c, c));
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0) : COLOR0
{
    float4 c = tex2D(uImage0,coords);
    float4 finalColor = float4(0, 0, 0, 0);
    if (c.b < m)
        finalColor = float4(1, 1, 1, pow(1 - c.b, 0.85));

    return finalColor;
}

float4 UseShadow(float2 coords : TEXCOORD0) : COLOR0
{
    float4 color = tex2D(uImage0,coords);
    float4 shadow = tex2D(uImage1, coords);
    color.rgb -= shadow.rgb * 0.95;
    
    return color;
}

technique Technique1
{
	pass GetShadow{

		PixelShader = compile ps_2_0 PixelShaderFunction();

	}

    pass UseShadow{

        PixelShader = compile ps_2_0 UseShadow();

    }
}