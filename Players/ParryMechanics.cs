using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using taoism.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameInput;

namespace Taoism.Players;

public class ParryMechanics : ModPlayer
{
    private const int ParryDurationTicks = 40;
    public const float DefaultAllowedRadius = 40f;
    private const float StaggerHealthThreshold = 0.2f;

    private int parryTimer;
    private bool parryActive;
    private NPC trackedTarget;
    private float lastParryDistance;

    private int comboCount = 0;
    private NPC boundTarget = null;
    private int boundHand = -1;
    private bool hasFreeParry = false;

    private ParryGaugeRenderer GetGaugeRenderer()
    {
        // Acessa o sistema UI primeiro
        var uiSystem = ModContent.GetInstance<ParryUiSystem>();
    
        // Usa reflection para acessar o campo privado renderer
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

    public void TryStartParry(NPC target, Vector2 parryGaugeWorldPosition,
        float allowedRadius = DefaultAllowedRadius)
    {
        var gauge = GetGaugeRenderer();
        if (gauge == null) return;

        if (hasFreeParry && gauge.CurrentStance == ParryGaugeRenderer.ElementalStance.Iron)
        {
            hasFreeParry = false;
            Main.NewText("Used free Iron parry!", Color.Silver);
        }
        else if (target == null || !target.active || target.friendly || parryActive)
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
            ExecuteElementalEffect(player, distance, allowedRadius);
            Reset();
            return;
        }

        if (parryTimer >= ParryDurationTicks)
        {
            Main.NewText("Parry timeout", Color.Gray);
            Reset();
        }
    }

    private void ExecuteElementalEffect(Player player, float currentDistance, float maxDistance)
    {
        var gauge = GetGaugeRenderer();
        if (gauge == null || trackedTarget == null || !trackedTarget.active)
            return;

        // Correção: Acessar ShouldDenylistWeapon através da instância gauge
        bool isValidWeapon = !gauge.ShouldDenylistWeapon(player.HeldItem);
    

        gauge.AdvanceStance();

        if (isValidWeapon)
        {
            float distanceRatio = MathHelper.Clamp(currentDistance / maxDistance, 0f, 1f);
            int baseDamage = GetModifiedDamage(player, player.HeldItem, distanceRatio);
            int damage = baseDamage / 2;
        
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
            Main.NewText($"Combo: {comboCount} hits", Color.Gold);
            Dust.NewDustPerfect(player.Center, DustID.GemTopaz, Vector2.Zero, 150, Color.White, 0.8f);
        }
        else
        {
            Main.NewText("Postura avançada - Arma inválida para parry", Color.Orange);
            Dust.NewDustPerfect(player.Center, DustID.Smoke, Vector2.Zero, 100, Color.Gray, 1.2f);
            SoundEngine.PlaySound(SoundID.Item10 with { Pitch = -0.5f }, player.Center);
            comboCount = 0;
        }
    }


    private void ExecuteWaterUppercut(Player player, int damage)
    {
        trackedTarget.velocity.Y = -30f;
        if (Main.netMode != NetmodeID.SinglePlayer)
            trackedTarget.netUpdate = true;

        if (trackedTarget.HasBuff(BuffID.Confused))
            trackedTarget.DelBuff(trackedTarget.FindBuffIndex(BuffID.Confused));

        trackedTarget.AddBuff(BuffID.Slow, 180);
        ApplyDamageEffects(player, damage, Color.Cyan);
        Main.NewText("Water Uppercut! Target launched upward", Color.Cyan);
    }

    private void ExecuteWoodenJab(Player player, int damage)
    {
        Vector2 direction = trackedTarget.Center - player.Center;
        direction.Normalize();
        trackedTarget.velocity += direction * 8f;

        player.AddBuff(BuffID.WeaponImbueIchor, 300);
        damage = (int)(damage * 1.3f);

        ApplyDamageEffects(player, damage, Color.LimeGreen);
        Main.NewText("Wooden Jab! +Knockback & Ichor Buff", Color.LimeGreen);
    }

    private void ExecuteFieryLunge(Player player, int damage)
    {
        player.velocity.X = player.direction * 10f;
        player.immune = true;
        player.immuneTime = 15;

        boundTarget = trackedTarget;
        boundHand = Main.rand.Next(2);

        ApplyDamageEffects(player, damage, Color.OrangeRed);
        Main.NewText($"Fiery Lunge! Target hand bound ({boundHand})", Color.OrangeRed);
    }

    private void ExecuteEarthenSweep(Player player, int damage)
    {
        float coneAngle = MathHelper.PiOver2;
        float coneDistance = 200f;
        int dustCount = 30;

        Vector2 direction = new Vector2(player.direction, 0);

        if (boundTarget == trackedTarget)
        {
            damage *= 2;
            Main.NewText("Earthen Sweep: Double damage to bound target!", new Color(200, 150, 50));
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
                            (int)(damage * 0.75f),
                            hitDirection: player.direction,
                            crit: false,
                            knockBack: 2f,
                            damageType: player.HeldItem.DamageType
                        );

                        CombatText.NewText(npc.Hitbox, new Color(150, 75, 0), (damage * 0.75f).ToString("0"));
                        alreadyHit.Add(npc);
                    }
                }
            }
        }

        if (!alreadyHit.Contains(trackedTarget))
        {
            ApplyDamageEffects(player, damage, new Color(150, 75, 0));
        }

        Main.NewText("Earthen Sweep! Cone attack", new Color(150, 75, 0));
    }

private void ExecuteIronSurge(Player player, int damage)
{
    float upwardKnockback = 10f;
    trackedTarget.velocity.Y = -upwardKnockback;
    trackedTarget.netUpdate = true;
    trackedTarget.AddBuff(BuffID.Electrified, 180);
    trackedTarget.AddBuff(BuffID.Ichor, 300);

    float dashSpeed = 40f;
    Vector2 dashDir = new Vector2(player.direction, 0f);
    trackedTarget.velocity = dashDir * dashSpeed;
    trackedTarget.netUpdate = true;
    
    // --- Lógica de condução elétrica ---

    // A partir do alvo principal, vamos iniciar a cadeia de condução
    // Usamos um Set para garantir que cada NPC seja atingido apenas uma vez na cadeia
    HashSet<int> hitNpcs = new HashSet<int>();
    
    // Inicia a cadeia de dano no alvo principal
    ApplyChainDamage(player, trackedTarget, damage, hitNpcs, 1);
    
    Main.NewText(
        "Iron Surge! Electrified target surges through enemies!",
        Color.Yellow
    );
    
    ApplyDamageEffects(player, damage, Color.Yellow);
    SoundEngine.PlaySound(
        SoundID.Item93 with { Pitch = -0.3f },
        trackedTarget.Center
    );
    
    // Efeito visual no alvo principal
    for (int i = 0; i < 10; i++)
    {
        Dust.NewDust(trackedTarget.position, trackedTarget.width, trackedTarget.height, 
                    DustID.Electric, 0f, 0f, 100, default, 2f);
    }
}

// Nova função para aplicar o dano em cadeia e propagar o efeito
private void ApplyChainDamage(Player player, NPC currentTarget, int damage, HashSet<int> hitNpcs, int chainCount)
{
    // Adiciona o alvo atual à lista de inimigos já atingidos para evitar loops infinitos
    if (hitNpcs.Contains(currentTarget.whoAmI))
    {
        return;
    }
    hitNpcs.Add(currentTarget.whoAmI);
    
    // Aplica o dano ao alvo atual
    int chainDamage = (int)(damage * (1.0f - (chainCount * 0.1f))); // Redução de dano por cada salto
    if (chainDamage <= 0) chainDamage = 1; // Garante que o dano não seja zero

    var hitInfo = new NPC.HitInfo()
    {
        Damage = chainDamage,
        Knockback = 8f,
        HitDirection = player.direction
    };
    currentTarget.StrikeNPC(hitInfo);
    
    // Aplica o buff elétrico com duração progressiva
    int baseDuration = 180;
    int duration = baseDuration + (chainCount * 30);
    currentTarget.AddBuff(BuffID.Electrified, duration);

    // Efeitos visuais
    CombatText.NewText(currentTarget.Hitbox, Color.Yellow, chainDamage.ToString());
    Dust.NewDust(currentTarget.position, currentTarget.width, currentTarget.height, DustID.Electric, 0f, 0f, 0, default, 1.5f);

    // --- Lógica de propagação (70% de chance) ---
    // A cada salto, a chance pode diminuir para balancear. Ex: 70%, 60%, 50%...
    // Neste exemplo, a chance é fixa em 70%
    if (Main.rand.NextFloat() < 0.70f)
    {
        float spreadRadius = 150f; // Raio para encontrar o próximo alvo

        // Procura por inimigos próximos
        foreach (NPC npc in Main.npc)
        {
            if (npc.active && !npc.friendly && !hitNpcs.Contains(npc.whoAmI))
            {
                if (Vector2.Distance(npc.Center, currentTarget.Center) < spreadRadius)
                {
                    // Propaga o efeito para o novo alvo
                    ApplyChainDamage(player, npc, damage, hitNpcs, chainCount + 1);
                    // Apenas um alvo por vez para o próximo salto
                    break; 
                }
            }
        }
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

    private void ApplyPostureDamage(int baseDamage, float distanceRatio)
    {
        var gauge = GetGaugeRenderer();
        if (gauge == null) return;

        float postureMultiplier = 0.3f * (1.5f - distanceRatio);
        int postureDamage = (int)(baseDamage * postureMultiplier);

        CombatText.NewText(trackedTarget.Hitbox, GetStanceColor(gauge.CurrentStance), $"{postureDamage}!",
            dramatic: true);
    }
        
        


    private void StaggerEnemy(Player player, int baseDamage)
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