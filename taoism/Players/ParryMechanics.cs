using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Input;

namespace Taoism.Players
{
    public class ParryMechanics
    {
        private const int ParryDurationTicks = 30; // 0.5s on 60 FPS
        private const float DefaultAllowedRadius = 20f;

        private int parryTimer;
        private bool parryActive;
        private NPC trackedTarget;

        public void TryStartParry(NPC target, Vector2 parryGaugeWorldPosition, float allowedRadius = DefaultAllowedRadius)
        {
            if (target == null || !target.active || target.friendly || parryActive)
                return;

            float distance = Vector2.Distance(target.Center, parryGaugeWorldPosition);
            if (distance > allowedRadius)
            {
                Main.NewText("Parry failed (initial misalignment)", Color.OrangeRed); // Debug
                return;
            }

            trackedTarget = target;
            parryActive = true;
            parryTimer = 0;

            Main.NewText("Parry is possible!", Color.LightGreen);
        }

        /// <summary>
        /// Called each tick by the player
        /// </summary>
        public void Update(Player player, Vector2 parryGaugeWorldPosition, float allowedRadius = DefaultAllowedRadius)
        {
            if (!parryActive || trackedTarget == null || !trackedTarget.active)
            {
                Reset();
                return;
            }

            float distance = Vector2.Distance(trackedTarget.Center, parryGaugeWorldPosition);
            if (distance > allowedRadius)
            {
                Main.NewText("Parry not possible (misaligned)", Color.Red);
                Reset();
                return;
            }

            parryTimer++;

            // Verifica se o jogador pressionou a tecla "X"
            if (Main.keyState.IsKeyDown(Keys.X))
            {
                ExecuteParryEffect(player);
                Reset();
                return;
            }

            if (parryTimer >= ParryDurationTicks)
            {
                Main.NewText("Parry timeout", Color.Gray);
                Reset();
            }
        }

        private void ExecuteParryEffect(Player player)
        {
            if (trackedTarget == null || !trackedTarget.active)
                return;

            // Lança o inimigo para cima
            trackedTarget.velocity.Y = -10f;
            trackedTarget.netUpdate = true;

            // Cálculo de dano: 50% da arma do jogador
            int baseDamage = player.HeldItem?.damage ?? 0;
            if (baseDamage <= 0)
            {
                Main.NewText("No valid weapon to parry with.", Color.Orange);
                return;
            }
            int damage = baseDamage / 2;

            // Aplica dano corretamente via SimpleStrikeNPC
            trackedTarget.SimpleStrikeNPC(
                damage,
                hitDirection: 0,
                crit: false,
                knockBack: 0f,
                damageType: player.HeldItem.DamageType
            );

            // Feedback visual e sonoro
            CombatText.NewText(trackedTarget.Hitbox, Color.LightCyan, damage);
            SoundEngine.PlaySound(SoundID.Item71, trackedTarget.position);
            Main.NewText($"Parry executed! Dealt {damage} damage.", Color.Cyan);
        }

        private void Reset()
        {
            parryActive = false;
            parryTimer = 0;
            trackedTarget = null;
        }
    }
}
