using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using TShader.Core;

namespace TShader.Passes
{
    public class BloomPass : BaseShaderPass
    {
        public override string Name => "Bloom";
        public override bool IsEnabled => TShader.useBloom;

        // cache expensive calculations to avoid recalculating every frame
        private float _cachedAdaptiveThreshold = -1f;
        private float _cachedScatterStrength = -1f;
        private float _cachedBloomPower = -1f;
        private float _cachedBloomMultiplier = -1f;
        private int _lastFrameUpdate = -1;

        public BloomPass(ResourceCache resourceCache) : base(resourceCache) { }

        public override void Execute(ShaderContext context)
        {
            var bloomEffect = ResourceCache.GetEffect("Bloom");
            var screenTarget = ResourceCache.GetRenderTarget("Screen");

            if (bloomEffect == null || screenTarget == null) return;

            // prepare render targets
            SwapToScreenTargetHelper(context);
            SetupRenderTarget(context.GraphicsDevice, screenTarget, context.ScreenTarget);

            // apply enhanced bloom effect
            ApplyEnhancedBloomEffect(context, bloomEffect);

            // blend bloom back to main target with enhanced processing
            BlendEnhancedBloomEffect(context, bloomEffect, screenTarget);
        }

        private void ApplyEnhancedBloomEffect(ShaderContext context, Effect bloomEffect)
        {
            context.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            
            bloomEffect.CurrentTechnique.Passes[0].Apply();
            
            // adaptive bloom threshold based on scene brightness
            float adaptiveThreshold = CalculateAdaptiveBloomThreshold(context);
            bloomEffect.Parameters["m"].SetValue(adaptiveThreshold);
            
            // enhanced light scattering parameter
            float scatterStrength = CalculateScatterStrength(context);
            bloomEffect.Parameters["bloomScatter"].SetValue(scatterStrength);
            
            context.SpriteBatch.Draw(context.ScreenTarget, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, 
                ShaderConstants.BLOOM_SCALE, SpriteEffects.None, 0f);
            
            context.SpriteBatch.End();
        }

        private void BlendEnhancedBloomEffect(ShaderContext context, Effect bloomEffect, RenderTarget2D screenTarget)
        {
            context.GraphicsDevice.SetRenderTarget(context.ScreenTarget);
            context.GraphicsDevice.Clear(Color.Transparent);

            context.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive);
            
            bloomEffect.CurrentTechnique.Passes["Blend"].Apply();
            bloomEffect.Parameters["tex0"].SetValue(context.ScreenTargetSwap);
            bloomEffect.Parameters["uScreenResolution"].SetValue(context.ScreenResolution);
            
            // enhanced bloom parameters
            float enhancedP = CalculateBloomPower(context);
            float enhancedM2 = CalculateBloomMultiplier(context);
            
            bloomEffect.Parameters["p"].SetValue(enhancedP);
            bloomEffect.Parameters["m2"].SetValue(enhancedM2);
            
            context.SpriteBatch.Draw(screenTarget, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, 
                ShaderConstants.SCREEN_SCALE_DIVISOR, SpriteEffects.None, 0f);
            
            context.SpriteBatch.End();
        }

        private float CalculateAdaptiveBloomThreshold(ShaderContext context)
        {
            // only recalculate if frame changed (cache for multiple passes)
            if (_lastFrameUpdate != Main.GameUpdateCount)
            {
                _lastFrameUpdate = (int)Main.GameUpdateCount;
                _cachedAdaptiveThreshold = DoCalculateAdaptiveBloomThreshold(context);
            }
            return _cachedAdaptiveThreshold;
        }

        private float DoCalculateAdaptiveBloomThreshold(ShaderContext context)
        {
            // base threshold from configuration
            float baseThreshold = ShaderConstants.BLOOM_BASE_M - TShader.bloomIntensity * ShaderConstants.BLOOM_M_MODIFIER;
            
            // adapt based on time of day
            float timeAdaptation = 1f;
            if (context.IsDayTime)
            {
                // higher threshold during bright day to prevent over-blooming
                float dayProgress = context.GameTime / ShaderConstants.DAY_TIME_TOTAL;
                float noonFactor = 1f - Math.Abs(dayProgress - 0.5f) * 2f; // peaks at noon
                timeAdaptation = 1f + noonFactor * 0.3f;
            }
            else
            {
                // lower threshold at night to enhance light sources
                timeAdaptation = 0.7f;
            }
            
            // adapt based on player location
            var player = context.LocalPlayer;
            float locationAdaptation = 1f;
            
            if (player.ZoneCorrupt || player.ZoneCrimson)
                locationAdaptation = 0.8f; // enhance bloom in dark biomes
            else if (player.ZoneHallow)
                locationAdaptation = 1.2f; // more selective bloom in bright biomes
            else if (player.ZoneSnow)
                locationAdaptation = 1.1f; // account for snow brightness
            
            return baseThreshold * timeAdaptation * locationAdaptation;
        }

        private float CalculateScatterStrength(ShaderContext context)
        {
            // Only recalculate if frame changed
            if (_lastFrameUpdate == Main.GameUpdateCount && _cachedScatterStrength > 0)
                return _cachedScatterStrength;

            float baseScatter = ShaderConstants.BLOOM_SCATTER_STRENGTH;
            
            // enhance scattering during atmospheric conditions
            var player = context.LocalPlayer;
            
            // more scattering in misty/atmospheric biomes
            if (player.ZoneJungle)
                baseScatter *= 1.3f; // humid atmosphere
            else if (player.ZoneSnow)
                baseScatter *= 1.2f; // ice crystals in air
            else if (player.ZoneDesert)
                baseScatter *= 1.1f; // heat distortion
            
            // reduce scattering underground
            if (IsUnderground(player))
                baseScatter *= 0.7f;
                
            // atmospheric scattering based on time
            if (!context.IsDayTime)
            {
                baseScatter *= 1.4f; // more prominent light scattering at night
            }
            
            _cachedScatterStrength = baseScatter * TShader.bloomIntensity;
            return _cachedScatterStrength;
        }

        private float CalculateBloomPower(ShaderContext context)
        {
            // Only recalculate if frame changed
            if (_lastFrameUpdate == Main.GameUpdateCount && _cachedBloomPower > 0)
                return _cachedBloomPower;

            // adaptive power based on scene conditions
            float basePower = ShaderConstants.SCREEN_SCALE_DIVISOR;
            
            // adjust based on overall scene brightness
            var skyColor = Main.ColorOfTheSkies;
            float sceneBrightness = (skyColor.R + skyColor.G + skyColor.B) / (3f * 255f);
            
            // higher power in darker scenes for more dramatic bloom
            float brightnessModifier = 1f + (1f - sceneBrightness) * 0.5f;
            
            _cachedBloomPower = basePower * brightnessModifier;
            return _cachedBloomPower;
        }

        private float CalculateBloomMultiplier(ShaderContext context)
        {
            // only recalculate if frame changed
            if (_lastFrameUpdate == Main.GameUpdateCount && _cachedBloomMultiplier > 0)
                return _cachedBloomMultiplier;

            float baseMultiplier = ShaderConstants.BLOOM_BLEND_MULTIPLIER * TShader.bloomIntensity;
            
            // enhance bloom strength based on number of light sources
            float lightSourceModifier = 1f;
            
            // time-based adjustments
            if (!context.IsDayTime)
            {
                // stronger bloom at night to make lights more prominent
                lightSourceModifier = 1.3f;
            }
            else
            {
                // subtle bloom during day
                lightSourceModifier = 0.8f;
            }
            
            // biome-specific adjustments
            var player = context.LocalPlayer;
            if (player.ZoneCorrupt || player.ZoneCrimson)
            {
                lightSourceModifier *= 1.2f; // enhance light sources in dark biomes
            }
            
            _cachedBloomMultiplier = baseMultiplier * lightSourceModifier;
            return _cachedBloomMultiplier;
        }

        private bool IsUnderground(Player player)
        {
            return player.ZoneRockLayerHeight || player.ZoneDirtLayerHeight || 
                   player.Center.Y > Main.worldSurface * 16.0 + ShaderConstants.SURFACE_OFFSET;
        }
    }
}