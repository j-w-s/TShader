using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;
using TShader.Core;
using TShader.Passes;

namespace TShader.Core
{
    public class ShaderManager
    {
        private readonly List<IShaderPass> _passes = new List<IShaderPass>();
        private ResourceCache _resourceCache;
        
        public void Initialize(ResourceCache resourceCache)
        {
            _resourceCache = resourceCache;
            
            // init passes in order
            _passes.Add(new BloomPass(_resourceCache));
            _passes.Add(new LightingPass(_resourceCache));
            _passes.Add(new ShadowPass(_resourceCache));
        }

        public void ExecuteShaderPipeline(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, 
            RenderTarget2D screenTarget, RenderTarget2D screenTargetSwap)
        {
            var context = new ShaderContext
            {
                GraphicsDevice = graphicsDevice,
                SpriteBatch = spriteBatch,
                ScreenTarget = screenTarget,
                ScreenTargetSwap = screenTargetSwap,
                ScreenResolution = new Vector2(Main.screenWidth, Main.screenHeight),
                SunPosition = CalculateSunPosition(),
                IsDayTime = Main.dayTime,
                GameTime = (float)Main.time,
                LocalPlayer = Main.LocalPlayer
            };

            foreach (var pass in _passes.Where(p => p.IsEnabled))
            {
                pass.Execute(context);
            }
        }

        private Vector2 CalculateSunPosition()
        {
            // re-used from dynamic lights mod
            float bgTop = (int)((0.0 - Main.screenPosition.Y) / (Main.worldSurface * 16.0 - 600.0) * 200.0);
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
            
            return new Vector2((float)Main.time / (Main.dayTime ? 54000.0f : 32400.0f) * (Main.screenWidth + 200f) - 100f, 
                height + Main.sunModY);
        }

        public void Dispose()
        {
            foreach (var pass in _passes)
            {
                pass?.Dispose();
            }
            _passes.Clear();
        }
    }

    public class ShaderContext
    {
        public GraphicsDevice GraphicsDevice { get; set; }
        public SpriteBatch SpriteBatch { get; set; }
        public RenderTarget2D ScreenTarget { get; set; }
        public RenderTarget2D ScreenTargetSwap { get; set; }
        public Vector2 ScreenResolution { get; set; }
        public Vector2 SunPosition { get; set; }
        public bool IsDayTime { get; set; }
        public float GameTime { get; set; }
        public Player LocalPlayer { get; set; }

        public Vector2 ToScreenCoords(Vector2 worldPos) => worldPos / ScreenResolution;
    }

    public interface IShaderPass
    {
        string Name { get; }
        bool IsEnabled { get; }
        void Execute(ShaderContext context);
        void Dispose();
    }

    public abstract class BaseShaderPass : IShaderPass
    {
        protected ResourceCache ResourceCache { get; }

        public abstract string Name { get; }
        public virtual bool IsEnabled => true;

        protected BaseShaderPass(ResourceCache resourceCache)
        {
            ResourceCache = resourceCache;
        }

        public abstract void Execute(ShaderContext context);
        public virtual void Dispose() { }

        protected void SetupRenderTarget(GraphicsDevice gd, RenderTarget2D target, RenderTarget2D source)
        {
            gd.SetRenderTarget(target);
            gd.Clear(Color.Transparent);
        }

        protected void SwapToScreenTargetHelper(ShaderContext context)
        {
            context.GraphicsDevice.SetRenderTarget(context.ScreenTargetSwap);
            context.GraphicsDevice.Clear(Color.Transparent);
            
            context.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
            context.SpriteBatch.Draw(context.ScreenTarget, Vector2.Zero, Color.White);
            context.SpriteBatch.End();
        }
    }
}