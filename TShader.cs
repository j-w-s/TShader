using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;
using TShader.Core;

namespace TShader
{
    public class TShader : Mod
    {
        public static bool useLight = true;
        public static bool useBloom = true;
        public static float lightIntensity = 1.05f;
        public static float shadowIntensity = 1f;
        public static float bloomIntensity = 0.75f;
        public static float moonLightIntensity = 0.85f;
        public static int quality = 32;  

        // legacy render target access
        public RenderTarget2D screen => _resourceCache?.GetRenderTarget("Screen");
        public RenderTarget2D light => _resourceCache?.GetRenderTarget("Light");
        public RenderTarget2D bloom => _resourceCache?.GetRenderTarget("Bloom");
        public RenderTarget2D cloud => _resourceCache?.GetRenderTarget("Cloud");

        // legacy effect access
        public static Effect Light => ModContent.GetInstance<TShader>()?._resourceCache?.GetEffect("Light");
        public static Effect Shadow => ModContent.GetInstance<TShader>()?._resourceCache?.GetEffect("Shadow");
        public static Effect Bloom => ModContent.GetInstance<TShader>()?._resourceCache?.GetEffect("Bloom");

        // core system components
        private ResourceCache _resourceCache;
        private ShaderManager _shaderManager;

        public override void Load()
        {
            if (Main.dedServ) return;

            try
            {
                InitializeSystems();
                RegisterEventHandlers();
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error during Load: {ex}");
                throw; 
            }
        }

        public override void PostSetupContent()
        {
            if (Main.dedServ || _resourceCache == null) return;
            
            try
            {
                _resourceCache.Initialize();
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error during PostSetupContent: {ex}");
            }
        }

        public void UnloadContent()
        {
            try
            {
                UnregisterEventHandlers();
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error unregistering event handlers: {ex}");
            }

            try
            {
                DisposeSystems();
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error disposing systems: {ex}");
            }
        }

        public override void Unload()
        {
            _shaderManager = null;
            _resourceCache = null;
            
            useLight = true;
            useBloom = true;
            lightIntensity = 1f;
            shadowIntensity = 1f;
            bloomIntensity = 0.5f;
            moonLightIntensity = 1f;
            quality = 3;
        }

        private void InitializeSystems()
        {
            _resourceCache = new ResourceCache();
            _shaderManager = new ShaderManager();
            
            // only load effects and textures here - render targets will be created on main thread
            _resourceCache.Initialize();
            _shaderManager.Initialize(_resourceCache);
        }

        private void RegisterEventHandlers()
        {
            try
            {
                On_FilterManager.EndCapture += FilterManager_EndCapture;
                On_Main.LoadWorlds += Main_LoadWorlds;
                Main.OnResolutionChanged += Main_OnResolutionChanged;
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error registering event handlers: {ex}");
                throw;
            }
        }

        private void UnregisterEventHandlers()
        {
            try
            {
                On_FilterManager.EndCapture -= FilterManager_EndCapture;
                On_Main.LoadWorlds -= Main_LoadWorlds;
                Main.OnResolutionChanged -= Main_OnResolutionChanged;
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error unregistering event handlers: {ex}");
            }
        }

        private void DisposeSystems()
        {
            // dispose shader manager first (safer)
            try
            {
                _shaderManager?.Dispose();
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error disposing shader manager: {ex}");
            }
            finally
            {
                _shaderManager = null;
            }

            // dispose resource cache with thread safety
            try
            {
                _resourceCache?.Dispose();
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error disposing resource cache: {ex}");
            }
            finally
            {
                _resourceCache = null;
            }
        }

        private void Main_LoadWorlds(On_Main.orig_LoadWorlds orig)
        {
            orig();
            
            try
            {
                // init render targets now that we're on the main thread
                if (_resourceCache != null)
                {
                    _resourceCache.EnsureRenderTargetsInitialized();
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error initializing render targets: {ex}");
            }
        }

        private void Main_OnResolutionChanged(Vector2 newResolution)
        {
            try
            {
                _resourceCache?.UpdateRenderTargets((int)newResolution.X, (int)newResolution.Y);
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error updating render targets: {ex}");
            }
        }

        private void FilterManager_EndCapture(On_FilterManager.orig_EndCapture orig, FilterManager self,
            RenderTarget2D finalTexture, RenderTarget2D screenTarget1, RenderTarget2D screenTarget2, Color clearColor)
        {
            try
            {
                // only execute shader pipeline if we have a valid player and systems are initialized
                if (Main.myPlayer >= 0 && _shaderManager != null && _resourceCache != null)
                {
                    // ensure render targets exist (we're definitely on main thread here)
                    _resourceCache.EnsureRenderTargetsInitialized();

                    // exec the shader pipeline
                    _shaderManager.ExecuteShaderPipeline(Main.instance.GraphicsDevice, Main.spriteBatch, 
                        screenTarget1, screenTarget2);
                }
            }
            catch (System.Exception ex)
            {
                Logger.Error($"Error in shader pipeline: {ex}");
                // continue to original method even if shader fails
            }
            finally
            {
                orig.Invoke(self, finalTexture, screenTarget1, screenTarget2, clearColor);
            }
        }
    }
}