using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Taoism.Content.Buffs;
using taoism.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;


namespace Taoism.Players;

public class ParryMechanics : ModPlayer
{
    private const int ParryDurationTicks = 180;
    private const float DefaultAllowedRadius = 40f;
    private const float StaggerHealthThreshold = 0.1f;

    private int parryTimer;
    private bool parryActive;
    private NPC trackedTarget;
    private float lastParryDistance;
    
    public const int ComboTimeoutTicks = 900; 

    private int comboCount;
    private NPC boundTarget = null;
    private int boundHand = -1;
    public uint lastHitTime;

    private ParryGaugeRenderer GetGaugeRenderer()
    {
        var uiSystem = ModContent.GetInstance<ParryUiSystem>();
        if (uiSystem != null)
        {
            var field = typeof(ParryUiSystem).GetField("renderer",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);

            if (field != null)
            {
                return (ParryGaugeRenderer)field.GetValue(uiSystem);
            }
        }
        return null;
    }

    public void TryStartParry(NPC target, Vector2 parryGaugeWorldPosition, float allowedRadius = DefaultAllowedRadius)
    {
        var gauge = GetGaugeRenderer();
        if (gauge == null) return;
        if (target == null || !target.active || target.friendly || parryActive)
        {
            return;
        }

        lastParryDistance = Vector2.Distance(target.Center, parryGaugeWorldPosition);
        if (lastParryDistance > allowedRadius)
        {
            if (Main.rand.NextFloat() < 0.5f)
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

    public void CheckParryInput(Player player, Vector2 parryGaugeWorldPosition, float allowedRadius = DefaultAllowedRadius)
    {
        if (parryActive && Taoism.taoism.RiposteKey.JustPressed)
        {
            float distance = Vector2.Distance(trackedTarget.Center, parryGaugeWorldPosition);
            ExecuteElementalEffect(player, distance, allowedRadius);
            Reset();
        }
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

        if (parryTimer >= ParryDurationTicks)
        {
            Main.NewText("Parry timeout", Color.Gray);
            Reset();
        }
    }

    private int ApplyPostureDamage(int baseDamage, float distanceRatio)
    {
        var gauge = GetGaugeRenderer();
        if (gauge == null) return 0;

        float postureMultiplier = 0.3f * (1.5f - distanceRatio);
        float healthScaling = trackedTarget.lifeMax * 0.005f;
        float sizeScaling = (trackedTarget.width * trackedTarget.height) * 0.001f;
        float finalPostureMultiplier = postureMultiplier + healthScaling + sizeScaling;

        float sizeFactor = (trackedTarget.width * trackedTarget.height) / 100f;
        float healthFactor = (trackedTarget.lifeMax * 0.05f) / 1000f;
        float maxMultiplier = sizeFactor + healthFactor;
        maxMultiplier = MathHelper.Clamp(maxMultiplier, 1.0f, float.MaxValue);
        finalPostureMultiplier = MathHelper.Clamp(finalPostureMultiplier, 1.0f, maxMultiplier);
        int postureDamage = (int)(baseDamage * finalPostureMultiplier);
        if (trackedTarget.boss)
        {
            postureDamage /= 25;
        }
        CombatText.NewText(trackedTarget.Hitbox, GetStanceColor(gauge.CurrentStance), $"{postureDamage}!", dramatic: true);
        Main.NewText($"Posture Damage Applied: {postureDamage}", Color.LightYellow);
        return postureDamage;
    }

    private void ExecuteElementalEffect(Player player, float currentDistance, float maxDistance)
    {
        var gauge = GetGaugeRenderer();
        
        // Coloque a verificação do timeout primeiro, fora das outras verificações.
        if (Main.GameUpdateCount - lastHitTime > ComboTimeoutTicks)
        {
            comboCount = 0;
            if (gauge != null)
            {
                gauge.EarthFrameIndex = 0;
            }
        }

        if (gauge == null || trackedTarget == null || !trackedTarget.active)
        {
            comboCount = 0;
            if (gauge != null)
            {
                gauge.EarthFrameIndex = 0;
            }
            return;
        }

        bool isValidWeapon = !gauge.ShouldDenylistWeapon(player.HeldItem);

        gauge.AdvanceStance();

        if (isValidWeapon)
        {
            float distanceRatio = MathHelper.Clamp(currentDistance / maxDistance, 0f, 1f);
            int baseDamage = GetModifiedDamage(player, player.HeldItem, distanceRatio);
            int postureDamage = ApplyPostureDamage(baseDamage, distanceRatio);
            float comboDamageMultiplier = 1f + ((float)comboCount * (postureDamage * 0.1f) / baseDamage);
            int finalDamage = (int)(baseDamage * comboDamageMultiplier);
            int damage = finalDamage / 2;
            
            gauge.AdvanceEarthFrame();
            
            switch (gauge.CurrentStance)
            {
                case ParryGaugeRenderer.ElementalStance.Water:
                    ExecuteWaterUppercut(player, damage);
                    break;
                case ParryGaugeRenderer.ElementalStance.Wood:
                    ExecuteWoodenJab(player, damage);
                    break;
                case ParryGaugeRenderer.ElementalStance.Fire:
                    ExecuteFieryLunge(player, damage);
                    break;
                case ParryGaugeRenderer.ElementalStance.Earth:
                    ExecuteEarthenSweep(player, damage);
                    break;
                case ParryGaugeRenderer.ElementalStance.Iron:
                    ExecuteIronSurge(player, damage);
                    break;
            }
            comboCount++;
            lastHitTime = Main.GameUpdateCount; 

            CombatText.NewText(player.Hitbox, Color.Gold, $"Combo: {comboCount}!", dramatic: true);
            Dust.NewDustPerfect(player.Center, DustID.GemTopaz, Vector2.Zero, 150, Color.White, 0.8f);
        }
        else
        {
            Main.NewText("Postura avançada - Arma inválida para parry", Color.Orange);
            Dust.NewDustPerfect(player.Center, DustID.Smoke, Vector2.Zero, 100, Color.Gray, 1.2f);
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.5f }, player.Center);

            comboCount = 0;
            if (gauge != null)
            {
                gauge.EarthFrameIndex = 0;
            }
        }
    }


    private void ExecuteWaterUppercut(Player player, int damage)
    {
        int knockbackDirection = -1;
        float launchStrength = 30f;
        if (trackedTarget.knockBackResist <= 0f)
        {
            trackedTarget.velocity.Y = -launchStrength;
        }
        else
        {
            var hitInfo = new NPC.HitInfo()
            {
                Damage = damage,
                Knockback = launchStrength,
                HitDirection = knockbackDirection
            };
            trackedTarget.StrikeNPC(hitInfo);
        }
        if (Main.netMode != NetmodeID.SinglePlayer)
            trackedTarget.netUpdate = true;

        if (trackedTarget.HasBuff(BuffID.Confused))
            trackedTarget.DelBuff(trackedTarget.FindBuffIndex(BuffID.Confused));

        trackedTarget.AddBuff(BuffID.Slow, 900);
        ApplyDamageEffects(player, damage, Color.Cyan);
        Main.NewText("Water Uppercut! Target launched upward", Color.Cyan);
    }

    private void ExecuteWoodenJab(Player player, int damage)
    {
        Vector2 direction = trackedTarget.Center - player.Center;
        direction.Normalize();

        if (trackedTarget.knockBackResist == 0f)
        {
            trackedTarget.velocity += direction * 10f;
        }
        else
        {
            var hitInfo = new NPC.HitInfo()
            {
                Damage = damage,
                Knockback = 10f,
                HitDirection = player.direction
            };
            trackedTarget.StrikeNPC(hitInfo);
        }

        player.AddBuff(BuffID.WeaponImbueIchor, 300);
        damage = (int)(damage * 1.3f);

        ApplyDamageEffects(player, damage, Color.LimeGreen);
        Main.NewText("Wooden Jab! +Knockback & Ichor Buff", Color.LimeGreen);
    }

    private void ExecuteFieryLunge(Player player, int damage)
    {
        Vector2 directionToTarget = trackedTarget.Center - player.Center;
        directionToTarget.Normalize();
        float dashSpeed = 10f;
        player.velocity = directionToTarget * dashSpeed;
        int dashDuration = 25;
        player.immune = true;
        player.immuneTime = dashDuration;
        int projectileType = ProjectileID.MolotovFire2;

        Projectile.NewProjectile(player.GetSource_FromThis(), player.Center, Vector2.Zero, projectileType,
            (int)(damage * 0.5f), 0f, player.whoAmI, 0, dashDuration);

        for (int i = 0; i < 8; i++)
        {
            Dust dust = Dust.NewDustDirect(player.position, player.width, player.height, DustID.IchorTorch, 0f, 0f, 100, default, 2f);
            dust.noGravity = true;
            dust.velocity *= 2f;
        }
        player.AddBuff(BuffID.WeaponImbueIchor, 300);
        damage = (int)(damage * 1.3f);
        ApplyDamageEffects(player, damage, Color.OrangeRed);

        if (trackedTarget != null && trackedTarget.active)
        {
            trackedTarget.AddBuff(ModContent.BuffType<ShatteredClockBuff>(), 300);
            Main.NewText("Fiery Lunge! Shattered Clock already touched that target!", Color.OrangeRed);
        }
        else
        {
            Main.NewText("Fiery Lunge!", Color.OrangeRed);
        }
    }

    private void ExecuteEarthenSweep(Player player, int damage)
    {
        float coneAngle = MathHelper.PiOver2;
        float coneDistance = 200f;
        int dustCount = 40;
        Vector2 direction = new Vector2(player.direction, 0);
        int baseDamage = damage;
        if (boundTarget != null && boundTarget.active && boundTarget == trackedTarget)
        {
            int bonusDamage = trackedTarget.damage / 2;
            baseDamage += bonusDamage;
            Main.NewText($"Earthen Sweep: Bônus de dano de {bonusDamage} do alvo bound!", new Color(200, 150, 50));
        }
        List<NPC> alreadyHit = new List<NPC>();
        for (int i = 0; i < dustCount; i++)
        {
            float angleVariation = Main.rand.NextFloat(-coneAngle / 2, coneAngle / 2);
            Vector2 dustDirection = direction.RotatedBy(angleVariation);
            Vector2 position = player.Center + new Vector2(Main.rand.Next(-20, 20), Main.rand.Next(-20, 20));
            Dust dust = Dust.NewDustPerfect(
                position,
                DustID.Dirt,
                dustDirection * Main.rand.NextFloat(5f, 10f),
                100,
                new Color(150, 75, 0),
                Main.rand.NextFloat(1f, 2f)
            );
            dust.noGravity = true;
            Vector2 endPosition = position + dustDirection * coneDistance;
            foreach (NPC npc in Main.npc)
            {
                if (npc.active && !npc.friendly && npc.life > 0 && !alreadyHit.Contains(npc))
                {
                    if (Collision.CheckAABBvLineCollision(
                            npc.position, npc.Size,
                            position, endPosition))
                    {
                        npc.SimpleStrikeNPC(
                            (int)(baseDamage * 0.75f),
                            hitDirection: player.direction,
                            crit: false,
                            knockBack: 2f,
                            damageType: player.HeldItem.DamageType
                        );
                        CombatText.NewText(npc.Hitbox, new Color(150, 75, 0), (baseDamage * 0.75f).ToString("0"));
                        alreadyHit.Add(npc);
                    }
                }
            }
        }
        if (!alreadyHit.Contains(trackedTarget))
        {
            ApplyDamageEffects(player, baseDamage, new Color(150, 75, 0));
        }
        Main.NewText("Earthen Sweep! Cone attack", new Color(150, 75, 0));
    }

    private void ExecuteIronSurge(Player player, int damage)
    {
        float upwardKnockback = 10f;
        trackedTarget.velocity.Y = -upwardKnockback;
        trackedTarget.netUpdate = true;
        trackedTarget.AddBuff(BuffID.Electrified, 180);
        float dashSpeed = 40f;
        Vector2 dashDir = new Vector2(player.direction, 0f);
        trackedTarget.velocity = dashDir * dashSpeed;
        trackedTarget.netUpdate = true;
        HashSet<int> hitNpcs = new HashSet<int>();
        ApplyChainDamage(player, trackedTarget, damage, hitNpcs, 3);
        Main.NewText(
            "Iron Surge! Electrified target surges through enemies!",
            Color.Yellow
        );
        ApplyDamageEffects(player, damage, Color.Yellow);
        SoundEngine.PlaySound(
            SoundID.Item93 with { Pitch = -0.3f },
            trackedTarget.Center
        );

        for (int i = 0; i < 10; i++)
        {
            Dust dust = Dust.NewDustDirect(trackedTarget.position, trackedTarget.width, trackedTarget.height,
                DustID.Electric, 0f, 0f, 100, default, 2f);
            dust.noGravity = true;
        }
    }
    private void ApplyChainDamage(Player player, NPC currentTarget, int damage, HashSet<int> hitNpcs, int chainCount)
    {
        float distanceRatio = Vector2.Distance(player.Center, currentTarget.Center) / 1000f;
        int postureDamage = ApplyPostureDamage(damage, distanceRatio);

        if (!hitNpcs.Add(currentTarget.whoAmI))
        {
            return;
        }
        int chainDamage = (int)(damage * (1.0f - (chainCount * 0.1f)) + postureDamage);
        if (chainDamage <= 0) chainDamage = 2 * postureDamage;

        var hitInfo = new NPC.HitInfo()
        {
            Damage = chainDamage,
            Knockback = 8f,
            HitDirection = player.direction
        };
        currentTarget.StrikeNPC(hitInfo);

        int baseDuration = 900;
        int duration = baseDuration + (chainCount * 30);
        currentTarget.AddBuff(BuffID.Electrified, duration);

        CombatText.NewText(currentTarget.Hitbox, Color.Yellow, chainDamage.ToString());
        Dust.NewDust(currentTarget.position, currentTarget.width, currentTarget.height, DustID.Electric, 0f, 0f, 0, default, 1.5f);

        if (Main.rand.NextFloat() < 0.70f)
        {
            float spreadRadius = 150f;

            foreach (NPC npc in Main.npc)
            {
                if (npc.active && !npc.friendly && !hitNpcs.Contains(npc.whoAmI))
                {
                    if (Vector2.Distance(npc.Center, currentTarget.Center) < spreadRadius)
                    {
                        CreateArcEffect(currentTarget.Center, npc.Center);
                        Dust.NewDustPerfect(npc.Center, DustID.Electric, Vector2.Zero, 100, Color.LightBlue, 1.5f);
                        SoundEngine.PlaySound(SoundID.Item93, npc.position);

                        ApplyChainDamage(player, npc, damage, hitNpcs, chainCount + 1);
                    }
                }
            }
        }
    }
    private void CreateArcEffect(Vector2 start, Vector2 end)
    {
        int segments = 6;
        Vector2 step = (end - start) / segments;

        for (int i = 0; i <= segments; i++)
        {
            Vector2 point = start + step * i;
            point += new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f));

            Dust dust = Dust.NewDustPerfect(point, DustID.Electric, Vector2.Zero, 100, Color.LightBlue, 1.2f);
            dust.noGravity = true;
        }
    }

    private void ApplyDamageEffects(Player player, int damage, Color color)
    {
        if (damage <= 0)
        {
            Main.NewText("No valid weapon to parry with.", Color.Orange);
            return;
        }
        trackedTarget.SimpleStrikeNPC(
            damage,
            hitDirection: 0,
            crit: false,
            knockBack: 0f,
            damageType: player.HeldItem.DamageType
        );
        CombatText.NewText(trackedTarget.Hitbox, color, damage.ToString(), dramatic: true);
        ApplyPostureDamage(damage, lastParryDistance / DefaultAllowedRadius);
        if ((float)trackedTarget.life / trackedTarget.lifeMax < StaggerHealthThreshold && trackedTarget.active &&
            trackedTarget.life > 0)
        {
            StaggerEnemy(player, damage);
        }

        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.1f }, trackedTarget.position);
        Dust.NewDust(trackedTarget.position, trackedTarget.width, trackedTarget.height, DustID.BlueTorch);
    }

    public void StaggerEnemy(Player player, int baseDamage)
    {
        if (!trackedTarget.active || trackedTarget.life <= 0)
            return;

        trackedTarget.velocity = Vector2.Zero;
        trackedTarget.AddBuff(BuffID.Confused, 90);

        int critDamage = (int)(baseDamage * 1.8f);
        trackedTarget.SimpleStrikeNPC(
            critDamage,
            hitDirection: 0,
            crit: true,
            knockBack: 3f,
            damageType: player.HeldItem.DamageType
        );
        CombatText.NewText(trackedTarget.Hitbox, Color.Gold, "STAGGERED!", dramatic: true);
        SoundEngine.PlaySound(SoundID.Item89, trackedTarget.position);
        Main.NewText("Finishing blow!", Color.Gold);
    }

    private int GetModifiedDamage(Player player, Item weapon, float distanceRatio)
    {
        if (weapon == null)
            return 10;

        float distanceFactor = 1f - (distanceRatio * 0.5f);
        StatModifier mod = player.GetDamage(weapon.DamageType);
        return (int)(mod.ApplyTo(weapon.damage) * distanceFactor);
    }

    private void Reset()
    {
        parryActive = false;
        parryTimer = 0;
        trackedTarget = null;
    }

    public static Color GetStanceColor(ParryGaugeRenderer.ElementalStance stance)
    {
        return stance switch
        {
            ParryGaugeRenderer.ElementalStance.Water => Color.Cyan,
            ParryGaugeRenderer.ElementalStance.Wood => Color.LimeGreen,
            ParryGaugeRenderer.ElementalStance.Fire => Color.OrangeRed,
            ParryGaugeRenderer.ElementalStance.Earth => new Color(150, 75, 0),
            ParryGaugeRenderer.ElementalStance.Iron => Color.Silver,
            _ => Color.White
        };
    }
}