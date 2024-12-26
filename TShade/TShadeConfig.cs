using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TShade;

public class TShadeConfig : ModConfig
{
    #region General Settings

    [Header("GeneralSettings")]
    [Label("Enable Lighting")]
    [DefaultValue(true)]
    public bool UseLight = true;

    [Label("Overall Quality")]
    [Slider]
    [Range(1, 5)]
    [DefaultValue(3)]
    public int Quality = 3;

    #endregion

    #region Light Intensities

    [Header("LightIntensities")]
    [Label("General Light Intensity")]
    [Range(0.75f, 1.5f)]
    [DefaultValue(1f)]
    public float LightIntensity = 1f;

    [Label("Moon Light Intensity")]
    [Range(0.75f, 1.5f)]
    [DefaultValue(1f)]
    public float MoonLightIntensity = 1f;

    [Label("Shadow Intensity")]
    [Range(0.75f, 1.5f)]
    [DefaultValue(1f)]
    public float ShadowIntensity = 1f;

    #endregion

    #region Bloom Effects

    [Header("BloomEffects")]
    [Label("Enable Bloom")]
    [DefaultValue(true)]
    public bool UseBloom = true;

    [Label("Bloom Intensity")]
    [Range(0.75f, 1.5f)]
    [DefaultValue(1f)]
    public float BloomIntensity = 1f;

    #endregion

    public override ConfigScope Mode => ConfigScope.ClientSide;

	public override void OnChanged()
	{
		TShade.useLight = UseLight;
		TShade.quality = Quality;
		TShade.lightIntensity = LightIntensity;
		TShade.moonLightIntensity = MoonLightIntensity;
		TShade.shadowIntensity = ShadowIntensity;
		TShade.useBloom = UseBloom;
		TShade.bloomIntensity = BloomIntensity;
	}

}
