using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace taoism.Projectiles
{
    public class ParryGaugeRenderer
    {
        private static Asset<Texture2D> heavenTexture;
        private static Asset<Texture2D> earthTexture;
        private static Asset<Texture2D> skillSlotTexture;
        private static Asset<Texture2D> finisherTexture;
        private static Asset<Texture2D> gaugeTexture;

        private float currentMaxRange = 30f;
        private float visualRange = 30f;
        public float VisualRange => visualRange;
        private Item lastHeldItem;

        private const float BaseRange = 10f;
        private const float MaxPossibleRange = 500f;
        private const float BaseScale = 0.6f;
        private const float ScaleMultiplier = 1f;
        private const float EmaAlpha = 0.3f;

        private bool isCrimson = true;
        private bool numpad5WasPressed = false;

        public ParryGaugeRenderer()
        {
            gaugeTexture = ModContent.Request<Texture2D>("Taoism/Assets/ParryGauge");
            heavenTexture = ModContent.Request<Texture2D>("Taoism/Assets/Heaven");
            earthTexture = ModContent.Request<Texture2D>("Taoism/Assets/Crimson");
            skillSlotTexture = ModContent.Request<Texture2D>("Taoism/Assets/SkillSlot");
            finisherTexture = ModContent.Request<Texture2D>("Taoism/Assets/Finisher");
        }

        public void Update()
        {
            KeyboardState keyboard = Keyboard.GetState();
            bool numpad5Pressed = keyboard.IsKeyDown(Keys.NumPad5);

            if (numpad5Pressed && !numpad5WasPressed)
            {
                isCrimson = !isCrimson;
                string newPath = isCrimson ? "Taoism/Assets/Crimson" : "Taoism/Assets/Corruption";
                earthTexture = ModContent.Request<Texture2D>(newPath);
            }

            numpad5WasPressed = numpad5Pressed;
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            Player player = Main.LocalPlayer;
            if (!player.active || player.dead || gaugeTexture?.Value == null)
                return;

            if (player.HeldItem != lastHeldItem)
            {
                currentMaxRange = BaseRange;
                lastHeldItem = player.HeldItem;
            }

            int animationTime = player.HeldItem.useAnimation > 0 ? player.HeldItem.useAnimation : 20;
            float lerpSpeed = MathHelper.Clamp(1f / animationTime, 0.05f, 0.3f);
            visualRange = MathHelper.Lerp(visualRange, currentMaxRange, lerpSpeed);

            Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
            Vector2 orbitPosition = player.Center + direction * visualRange;
            float rotation = direction.ToRotation();
            float scale = (BaseScale + (player.HeldItem?.active == true
                ? (player.GetAdjustedItemScale(player.HeldItem) - 1f) * 0.5f
                : 0f)) * ScaleMultiplier;

            Color color = new Color(0, 255, 255) * (ShouldDenylistWeapon(player.HeldItem) ? 0.5f : 1f);
            bool facingLeft = direction.X < 0f;

            var oldSamplerState = spriteBatch.GraphicsDevice.SamplerStates[0];
            spriteBatch.GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

            Vector2 screenPos = orbitPosition - Main.screenPosition;
            Vector2 rootOrigin = new Vector2(0, gaugeTexture.Height() / 2f);
            Vector2 RotateOffset(Vector2 localOffset) => screenPos + localOffset.RotatedBy(rotation) * scale;

            spriteBatch.Draw(
                gaugeTexture.Value,
                screenPos,
                null,
                color,
                rotation,
                rootOrigin,
                scale,
                SpriteEffects.None,
                0f
            );

            SpriteEffects verticalFlip = facingLeft ? SpriteEffects.FlipVertically : SpriteEffects.None;

            if (heavenTexture?.Value != null)
            {
                float yOffset = (gaugeTexture.Height() / 2f + heavenTexture.Height() / 2f) * (facingLeft ? 1 : -1);
                Vector2 offset = new Vector2(0, yOffset);

                spriteBatch.Draw(
                    heavenTexture.Value,
                    RotateOffset(offset),
                    null,
                    Color.White,
                    rotation,
                    new Vector2(heavenTexture.Width() / 2f, heavenTexture.Height() / 2f),
                    scale,
                    verticalFlip,
                    0f
                );
            }

            if (earthTexture?.Value != null)
            {
                float yOffset = (gaugeTexture.Height() / 2f + earthTexture.Height() / 2f) * (facingLeft ? -1 : 1);
                Vector2 offset = new Vector2(0, yOffset);

                spriteBatch.Draw(
                    earthTexture.Value,
                    RotateOffset(offset),
                    null,
                    Color.White,
                    rotation,
                    new Vector2(earthTexture.Width() / 2f, earthTexture.Height() / 2f),
                    scale,
                    verticalFlip,
                    0f
                );
            }

            if (skillSlotTexture?.Value != null)
            {
                Vector2 offset = new Vector2(-gaugeTexture.Width() / 2f - skillSlotTexture.Width() / 2f, 0);
                spriteBatch.Draw(
                    skillSlotTexture.Value,
                    RotateOffset(offset),
                    null,
                    color,
                    rotation,
                    new Vector2(skillSlotTexture.Width() / 2f, skillSlotTexture.Height() / 2f),
                    scale,
                    SpriteEffects.None,
                    0f
                );

                if (finisherTexture?.Value != null)
                {
                    Vector2 finisherOffset = new Vector2(
                        -gaugeTexture.Width() / 2f - skillSlotTexture.Width() - finisherTexture.Width() / 2f,
                        0
                    );

                    spriteBatch.Draw(
                        finisherTexture.Value,
                        RotateOffset(finisherOffset),
                        null,
                        color,
                        rotation,
                        new Vector2(finisherTexture.Width() / 2f, finisherTexture.Height() / 2f),
                        scale,
                        SpriteEffects.None,
                        0f
                    );
                }
            }

            spriteBatch.GraphicsDevice.SamplerStates[0] = oldSamplerState;
        }

        public void UpdateWeaponRange(NPC target)
        {
            Player player = Main.LocalPlayer;
            float distanceToTarget = Vector2.Distance(player.Center, target.Center);

            if (ShouldAllowlistWeapon(player.HeldItem))
                distanceToTarget = MathHelper.Clamp(distanceToTarget, BaseRange, MaxPossibleRange);
            else if (ShouldDenylistWeapon(player.HeldItem))
                distanceToTarget = BaseRange;
            else
                distanceToTarget = MathHelper.Clamp(distanceToTarget, BaseRange, MaxPossibleRange);

            currentMaxRange = EmaAlpha * distanceToTarget + (1f - EmaAlpha) * currentMaxRange;
        }

        private bool ShouldAllowlistWeapon(Item item) { /*...*/ return false; }
        private bool ShouldDenylistWeapon(Item item) { /*...*/ return false; }
    }

    public class ParryUiSystem : ModSystem
    {
        private ParryGaugeRenderer renderer;

        public Vector2 GetParryGaugeWorldPosition()
        {
            if (renderer == null)
                return Vector2.Zero;

            Player player = Main.LocalPlayer;
            if (!player.active)
                return Vector2.Zero;

            Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
            float visualRange = renderer.VisualRange;
            return player.Center + direction * visualRange;
        }

        public override void Load()
        {
            if (Main.netMode != NetmodeID.Server)
                renderer = new ParryGaugeRenderer();
        }

        public override void Unload() => renderer = null;

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "Taoism: Parry Gauge",
                    delegate
                    {
                        if (Main.LocalPlayer.active && !Main.LocalPlayer.dead)
                        {
                            renderer?.Update();
                            renderer?.Draw(Main.spriteBatch);
                        }
                        return true;
                    }));
            }
        }

        public void UpdateParryRange(NPC target) => renderer?.UpdateWeaponRange(target);
    }
}
