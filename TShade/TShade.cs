using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.GameContent.Drawing;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Light;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace TShade 
{
	public class TShade : Mod 
	{
		public static bool useLight = true;
		public static bool useBloom = true;

		public static float lightIntensity = 1f;
		public static float shadowIntensity = 1f;
		public static float bloomIntensity = 0.5f;
		public static float moonLightIntensity = 1f;

		public static int quality = 3;

		public RenderTarget2D screen;
		public RenderTarget2D light;
		public RenderTarget2D bloom;
		public RenderTarget2D cloud;

		public static Effect Light;
		public static Effect Shadow;
		public static Effect Bloom;

		private Texture2D colorTexDay;
		private Texture2D colorTexNight;
		private Texture2D pixelTexture;

		public override void Load()
		{
			if (!Main.dedServ)
			{
				Light = ModContent.Request<Effect>("TShade/Effects/Light", (AssetRequestMode)2).Value;
				Shadow = ModContent.Request<Effect>("TShade/Effects/Shadow", (AssetRequestMode)2).Value;
				Bloom = ModContent.Request<Effect>("TShade/Effects/Bloom", (AssetRequestMode)2).Value;

				colorTexDay = ModContent.Request<Texture2D>("TShade/ColorTexDay", AssetRequestMode.ImmediateLoad).Value;
				colorTexNight = ModContent.Request<Texture2D>("TShade/ColorTexNight", AssetRequestMode.ImmediateLoad).Value;
				pixelTexture = ModContent.Request<Texture2D>("TShade/PixelTex", AssetRequestMode.ImmediateLoad).Value;

				Terraria.Graphics.Effects.On_FilterManager.EndCapture += FilterManager_EndCapture;
				Terraria.On_Main.LoadWorlds += Main_LoadWorlds;
				Main.OnResolutionChanged += Main_OnResolutionChanged;
			}
		}

		public override void PostSetupContent()
		{
			if (!Main.dedServ)
			{
				Light = ModContent.Request<Effect>("TShade/Effects/Light", (AssetRequestMode)2).Value;
				Shadow = ModContent.Request<Effect>("TShade/Effects/Shadow", (AssetRequestMode)2).Value;
				Bloom = ModContent.Request<Effect>("TShade/Effects/Bloom", (AssetRequestMode)2).Value;
			}
		}

		public override void Unload()
		{
			Terraria.Graphics.Effects.On_FilterManager.EndCapture -= FilterManager_EndCapture;
			Terraria.On_Main.LoadWorlds -= Main_LoadWorlds;
			Main.OnResolutionChanged -= Main_OnResolutionChanged;
			Light = null;
			Shadow = null;
			Bloom = null;
			screen = null;
			light = null;
			bloom = null;
			colorTexDay = null;
			colorTexNight = null;
			pixelTexture = null;
		}

		private void Main_LoadWorlds(Terraria.On_Main.orig_LoadWorlds orig)
		{
			orig();
			if (screen == null)
			{
				GraphicsDevice gd = Main.instance.GraphicsDevice;
				screen = new RenderTarget2D(gd, gd.PresentationParameters.BackBufferWidth / 3, gd.PresentationParameters.BackBufferHeight / 3, false, gd.PresentationParameters.BackBufferFormat, (DepthFormat)0);
				light = new RenderTarget2D(gd, gd.PresentationParameters.BackBufferWidth, gd.PresentationParameters.BackBufferHeight, false, gd.PresentationParameters.BackBufferFormat, (DepthFormat)0);
				bloom = new RenderTarget2D(gd, gd.PresentationParameters.BackBufferWidth / 3, gd.PresentationParameters.BackBufferHeight / 3, false, gd.PresentationParameters.BackBufferFormat, (DepthFormat)0);
				cloud = new RenderTarget2D(gd, gd.PresentationParameters.BackBufferWidth / 3, gd.PresentationParameters.BackBufferHeight / 3, false, gd.PresentationParameters.BackBufferFormat, (DepthFormat)0);
			}
		}

		private void Main_OnResolutionChanged(Vector2 obj)
		{
			screen = new RenderTarget2D(Main.instance.GraphicsDevice, Main.screenWidth / 3, Main.screenHeight / 3);
			light = new RenderTarget2D(Main.instance.GraphicsDevice, Main.screenWidth, Main.screenHeight);
			bloom = new RenderTarget2D(Main.instance.GraphicsDevice, Main.screenWidth / 3, Main.screenHeight / 3);
		}

		private void FilterManager_EndCapture(Terraria.Graphics.Effects.On_FilterManager.orig_EndCapture orig, FilterManager self, RenderTarget2D finalTexture, RenderTarget2D screenTarget1, RenderTarget2D screenTarget2, Color clearColor)
		{
			GraphicsDevice gd = Main.instance.GraphicsDevice;
			SpriteBatch sb = Main.spriteBatch;
			if (Main.myPlayer >= 0)
			{
				if (screen == null)
				{
					screen = new RenderTarget2D(gd, gd.PresentationParameters.BackBufferWidth / 3, gd.PresentationParameters.BackBufferHeight / 3, false, gd.PresentationParameters.BackBufferFormat, (DepthFormat)0);
					light = new RenderTarget2D(gd, gd.PresentationParameters.BackBufferWidth, gd.PresentationParameters.BackBufferHeight, false, gd.PresentationParameters.BackBufferFormat, (DepthFormat)0);
					bloom = new RenderTarget2D(gd, gd.PresentationParameters.BackBufferWidth / 3, gd.PresentationParameters.BackBufferHeight / 3, false, gd.PresentationParameters.BackBufferFormat, (DepthFormat)0);
					cloud = new RenderTarget2D(gd, gd.PresentationParameters.BackBufferWidth / 3, gd.PresentationParameters.BackBufferHeight / 3, false, gd.PresentationParameters.BackBufferFormat, (DepthFormat)0);
				}
				if (useBloom)
				{
					UseBloom(gd);
				}
				if (useLight)
				{
					UseLightAndShadow(gd, sb);
				}
			}
			orig.Invoke(self, finalTexture, screenTarget1, screenTarget2, clearColor);
		}

		// type is light or bloom
		private void RenderTargetHelper(GraphicsDevice graphicsDevice, String type) {
			graphicsDevice.SetRenderTarget(Main.screenTargetSwap);
			graphicsDevice.Clear(Color.Transparent);
			Main.spriteBatch.Begin((SpriteSortMode)0, BlendState.AlphaBlend);
			Main.spriteBatch.Draw((Texture2D)(object)Main.screenTarget, Vector2.Zero, Color.White);
			Main.spriteBatch.End();
			if (type == "bloom"){
				graphicsDevice.SetRenderTarget(screen);
			}
			else{
				graphicsDevice.SetRenderTarget(light);
			}
			graphicsDevice.Clear(Color.Transparent);
		}

		private void UseBloom(GraphicsDevice graphicsDevice)
		{
			RenderTargetHelper(graphicsDevice, "bloom");
			ApplyBloomEffect(graphicsDevice);
			BlendBloomEffect(graphicsDevice);
		}

		private void ApplyBloomEffect(GraphicsDevice graphicsDevice)
		{
			Main.spriteBatch.Begin((SpriteSortMode)1, BlendState.AlphaBlend);
			Bloom.CurrentTechnique.Passes[0].Apply();
			Bloom.Parameters["m"].SetValue(0.68f - bloomIntensity * 0.02f);
			Main.spriteBatch.Draw((Texture2D)(object)Main.screenTarget, Vector2.Zero, (Rectangle?)null, Color.White, 0f, Vector2.Zero, 0.333f, (SpriteEffects)0, 0f);
			Main.spriteBatch.End();
		}

		private void BlendBloomEffect(GraphicsDevice graphicsDevice)
		{
			graphicsDevice.SetRenderTarget(Main.screenTarget);
			graphicsDevice.Clear(Color.Transparent);

			Main.spriteBatch.Begin((SpriteSortMode)1, BlendState.Additive);
			Bloom.CurrentTechnique.Passes["Blend"].Apply();
			Bloom.Parameters["tex0"].SetValue((Texture)(object)Main.screenTargetSwap);
			Bloom.Parameters["p"].SetValue(3);
			Bloom.Parameters["m2"].SetValue(1.5f * bloomIntensity);
			Main.spriteBatch.Draw((Texture2D)(object)screen, Vector2.Zero, (Rectangle?)null, Color.White, 0f, Vector2.Zero, 3f, (SpriteEffects)0, 0f);
			Main.spriteBatch.End();
		}

		private float Gauss(float x, float sigma)
		{
			return 0.39894f * (float)Math.Exp((double)(-0.5f * x * x / (0.2f * sigma)));
		}

		private void UseLightAndShadow(GraphicsDevice gd, SpriteBatch sb)
		{
			RenderTargetHelper(gd, "light");
			ApplyLightingEffect(gd, sb);
			ApplyShadowEffect(gd, sb);
			CombineLightAndShadowEffects(gd, sb);
			FinalBlendPasses(gd, sb);
		}

		private void ApplyLightingEffect(GraphicsDevice gd, SpriteBatch sb)
		{
			sb.Begin((SpriteSortMode)1, BlendState.NonPremultiplied);
			
			Light.CurrentTechnique.Passes["Light"].Apply();
			Light.Parameters["uScreenResolution"].SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
			Light.Parameters["uPos"].SetValue(ToScreenCoords(GetSunPos()));
			Light.Parameters["tex0"].SetValue(Main.dayTime ? colorTexDay : colorTexNight);
			float intensity = CalculateIntensity();
			float moonLightIntensity = CalculateMoonlightIntensity();
			float finalIntensity = lightIntensity * (0.75f + intensity * 0.2f);
			Light.Parameters["intensity"].SetValue(Main.dayTime ? finalIntensity : (moonLightIntensity * moonLightIntensity));
			Light.Parameters["t"].SetValue(Main.dayTime ? (float)Main.time / 54000f : (float)Main.time / 32400f);

			if (IsAboveSurface())
			{
				sb.Draw(pixelTexture, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
			}
			
			sb.End();
		}

		private float CalculateIntensity()
		{
			Color c = Main.ColorOfTheSkies;
			float intensity = 1f - 1.2f * (c.R * 0.3f + c.G * 0.6f + c.B * 0.1f) / 255f;

			if (Main.LocalPlayer.ZoneSnow && !Main.LocalPlayer.ZoneCrimson && !Main.LocalPlayer.ZoneCorrupt)
				intensity -= Main.bgAlphaFrontLayer[7] * 0.1f;

			if (Main.LocalPlayer.ZoneCrimson)
				intensity += 0.2f;

			if (Main.snowBG[0] == 263 || Main.snowBG[0] == 258 || Main.snowBG[0] == 267)
				intensity -= Main.bgAlphaFrontLayer[7] * 1f;

			if (Main.desertBG[0] == 248)
				intensity -= Main.bgAlphaFrontLayer[2] * 0.6f;

			return intensity;
		}

		private float CalculateMoonlightIntensity()
		{
			return Main.moonPhase switch
			{
				0 => 1.01f,
				3 or 5 => 0.9f,
				4 => 0.6f,
				_ => 1f,
			};
		}

		private bool IsAboveSurface()
		{
			return ((Entity)Main.LocalPlayer).Center.Y < Main.worldSurface * 16.0 + 800.0;
		}

		private void ApplyShadowEffect(GraphicsDevice gd, SpriteBatch sb)
		{
			gd.SetRenderTarget(screen);
			gd.Clear(Color.Transparent);

			sb.Begin((SpriteSortMode)1, BlendState.Additive);
			Shadow.CurrentTechnique.Passes[0].Apply();
			Shadow.Parameters["uScreenResolution"].SetValue(new Vector2((float)Main.screenWidth, (float)Main.screenHeight));
			Shadow.Parameters["m"].SetValue(CalculateShadowIntensity());
			Shadow.Parameters["uPos"].SetValue(ToScreenCoords(GetSunPos()));

			sb.Draw((Texture2D)(object)Main.screenTarget, Vector2.Zero, (Rectangle?)null, 
				Color.White, 0f, Vector2.Zero, 0.333f, (SpriteEffects)0, 0f);
			sb.End();
		}

		private float CalculateShadowIntensity()
		{
			float desertI = Main.bgAlphaFrontLayer[2] * 0.1f;

			if (Main.desertBG[0] == 248)
				desertI = 0f;

			return Main.dayTime ? 1f - desertI : 0.02f;
		}

		private void CombineLightAndShadowEffects(GraphicsDevice gd, SpriteBatch sb)
		{
			gd.SetRenderTarget(Main.screenTarget);
			gd.Clear(Color.Transparent);

			sb.Begin((SpriteSortMode)0, BlendState.Additive);

			if (Main.dayTime)
			{
				for (int j = 0; j < quality * 10; j++)
				{
					float shaderIntensity = CalculateDayShaderIntensity(j);
					float alpha = CalculateDayAlpha(j, shaderIntensity);
					sb.Draw((Texture2D)(object)screen, GetSunPos() / 3f, (Rectangle?)null, 
						Color.White * alpha, 0f, GetSunPos() / 3f, 
						1f * (1f + j * 0.015f), (SpriteEffects)0, 0f);
				}
			}
			else
			{
				for (int i = 0; i < 20; i++)
				{
					float shaderIntensity = 195f;
					float alpha = (20f - i) / shaderIntensity;
					sb.Draw((Texture2D)(object)screen, GetSunPos() / 3f, (Rectangle?)null, 
						Color.White * alpha, 0f, GetSunPos() / 3f, 
						1f * (1f + i * 0.015f), (SpriteEffects)0, 0f);
				}
			}
			sb.End();
		}

		private float CalculateDayShaderIntensity(int j)
		{
			float shaderIntensity = quality * 18 * (1f + quality * 0.16f);

			if (Main.snowBG[0] == 263 || Main.snowBG[0] == 258 || Main.snowBG[0] == 267)
				shaderIntensity -= Main.bgAlphaFrontLayer[7] * 30f;

			return shaderIntensity;
		}

		private float CalculateDayAlpha(int j, float shaderIntensity)
		{
			return (quality * 10 - j) / shaderIntensity;
		}

		private void FinalBlendPasses(GraphicsDevice gd, SpriteBatch sb)
		{
			gd.SetRenderTarget(screen);
			gd.Clear(Color.Transparent);

			sb.Begin((SpriteSortMode)0, BlendState.AlphaBlend);
			sb.Draw((Texture2D)(object)Main.screenTarget, Vector2.Zero, (Rectangle?)null, 
				Color.White, 0f, Vector2.Zero, 1f, (SpriteEffects)0, 0f);
			sb.End();

			gd.SetRenderTarget(Main.screenTarget);
			gd.Clear(Color.Transparent);

			sb.Begin((SpriteSortMode)0, BlendState.Additive);
			sb.Draw((Texture2D)(object)Main.screenTargetSwap, Vector2.Zero, Color.White);
			sb.End();

			sb.Begin((SpriteSortMode)1, BlendState.Additive);
			Shadow.CurrentTechnique.Passes[1].Apply();
			Shadow.Parameters["tex0"].SetValue((Texture)(object)screen);
			sb.Draw((Texture2D)(object)light, Vector2.Zero, Color.White);
			sb.End();
		}

		public static Vector2 ToScreenCoords(Vector2 vec)
		{
			return vec / new Vector2((float)Main.screenWidth, (float)Main.screenHeight);
		}

		public Vector2 GetSunPos()
		{
			// re-used code from the dynamic lights mod
			float bgTop = (int)((0.0 - (double)Main.screenPosition.Y) / (Main.worldSurface * 16.0 - 600.0) * 200.0);
			float height = 0;
			if (Main.dayTime)
			{
				if (Main.time < 27000.0)
				{
					height = bgTop + (float)Math.Pow(1.0 - Main.time / 54000.0 * 2.0, 2.0) * 250.0f + 180.0f;
				}
				else
				{
					height = bgTop + (float)Math.Pow((Main.time / 54000.0 - 0.5) * 2.0, 2.0) * 250.0f + 180.0f;
				}
			}
			return new Vector2((float)Main.time / (Main.dayTime ? 54000.0f : 32400.0f) * (float)(Main.screenWidth + 200f) - 100f, height + Main.sunModY);
		}
	}
}