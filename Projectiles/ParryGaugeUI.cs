using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace taoism.Projectiles;

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

    private int stanceIndex = 0;
    private const int MaxStances = 5;

    public enum ElementalStance
    {
        Fire,
        Earth,
        Iron,
        Water,
        Wood
    }

    public ElementalStance CurrentStance => (ElementalStance)stanceIndex;

    public ParryGaugeRenderer()
        
    {
        gaugeTexture = ModContent.Request<Texture2D>("Taoism/Assets/ParryGauge");
        heavenTexture = ModContent.Request<Texture2D>("Taoism/Assets/Heaven");
        earthTexture = ModContent.Request<Texture2D>("Taoism/Assets/Crimson");
        skillSlotTexture = ModContent.Request<Texture2D>("Taoism/Assets/SkillSlot");
        finisherTexture = ModContent.Request<Texture2D>("Taoism/Assets/Finisher");
    }

    public void AdvanceStance()
    {
        stanceIndex = (stanceIndex - 1 + MaxStances) % MaxStances;
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

        int frameIndex = stanceIndex;
        Rectangle sourceRect = new Rectangle(0, frameIndex * 56, 64, 56);

        Color color = ShouldDenylistWeapon(player.HeldItem) ? 
            Color.White * 0.5f : // Transparência para armas inválidas
            Color.White;
        bool facingLeft = direction.X < 0f;

        var oldSamplerState = spriteBatch.GraphicsDevice.SamplerStates[0];
        spriteBatch.GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

        Vector2 screenPos = orbitPosition - Main.screenPosition;
        Vector2 rootOrigin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);
        Vector2 RotateOffset(Vector2 localOffset) => screenPos + localOffset.RotatedBy(rotation) * scale;

        spriteBatch.Draw(
            gaugeTexture.Value,
            screenPos,
            sourceRect,
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
            float yOffset = (sourceRect.Height / 2f + heavenTexture.Height() / 2f) * (facingLeft ? 1 : -1);
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
            float yOffset = (sourceRect.Height / 2f + earthTexture.Height() / 2f) * (facingLeft ? -1 : 1);
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
            Vector2 offset = new Vector2(-sourceRect.Width / 2f - skillSlotTexture.Width() / 2f, 0);
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
                    -sourceRect.Width / 2f - skillSlotTexture.Width() - finisherTexture.Width() / 2f,
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

        
    private bool ShouldAllowlistWeapon(Item item)
    {
        if (item == null || item.IsAir)
            return false;

        bool isModded = item.ModItem != null;

        if (!isModded)
        {
            // === Vanilla logic ===
            if (item.DamageType == DamageClass.SummonMeleeSpeed)
                return true;

            if (item.shoot > ProjectileID.None && 
                ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out var proj))
            {
                if (proj.aiStyle == ProjAIStyleID.Yoyo ||
                    proj.aiStyle == ProjAIStyleID.Flail ||
                    proj.aiStyle == ProjAIStyleID.Spear ||
                    proj.aiStyle == ProjAIStyleID.Boomerang ||
                    proj.aiStyle == ProjAIStyleID.Harpoon ||
                    proj.aiStyle == ProjAIStyleID.Drill ||
                    proj.aiStyle == ProjAIStyleID.ShortSword)
                {
                    return true;
                }
            }
            return false;
        }
        else
        {
            // === Modded logic ===
            if (!item.DamageType.CountsAsClass(DamageClass.Ranged) &&
                !item.DamageType.CountsAsClass(DamageClass.Magic) &&
                !item.DamageType.CountsAsClass(DamageClass.Summon))
            {
                return true;
            }

            if (item.shoot > ProjectileID.None && 
                ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out var proj))
            {
                if (proj.aiStyle == ProjAIStyleID.Yoyo ||
                    proj.aiStyle == ProjAIStyleID.Flail ||
                    proj.aiStyle == ProjAIStyleID.Spear ||
                    proj.aiStyle == ProjAIStyleID.Boomerang ||
                    proj.aiStyle == ProjAIStyleID.Harpoon ||
                    proj.aiStyle == ProjAIStyleID.Drill ||
                    proj.aiStyle == ProjAIStyleID.ShortSword)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public bool ShouldDenylistWeapon(Item item)
    {
        if (item == null || item.IsAir)
            return true;
        
        if (ShouldAllowlistWeapon(item))
            return false;

        bool isModded = item.ModItem != null;

        if (!isModded)
        {
            // === Vanilla logic ===
            return item.noMelee ||
                   item.DamageType == DamageClass.Ranged ||
                   item.DamageType == DamageClass.Magic ||
                   item.DamageType == DamageClass.Summon ||
                   (item.shoot > ProjectileID.None);
        }
        else
        {
            // === Modded logic ===
            if (item.DamageType.CountsAsClass(DamageClass.Ranged) ||
                item.DamageType.CountsAsClass(DamageClass.Magic) ||
                item.DamageType.CountsAsClass(DamageClass.Summon))
                return true;

            if (item.noUseGraphic || item.noMelee)
                return true;

            if (item.shoot > ProjectileID.None &&
                (!ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out var proj) ||
                 proj.aiStyle == 0 || proj.aiStyle == ProjAIStyleID.MagicMissile))
                return true;

            return false;
        }
    }
}

public class ParryUiSystem : ModSystem
{
    private static ParryGaugeRenderer renderer;
    private static ParryUiSystem instance;

    public Vector2 GetParryGaugeWorldPosition()
    {
        if (renderer == null) return Vector2.Zero;
        Player player = Main.LocalPlayer;
        if (!player.active) return Vector2.Zero;
        Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
        float visualRange = renderer.VisualRange;
        return player.Center + direction * visualRange;
    }

    public static void AdvanceStanceUI()
    {
        renderer?.AdvanceStance();
    }

    public override void Load()
    {
        if (Main.netMode != NetmodeID.Server)
        {
            renderer = new ParryGaugeRenderer();
            instance = this;
        }
    }

    public override void Unload()
    {
        renderer = null;
        instance = null;
    }

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