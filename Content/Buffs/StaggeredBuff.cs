using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;

namespace Taoism.Content.Buffs
{
    public class StaggeredBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex)
        {
            npc.velocity = Vector2.Zero;
            if (!npc.HasBuff(BuffID.Confused))
            {
                npc.AddBuff(BuffID.Confused, npc.buffTime[buffIndex]);
            }
        }
    }
}