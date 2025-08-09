using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;
using Terraria.Audio;

namespace Taoism.Content.Buffs
{
    public class ShatteredClockBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }
        public override void Update(NPC npc, ref int buffIndex)
        {

            if (Main.rand.NextBool(5))
            {
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.FlameBurst, 0, 0, 100, Color.Red, 1f);
            }

            if (npc.buffTime[buffIndex] == 1)
            {
                ExecuteStaggerEffect(npc);
            }
        }

        private void ExecuteStaggerEffect(NPC centerNpc)
        {
            float staggerRadius = 150f;

            foreach (NPC otherNpc in Main.npc)
            {
                if (otherNpc == null || !otherNpc.active || otherNpc.friendly)
                    continue;

                if (Vector2.Distance(centerNpc.Center, otherNpc.Center) < staggerRadius)
                {
                    // Verifica se o NPC já tem o StaggeredBuff
                    if (otherNpc.HasBuff(ModContent.BuffType<StaggeredBuff>()))
                    {
                        continue;
                    }

                    // Aplica o buff de stagger para paralisar e confundir o inimigo.
                    otherNpc.AddBuff(ModContent.BuffType<StaggeredBuff>(), 90);
                    
                    int baseDamage = 5; 
                    int critDamage = (int)(baseDamage * 1.8f);
                    
                    otherNpc.SimpleStrikeNPC(
                        critDamage,
                        hitDirection: 0,
                        crit: true,
                        knockBack: 3f,
                        damageType: DamageClass.Generic
                    );

                    CombatText.NewText(otherNpc.Hitbox, Color.BlueViolet, "BITES THE DUST!", dramatic: true);
                    SoundEngine.PlaySound(SoundID.Item93 with { Pitch = 0.5f }, otherNpc.position);
                    for (int i = 0; i < 15; i++)
                    {
                        Dust.NewDust(
                            otherNpc.position,
                            otherNpc.width,
                            otherNpc.height,
                            DustID.Vortex,
                            0f, 0f, 100, Color.LightBlue,
                            2f
                        );
                    }
                }
            }
        }
    }
}