using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TShader.Core
{
    public class ResourceCache
    {
        private readonly Dictionary<string, Effect> _effects = new Dictionary<string, Effect>();
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, RenderTarget2D> _renderTargets = new Dictionary<string, RenderTarget2D>();
        private bool _renderTargetsInitialized = false;
        private bool _disposed = false;

        public void Initialize()
        {
            if (_disposed) return;

            try
            {
                LoadEffects();
                LoadTextures();
                // don't create render targets here - they need to be on main thread
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<TShader>()?.Logger?.Error($"Error initializing ResourceCache: {ex}");
                throw;
            }
        }

        public void EnsureRenderTargetsInitialized()
        {
            if (_disposed || _renderTargetsInitialized) return;

            try
            {
                CreateRenderTargets();
                _renderTargetsInitialized = true;
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<TShader>()?.Logger?.Error($"Error creating render targets: {ex}");
                throw;
            }
        }

        private void LoadEffects()
        {
            try
            {
                _effects["Light"] = ModContent.Request<Effect>("TShader/Effects/Light", AssetRequestMode.ImmediateLoad).Value;
                _effects["Shadow"] = ModContent.Request<Effect>("TShader/Effects/Shadow", AssetRequestMode.ImmediateLoad).Value;
                _effects["Bloom"] = ModContent.Request<Effect>("TShader/Effects/Bloom", AssetRequestMode.ImmediateLoad).Value;
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<TShader>()?.Logger?.Error($"Error loading effects: {ex}");
                throw;
            }
        }

        private void LoadTextures()
        {
            try
            {
                _textures["ColorTexDay"] = ModContent.Request<Texture2D>("TShader/ColorTexDay", AssetRequestMode.ImmediateLoad).Value;
                _textures["ColorTexNight"] = ModContent.Request<Texture2D>("TShader/ColorTexNight", AssetRequestMode.ImmediateLoad).Value;
                _textures["PixelTex"] = ModContent.Request<Texture2D>("TShader/PixelTex", AssetRequestMode.ImmediateLoad).Value;
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<TShader>()?.Logger?.Error($"Error loading textures: {ex}");
                throw;
            }
        }

        private void CreateRenderTargets()
        {
            if (Main.dedServ) return; // no graphics on server

            var gd = Main.instance.GraphicsDevice;
            var pp = gd.PresentationParameters;

            _renderTargets["Screen"] = new RenderTarget2D(gd, pp.BackBufferWidth / 3, pp.BackBufferHeight / 3, 
                false, pp.BackBufferFormat, DepthFormat.None);
            _renderTargets["Light"] = new RenderTarget2D(gd, pp.BackBufferWidth, pp.BackBufferHeight, 
                false, pp.BackBufferFormat, DepthFormat.None);
            _renderTargets["Bloom"] = new RenderTarget2D(gd, pp.BackBufferWidth / 3, pp.BackBufferHeight / 3, 
                false, pp.BackBufferFormat, DepthFormat.None);
            _renderTargets["Cloud"] = new RenderTarget2D(gd, pp.BackBufferWidth / 3, pp.BackBufferHeight / 3, 
                false, pp.BackBufferFormat, DepthFormat.None);
        }

        public void UpdateRenderTargets(int width, int height)
        {
            if (_disposed || Main.dedServ) return;

            try
            {
                // dispose old render targets safely
                DisposeRenderTargets();

                // create new ones with updated dimensions
                var gd = Main.instance.GraphicsDevice;
                
                _renderTargets["Screen"] = new RenderTarget2D(gd, width / 3, height / 3);
                _renderTargets["Light"] = new RenderTarget2D(gd, width, height);
                _renderTargets["Bloom"] = new RenderTarget2D(gd, width / 3, height / 3);
                _renderTargets["Cloud"] = new RenderTarget2D(gd, width / 3, height / 3);
                
                _renderTargetsInitialized = true;
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<TShader>()?.Logger?.Error($"Error updating render targets: {ex}");
            }
        }

        private void DisposeRenderTargets()
        {
            foreach (var target in _renderTargets.Values)
            {
                try
                {
                    target?.Dispose();
                }
                catch (Exception ex)
                {
                    ModContent.GetInstance<TShader>()?.Logger?.Error($"Error disposing render target: {ex}");
                }
            }
            _renderTargets.Clear();
        }

        public Effect GetEffect(string name) => 
            _disposed ? null : (_effects.GetValueOrDefault(name));
        
        public Texture2D GetTexture(string name) => 
            _disposed ? null : (_textures.GetValueOrDefault(name));
        
        public RenderTarget2D GetRenderTarget(string name) => 
            _disposed ? null : (_renderTargets.GetValueOrDefault(name));

        public void Dispose()
        {
            if (_disposed) return;

            if (Thread.CurrentThread.ManagedThreadId == 1)
            {
                // main thread, dispose directly
                DisposeImmediate();
            }
            else
            {
                Main.RunOnMainThread(DisposeImmediate);
            }
        }

        private void DisposeImmediate()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                // dispose render targets
                DisposeRenderTargets();
                
                // clear other collections
                _effects.Clear();
                _textures.Clear();
                
                _renderTargetsInitialized = false;
            }
            catch (Exception ex)
            {
                ModContent.GetInstance<TShader>()?.Logger?.Error($"Error during immediate disposal: {ex}");
            }
        }
    }
}