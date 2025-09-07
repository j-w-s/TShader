using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using TShader.Core;

namespace TShader.Passes
{
    public class LightingPass : BaseShaderPass
    {
        public override string Name => "Lighting";
        public override bool IsEnabled => TShader.useLight;

        private readonly Dictionary<int, LightSourceData> _lightSources = new Dictionary<int, LightSourceData>();
        private Vector4 _lightPos; // xy=light0, zw=light1
        private Vector4 _lightData; // simplified: just intensity values for now
        private int _activeLightCount = 0;
        
        // cache to reduce allocations
        private Rectangle _screenBounds;
        private readonly List<Item> _tempItemList = new List<Item>(20);
        
        private static readonly Dictionary<int, Vector3> ItemLightColors = new Dictionary<int, Vector3>
        {
            { ItemID.Torch, new Vector3(1.0f, 0.95f, 0.8f) },
            { ItemID.CursedTorch, new Vector3(0.5f, 1.0f, 0.5f) },
            { ItemID.IchorTorch, new Vector3(1.0f, 1.0f, 0.5f) },
            { ItemID.CoralTorch, new Vector3(0.8f, 0.4f, 1.0f) },
            { ItemID.Candle, new Vector3(1.0f, 0.9f, 0.7f) },
            { ItemID.Glowstick, new Vector3(0.7f, 1.0f, 0.7f) },
            { ItemID.StickyGlowstick, new Vector3(0.7f, 1.0f, 0.7f) },
            { ItemID.BouncyGlowstick, new Vector3(0.7f, 1.0f, 0.7f) },
            { ItemID.FairyGlowstick, new Vector3(1.0f, 0.7f, 1.0f) },
            { ItemID.SpelunkerGlowstick, new Vector3(1.0f, 1.0f, 0.7f) },
            { ItemID.MiningHelmet, new Vector3(1.0f, 1.0f, 0.9f) },
            { ItemID.JellyfishNecklace, new Vector3(0.6f, 0.8f, 1.0f) },
            { ItemID.Magiluminescence, new Vector3(0.9f, 0.7f, 1.0f) },
            { ItemID.Flare, new Vector3(1.0f, 0.5f, 0.5f) },
            { ItemID.BlueFlare, new Vector3(1.0f, 0.5f, 0.5f) },
            { ItemID.HeartLantern, new Vector3(1.0f, 0.8f, 0.8f) },
            { ItemID.SkullLantern, new Vector3(0.9f, 0.9f, 0.7f) },
            { ItemID.JackOLantern, new Vector3(1.0f, 0.7f, 0.3f) },
            { ItemID.DiscoBall, new Vector3(0.8f, 0.8f, 1.0f) },
            { ItemID.LavaLamp, new Vector3(1.0f, 0.6f, 0.8f) }
        };
        
        private static readonly Dictionary<int, float> ItemLightIntensities = new Dictionary<int, float>
        {
            { ItemID.Torch, 0.85f },
            { ItemID.CursedTorch, 0.9f },
            { ItemID.IchorTorch, 0.9f },
            { ItemID.CoralTorch, 0.8f },
            { ItemID.Candle, 0.6f },
            { ItemID.Glowstick, 0.7f },
            { ItemID.MiningHelmet, 1.0f },
            { ItemID.JellyfishNecklace, 0.8f },
            { ItemID.Magiluminescence, 1.2f },
            { ItemID.Flare, 1.1f },
            { ItemID.BlueFlare, 1.1f },
            { ItemID.HeartLantern, 0.9f },
            { ItemID.DiscoBall, 1.0f },
            { ItemID.LavaLamp, 0.8f }
        };

        public LightingPass(ResourceCache resourceCache) : base(resourceCache) { }

        public override void Execute(ShaderContext context)
        {
            var lightTarget = ResourceCache.GetRenderTarget("Light");
            var lightEffect = ResourceCache.GetEffect("Light");

            if (lightTarget == null || lightEffect == null) return;

            // update dynamic light sources
            UpdateDynamicLights(context);

            // prepare render targets
            SwapToScreenTargetHelper(context);
            SetupRenderTarget(context.GraphicsDevice, lightTarget, context.ScreenTarget);

            // apply lighting effect
            ApplyLightingEffect(context, lightEffect);
        }

        private void UpdateDynamicLights(ShaderContext context)
        {
            _activeLightCount = 0;
            _lightSources.Clear();

            var player = context.LocalPlayer;
            var screenBounds = GetScreenBounds(player);

            // scan for light-emitting items in player inventory
            ScanPlayerLights(player, screenBounds);

            // scan for placed light sources (torches, lanterns, etc.)
            ScanWorldLights(screenBounds);

            // convert to shader arrays
            PackLightData();
        }

        private Rectangle GetScreenBounds(Player player)
        {
            // reuse existing rect to avoid allocation
            int padding = 400;
            int leftTile = (int)((Main.screenPosition.X) / 16f) - padding;
            int rightTile = (int)((Main.screenPosition.X + Main.screenWidth) / 16f) + padding;
            int topTile = (int)((Main.screenPosition.Y) / 16f) - padding;
            int bottomTile = (int)((Main.screenPosition.Y + Main.screenHeight) / 16f) + padding;

            _screenBounds.X = leftTile;
            _screenBounds.Y = topTile;
            _screenBounds.Width = rightTile - leftTile;
            _screenBounds.Height = bottomTile - topTile;
            
            return _screenBounds;
        }

        private void ScanPlayerLights(Player player, Rectangle bounds)
        {
            // check held items
            if (IsLightSource(player.HeldItem))
            {
                AddLightSource(player.Center, GetLightColor(player.HeldItem.type), GetLightIntensity(player.HeldItem.type));
            }

            // use pre-allocated temp list to avoid allocations
            _tempItemList.Clear();
            for (int i = 0; i < player.armor.Length; i++)
                _tempItemList.Add(player.armor[i]);
            for (int i = 0; i < player.miscEquips.Length; i++)
                _tempItemList.Add(player.miscEquips[i]);

            for (int i = 0; i < _tempItemList.Count && _activeLightCount < 2; i++)
            {
                var item = _tempItemList[i];
                if (IsLightSource(item))
                {
                    AddLightSource(player.Center, GetLightColor(item.type), GetLightIntensity(item.type));
                }
            }
        }

        private void ScanWorldLights(Rectangle bounds)
        {
            // search in expanding squares from player position for better cache locality
            var player = Main.LocalPlayer;
            int playerTileX = (int)(player.Center.X / 16f);
            int playerTileY = (int)(player.Center.Y / 16f);
            
            for (int radius = 0; radius < 25 && _activeLightCount < 2; radius++)
            {
                // check perimeter of current radius
                for (int dx = -radius; dx <= radius && _activeLightCount < 2; dx++)
                {
                    for (int dy = -radius; dy <= radius && _activeLightCount < 2; dy++)
                    {
                        // only check perimeter
                        if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
                        
                        int x = playerTileX + dx;
                        int y = playerTileY + dy;
                        
                        if (!WorldGen.InWorld(x, y)) continue;

                        var tile = Main.tile[x, y];
                        if (tile == null || !tile.HasTile) continue;

                        if (IsTileLightSource(tile.TileType))
                        {
                            var worldPos = new Vector2(x * 16f + 8f, y * 16f + 8f);
                            
                            // Distance culling - don't add very distant lights
                            float distanceSq = Vector2.DistanceSquared(worldPos, player.Center);
                            if (distanceSq < 800 * 800) // ~50 tile radius
                            {
                                AddLightSource(worldPos, GetTileLightColor(tile.TileType), GetTileLightIntensity(tile.TileType));
                            }
                        }
                    }
                }
            }
        }

        private bool IsLightSource(Item item)
        {
            if (item == null || item.IsAir) return false;
            
            return ItemLightColors.ContainsKey(item.type) ||
                   item.type == ItemID.FlareGun ||
                   item.type == ItemID.HeartLantern || item.type == ItemID.SkullLantern ||
                   item.type == ItemID.JackOLantern || item.type == ItemID.DiscoBall ||
                   item.type == ItemID.LavaLamp || item.type == ItemID.MagicLantern ||
                   item.type == ItemID.FairyBell;
        }

        private bool IsTileLightSource(ushort tileType)
        {
            // common light-emitting tiles
            return tileType == TileID.Torches || tileType == TileID.Candles || 
                   tileType == TileID.Chandeliers || tileType == TileID.Candelabras ||
                   tileType == TileID.Furnaces || tileType == TileID.Hellforge ||
                   tileType == TileID.AdamantiteForge ||
                   tileType == TileID.Campfire || tileType == TileID.FireflyinaBottle ||
                   tileType == TileID.LightningBuginaBottle || tileType == TileID.Lamps ||
                   tileType == TileID.ChineseLanterns ||
                   tileType == TileID.SkullLanterns;
        }

        private Vector3 GetLightColor(int itemType)
        {
            return ItemLightColors.TryGetValue(itemType, out var color) 
                ? color 
                : new Vector3(1.0f, 0.95f, 0.8f); // default
        }

        private Vector3 GetTileLightColor(ushort tileType)
        {
            return tileType switch
            {
                TileID.Torches => new Vector3(1.0f, 0.95f, 0.8f),
                TileID.Candles => new Vector3(1.0f, 0.9f, 0.7f),
                TileID.Chandeliers => new Vector3(1.0f, 0.95f, 0.85f),
                TileID.Candelabras => new Vector3(1.0f, 0.9f, 0.75f),
                TileID.Furnaces => new Vector3(1.0f, 0.6f, 0.4f),
                TileID.Hellforge => new Vector3(1.0f, 0.5f, 0.3f),
                TileID.AdamantiteForge => new Vector3(0.8f, 1.0f, 0.8f),
                TileID.Campfire => new Vector3(1.0f, 0.8f, 0.5f),
                TileID.FireflyinaBottle => new Vector3(0.9f, 1.0f, 0.7f),
                TileID.LightningBuginaBottle => new Vector3(0.7f, 0.9f, 1.0f),
                TileID.Lamps => new Vector3(1.0f, 1.0f, 0.95f),
                TileID.ChineseLanterns => new Vector3(1.0f, 0.7f, 0.5f),
                TileID.SkullLanterns => new Vector3(0.9f, 0.9f, 0.7f),
                TileID.Heart => new Vector3(1.0f, 0.8f, 0.8f),
                _ => new Vector3(1.0f, 0.95f, 0.8f)
            };
        }

        private float GetLightIntensity(int itemType)
        {
            return ItemLightIntensities.TryGetValue(itemType, out var intensity)
                ? intensity
                : 0.8f; // default
        }

        private float GetTileLightIntensity(ushort tileType)
        {
            return tileType switch
            {
                TileID.Torches => 0.85f,
                TileID.Candles => 0.6f,
                TileID.Chandeliers => 1.2f,
                TileID.Candelabras => 1.0f,
                TileID.Furnaces => 1.0f,
                TileID.Hellforge => 1.1f,
                TileID.Campfire => 0.9f,
                TileID.Lamps => 1.0f,
                TileID.Heart => 0.8f,
                _ => 0.8f
            };
        }

        private void AddLightSource(Vector2 worldPos, Vector3 color, float intensity)
        {
            if (_activeLightCount >= 2) return; // limit to 2 lights for ps_2_0 instruction constraints

            var screenPos = (worldPos - Main.screenPosition) / new Vector2(Main.screenWidth, Main.screenHeight);
            _lightSources[_activeLightCount] = new LightSourceData
            {
                WorldPosition = worldPos,
                ScreenPosition = screenPos,
                Color = color,
                Intensity = intensity
            };
            _activeLightCount++;
        }

        private void PackLightData()
        {
            // reset data
            _lightPos = Vector4.Zero;
            _lightData = Vector4.Zero;

            // pack light data in ultra-compact format
            if (_activeLightCount > 0)
            {
                var light0 = _lightSources[0];
                _lightPos.X = light0.ScreenPosition.X;
                _lightPos.Y = light0.ScreenPosition.Y;
                _lightData.X = light0.Intensity;
            }
            
            if (_activeLightCount > 1)
            {
                var light1 = _lightSources[1];
                _lightPos.Z = light1.ScreenPosition.X;
                _lightPos.W = light1.ScreenPosition.Y;
                _lightData.Y = light1.Intensity;
            }
        }

        private void ApplyLightingEffect(ShaderContext context, Effect lightEffect)
        {
            context.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.NonPremultiplied);

            var colorTexture = context.IsDayTime 
                ? ResourceCache.GetTexture("ColorTexDay") 
                : ResourceCache.GetTexture("ColorTexNight");

            lightEffect.CurrentTechnique.Passes["Light"].Apply();
            lightEffect.Parameters["uScreenResolution"].SetValue(context.ScreenResolution);
            lightEffect.Parameters["uPos"].SetValue(context.ToScreenCoords(context.SunPosition));
            lightEffect.Parameters["tex0"].SetValue(colorTexture);

            // pass ultra-compact dynamic light data to shader
            if (_activeLightCount > 0)
            {
                lightEffect.Parameters["uLightCount"]?.SetValue(_activeLightCount);
                lightEffect.Parameters["uLightPos"]?.SetValue(_lightPos);
                lightEffect.Parameters["uLightData"]?.SetValue(_lightData);
            }
            else
            {
                lightEffect.Parameters["uLightCount"]?.SetValue(0);
            }

            var intensity = CalculateIntensity(context);
            var moonIntensity = CalculateMoonlightIntensity();
            var finalIntensity = CalculateFinalIntensity(context, intensity, moonIntensity);

            lightEffect.Parameters["intensity"].SetValue(finalIntensity);
            lightEffect.Parameters["t"].SetValue(context.IsDayTime 
                ? context.GameTime / ShaderConstants.DAY_TIME_TOTAL 
                : context.GameTime / ShaderConstants.NIGHT_TIME_TOTAL);

            if (IsAboveSurface(context.LocalPlayer))
            {
                var pixelTexture = ResourceCache.GetTexture("PixelTex");
                var screenRect = new Rectangle(0, 0, (int)context.ScreenResolution.X, (int)context.ScreenResolution.Y);
                context.SpriteBatch.Draw(pixelTexture, screenRect, Color.White);
            }

            context.SpriteBatch.End();
        }

        private float CalculateIntensity(ShaderContext context)
        {
            var skyColor = Main.ColorOfTheSkies;
            float intensity = 1f - 1.2f * (skyColor.R * 0.3f + skyColor.G * 0.6f + skyColor.B * 0.1f) / 255f;

            var player = context.LocalPlayer;

            // zone-based adjustments
            if (player.ZoneSnow && !player.ZoneCrimson && !player.ZoneCorrupt)
                intensity -= Main.bgAlphaFrontLayer[7] * 0.1f;

            if (player.ZoneCrimson)
                intensity += 0.2f;

            // background-specific adjustments
            if (ShaderConstants.SNOW_BG_IDS.Contains(Main.snowBG[0]))
                intensity -= Main.bgAlphaFrontLayer[7] * 1f;

            if (Main.desertBG[0] == ShaderConstants.DESERT_BG_ID)
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
                _ => 1f
            };
        }

        private float CalculateFinalIntensity(ShaderContext context, float baseIntensity, float moonIntensity)
        {
            if (context.IsDayTime)
            {
                return TShader.lightIntensity * (ShaderConstants.BASE_LIGHT_INTENSITY + 
                    baseIntensity * ShaderConstants.LIGHT_INTENSITY_MULTIPLIER);
            }
            else
            {
                return TShader.moonLightIntensity * moonIntensity * moonIntensity;
            }
        }

        private bool IsAboveSurface(Player player)
        {
            return player.Center.Y < Main.worldSurface * 16.0 + ShaderConstants.SURFACE_OFFSET;
        }
    }

    // data structure for tracking light sources
    public struct LightSourceData
    {
        public Vector2 WorldPosition;
        public Vector2 ScreenPosition;
        public Vector3 Color;
        public float Intensity;
    }
}