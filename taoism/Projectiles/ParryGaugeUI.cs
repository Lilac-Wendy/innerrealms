using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.ID;

namespace Taoism.UI
{
    public class ParryGaugeRenderer
    {
        private static Asset<Texture2D> gaugeTexture;
        private float currentMaxRange = 30f;
        private float visualRange = 30f;
        private Item lastHeldItem;
        private const float BaseRange = 10f;
        private const float MaxPossibleRange = 500f;
        private const string TexturePath = "Taoism/Assets/ParryGauge";
        private const float BaseScale = 1f;
        private const float ScaleMultiplier = 1f;
        private const float EmaAlpha = 0.3f; // Quanto maior, mais responsivo (ideal: 0.1~0.3)

        public ParryGaugeRenderer()
        {
            gaugeTexture = ModContent.Request<Texture2D>(TexturePath);
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

            visualRange = MathHelper.Lerp(visualRange, currentMaxRange, 0.1f);

            Vector2 direction = Vector2.Normalize(Main.MouseWorld - player.Center);
            Vector2 orbitPosition = player.Center + direction * visualRange;

            float scale = (BaseScale + (player.HeldItem?.active == true
                ? (player.GetAdjustedItemScale(player.HeldItem) - 1f) * 0.5f
                : 0f)) * ScaleMultiplier;

            Color color = new Color(0, 255, 255) * (ShouldBlacklistWeapon(player.HeldItem) ? 0.5f : 1f);

            // Desenho direto no mundo
            var oldSamplerState = spriteBatch.GraphicsDevice.SamplerStates[0];
            spriteBatch.GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;

            spriteBatch.Draw(
                gaugeTexture.Value,
                orbitPosition - Main.screenPosition,
                null,
                color,
                direction.ToRotation(),
                new Vector2(0, gaugeTexture.Height() / 2f),
                scale,
                SpriteEffects.None,
                0f
            );

            spriteBatch.GraphicsDevice.SamplerStates[0] = oldSamplerState;
        }
        public void UpdateWeaponRange(NPC target)
        {
            Player player = Main.LocalPlayer;
            float distanceToTarget = Vector2.Distance(player.Center, target.Center);

            if (ShouldWhitelistWeapon(player.HeldItem))
            {
                // Armas especiais (yo-yo, flail, spear, etc.) sempre usam a distância real suavizada
                distanceToTarget = MathHelper.Clamp(distanceToTarget, BaseRange, MaxPossibleRange);
            }
            else if (ShouldBlacklistWeapon(player.HeldItem))
            {
                // Armas banidas forçam a distância base fixa
                distanceToTarget = BaseRange;
            }
            else
            {
                // Armas normais (espadas etc.)
                distanceToTarget = MathHelper.Clamp(distanceToTarget, BaseRange, MaxPossibleRange);
            }

            // Aplicação da EMA (Exponential Moving Average)
            currentMaxRange = EmaAlpha * distanceToTarget + (1f - EmaAlpha) * currentMaxRange;
        }

private bool ShouldWhitelistWeapon(Item item)
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
        // Permitir qualquer tipo de dano que não seja dos banidos explicitamente
        if (!item.DamageType.CountsAsClass(DamageClass.Ranged) &&
            !item.DamageType.CountsAsClass(DamageClass.Magic) &&
            !item.DamageType.CountsAsClass(DamageClass.Summon))
        {
            return true;
        }

        // E ainda checa se o projétil tem um AI estilo que seria permitido
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

private bool ShouldBlacklistWeapon(Item item)
{
    if (item == null || item.IsAir)
        return true;
    if (ShouldWhitelistWeapon(item))
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


    public class ParryUISystem : ModSystem
{
    private ParryGaugeRenderer renderer;

    public override void Load()
    {
        if (Main.netMode != NetmodeID.Server)
        {
            renderer = new ParryGaugeRenderer();
        }
    }

    public override void Unload()
    {
        renderer = null;
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
                            renderer?.Draw(Main.spriteBatch);
                        }
                        return true;
                    },
                    InterfaceScaleType.Game) 
            );
        }
    }

    public void UpdateParryRange(NPC target)
    {
        renderer?.UpdateWeaponRange(target);
    }
}

}
