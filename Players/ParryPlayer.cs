using Microsoft.Xna.Framework;
using Taoism.Players;
using taoism.Projectiles;
using Terraria;
using Terraria.ModLoader;

namespace taoism.Players;

public class ParryPlayer : ModPlayer
{
    private ParryMechanics parryMechanics = new ParryMechanics();

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        if (Main.myPlayer != Player.whoAmI)
            return;

        var uiSystem = ModContent.GetInstance<ParryUiSystem>();
        uiSystem.UpdateParryRange(target);

        Vector2 gaugePos = uiSystem.GetParryGaugeWorldPosition();

        parryMechanics.TryStartParry(target, gaugePos);
    }

    public override void PostUpdate()
    {
        var uiSystem = ModContent.GetInstance<ParryUiSystem>();
        Vector2 gaugePos = uiSystem.GetParryGaugeWorldPosition();
        parryMechanics.Update(Player, gaugePos);
    }
}