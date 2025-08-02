using Microsoft.Xna.Framework;
using Taoism.UI;
using Terraria;
using Terraria.ModLoader;

namespace Taoism.Players
{
    public class ParryPlayer : ModPlayer
    {
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Only update for the local player
            if (Main.myPlayer == Player.whoAmI)
            {
                // Get the UI system and update the range
                ModContent.GetInstance<ParryUISystem>().UpdateParryRange(target);
            }
        }
        
    }
}