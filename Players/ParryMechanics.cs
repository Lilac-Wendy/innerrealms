using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;

namespace Taoism.Players
{
    public class ParryMechanics
    {
        private const int ParryDurationTicks = 40;
        public const float DefaultAllowedRadius = 40f;
        private const float StaggerHealthThreshold = 0.2f; // 20% health threshold

        private int parryTimer;
        private bool parryActive;
        private NPC trackedTarget;
        private float lastParryDistance; 

        public void TryStartParry(NPC target, Vector2 parryGaugeWorldPosition, float allowedRadius = DefaultAllowedRadius)
        {
            if (target == null || !target.active || target.friendly || parryActive)
                return;

            lastParryDistance = Vector2.Distance(target.Center, parryGaugeWorldPosition);
            if (lastParryDistance > allowedRadius)
            {
                
                if (Main.rand.NextFloat() < 0.5f)  // 50/50 of failing if not perfect!
                {
                    Main.NewText("Parry failed (initial misalignment)", Color.OrangeRed);
                    return;
                }
                Main.NewText("Parry succeeded despite misalignment!", Color.Yellow);
            }
    
            trackedTarget = target;
            parryActive = true;
            parryTimer = 0;
            Main.NewText("Parry is possible!", Color.LightGreen);
        }

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

            if (Main.keyState.IsKeyDown(Keys.X) || PlayerInput.Triggers.Current.KeyStatus["X"])
            {
                ExecuteParryEffect(player, distance, allowedRadius);
                Reset();
                return;
            }

            if (parryTimer >= ParryDurationTicks)
            {
                Main.NewText("Parry timeout", Color.Gray);
                Reset();
            }
        }

        private int GetModifiedDamage(Player player, Item weapon, float distanceRatio)
        {
            if (weapon == null)
                return 10; // Fallback damage

            // Damage scales from 100% at point-blank to 50% at max distance
            float distanceFactor = 1f - (distanceRatio * 0.5f);
            StatModifier mod = player.GetDamage(weapon.DamageType);
            return (int)(mod.ApplyTo(weapon.damage) * distanceFactor);
        }

        private void ExecuteParryEffect(Player player, float currentDistance, float maxDistance)
        {
            if (trackedTarget == null || !trackedTarget.active)
                return;

            // Calculate distance ratio (0 = closest, 1 = at max allowed distance)
            float distanceRatio = MathHelper.Clamp(currentDistance / maxDistance, 0f, 1f);
            int baseDamage = GetModifiedDamage(player, player.HeldItem, distanceRatio);
            int damage = baseDamage / 2;

            if (damage <= 0)
            {
                Main.NewText("No valid weapon to parry with.", Color.Orange);
                return;
            }

            // Apply effects
            trackedTarget.velocity.Y = -20f;
            trackedTarget.netUpdate = true;
            player.velocity.X = -player.direction * 4f;
            player.immune = true;
            player.immuneTime = 20;

            // Apply damage
            trackedTarget.SimpleStrikeNPC(
                damage,
                hitDirection: 0,
                crit: false,
                knockBack: 0f,
                damageType: player.HeldItem.DamageType
            );

            // Apply posture damage (scales with distance)
            ApplyPostureDamage(damage, distanceRatio);

            // Check for stagger condition (below 20% health)
            if ((float)trackedTarget.life / trackedTarget.lifeMax < StaggerHealthThreshold)
            {
                StaggerEnemy(player);
            }

            // Visual/audio feedback
            CombatText.NewText(trackedTarget.Hitbox, Color.LightCyan, damage);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.1f }, trackedTarget.position);
            Dust.NewDust(trackedTarget.position, trackedTarget.width, trackedTarget.height, DustID.BlueTorch);
            Main.NewText($"Parry executed! Dealt {damage} damage.", Color.Cyan);
        }

        private void ApplyPostureDamage(int baseDamage, float distanceRatio)
        {
            // Posture damage scales inversely with distance (more at closer range)
            float postureMultiplier = 0.3f * (1.5f - distanceRatio); // 45% at close, 30% at max range
            int postureDamage = (int)(baseDamage * postureMultiplier);

            // TODO: ACTUAL POSTURE DAMAGE SYSTEM 
            Main.NewText($"Posture Damage: {postureDamage} (Distance Factor: {1 - distanceRatio:F2})", Color.Yellow);
        }

        private void StaggerEnemy(Player player)
        {
            // Stagger effects
            trackedTarget.velocity = Vector2.Zero;
            trackedTarget.AddBuff(BuffID.Confused, 90); // 1.5 second stagger
            
            // Critical hit on staggered enemy
            int critDamage = (int)(GetModifiedDamage(player, player.HeldItem, 0) * 1.5f);
            trackedTarget.SimpleStrikeNPC(
                critDamage,
                hitDirection: 0,
                crit: true,
                knockBack: 3f,
                damageType: player.HeldItem.DamageType
            );

            CombatText.NewText(trackedTarget.Hitbox, Color.Gold, "STAGGERED!");
            SoundEngine.PlaySound(SoundID.Item89, trackedTarget.position);
            Main.NewText("Finishing blow!", Color.Gold);
        }

        private void Reset()
        {
            parryActive = false;
            parryTimer = 0;
            trackedTarget = null;
        }
    }
}