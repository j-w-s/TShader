using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using TShader.Core;

namespace TShader.Passes
{
    public class ShadowPass : BaseShaderPass
    {
        public override string Name => "Shadow";
        public override bool IsEnabled => TShader.useLight;

        public ShadowPass(ResourceCache resourceCache) : base(resourceCache) { }

        public override void Execute(ShaderContext context)
        {
            var shadowEffect = ResourceCache.GetEffect("Shadow");
            var screenTarget = ResourceCache.GetRenderTarget("Screen");
            var lightTarget = ResourceCache.GetRenderTarget("Light");

            if (shadowEffect == null || screenTarget == null || lightTarget == null) return;

            // apply shadow effect
            ApplyShadowEffect(context, shadowEffect, screenTarget);

            // combine light and shadow effects
            CombineLightAndShadowEffects(context, screenTarget);

            // final blend passes
            PerformFinalBlendPasses(context, shadowEffect, screenTarget, lightTarget);
        }

        private void ApplyShadowEffect(ShaderContext context, Effect shadowEffect, RenderTarget2D screenTarget)
        {
            SetupRenderTarget(context.GraphicsDevice, screenTarget, context.ScreenTarget);

            context.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive);
            
            shadowEffect.CurrentTechnique.Passes[0].Apply();
            shadowEffect.Parameters["uScreenResolution"].SetValue(context.ScreenResolution);
            shadowEffect.Parameters["m"].SetValue(CalculateShadowIntensity());
            shadowEffect.Parameters["uPos"].SetValue(context.ToScreenCoords(context.SunPosition));

            context.SpriteBatch.Draw(context.ScreenTarget, Vector2.Zero, null, Color.White, 0f, Vector2.Zero,
                ShaderConstants.SHADOW_SCALE, SpriteEffects.None, 0f);
            
            context.SpriteBatch.End();
        }

        private void CombineLightAndShadowEffects(ShaderContext context, RenderTarget2D screenTarget)
        {
            context.GraphicsDevice.SetRenderTarget(context.ScreenTarget);
            context.GraphicsDevice.Clear(Color.Transparent);

            context.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive);

            if (context.IsDayTime)
            {
                ProcessDayTimeEffects(context, screenTarget);
            }
            else
            {
                ProcessNightTimeEffects(context, screenTarget);
            }

            context.SpriteBatch.End();
        }

        private void ProcessDayTimeEffects(ShaderContext context, RenderTarget2D screenTarget)
        {
            var iterations = TShader.quality * 10;
            var sunPos = context.SunPosition / ShaderConstants.SCREEN_SCALE_DIVISOR;

            for (int i = 0; i < iterations; i++)
            {
                var shaderIntensity = CalculateDayShaderIntensity();
                var alpha = (iterations - i) / shaderIntensity;
                var scale = 1f + i * ShaderConstants.SHADOW_ITERATION_SCALE;

                context.SpriteBatch.Draw(screenTarget, sunPos, null, Color.White * alpha, 
                    0f, sunPos, scale, SpriteEffects.None, 0f);
            }
        }

        private void ProcessNightTimeEffects(ShaderContext context, RenderTarget2D screenTarget)
        {
            var sunPos = context.SunPosition / ShaderConstants.SCREEN_SCALE_DIVISOR;

            for (int i = 0; i < ShaderConstants.NIGHT_ITERATIONS; i++)
            {
                var alpha = (ShaderConstants.NIGHT_ITERATIONS - i) / ShaderConstants.NIGHT_SHADER_INTENSITY;
                var scale = 1f + i * ShaderConstants.SHADOW_ITERATION_SCALE;

                context.SpriteBatch.Draw(screenTarget, sunPos, null, Color.White * alpha, 
                    0f, sunPos, scale, SpriteEffects.None, 0f);
            }
        }

        private void PerformFinalBlendPasses(ShaderContext context, Effect shadowEffect, 
            RenderTarget2D screenTarget, RenderTarget2D lightTarget)
        {
            // first pass - copy to screen target
            SetupRenderTarget(context.GraphicsDevice, screenTarget, context.ScreenTarget);
            
            context.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            context.SpriteBatch.Draw(context.ScreenTarget, Vector2.Zero, Color.White);
            context.SpriteBatch.End();

            // second pass - blend with original
            context.GraphicsDevice.SetRenderTarget(context.ScreenTarget);
            context.GraphicsDevice.Clear(Color.Transparent);

            context.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive);
            context.SpriteBatch.Draw(context.ScreenTargetSwap, Vector2.Zero, Color.White);
            context.SpriteBatch.End();

            // final pass - apply shadow effect
            context.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive);
            
            shadowEffect.CurrentTechnique.Passes[1].Apply();
            shadowEffect.Parameters["tex0"].SetValue(screenTarget);
            
            context.SpriteBatch.Draw(lightTarget, Vector2.Zero, Color.White);
            context.SpriteBatch.End();
        }

        private float CalculateShadowIntensity()
        {
            float desertIntensity = Main.bgAlphaFrontLayer[2] * 0.1f;

            if (Main.desertBG[0] == ShaderConstants.DESERT_BG_ID)
                desertIntensity = 0f;

            return Main.dayTime ? ShaderConstants.DAY_SHADOW_BASE - desertIntensity : ShaderConstants.NIGHT_SHADOW_BASE;
        }

        private float CalculateDayShaderIntensity()
        {
            float shaderIntensity = TShader.quality * 18 * (1f + TShader.quality * 0.16f);

            if (ShaderConstants.SNOW_BG_IDS.Contains(Main.snowBG[0]))
                shaderIntensity -= Main.bgAlphaFrontLayer[7] * 30f;

            return shaderIntensity;
        }
    }
}