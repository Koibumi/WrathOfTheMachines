﻿using Luminance.Common.Utilities;
using Microsoft.Xna.Framework;
using Terraria;
using WoTM.Core.BehaviorOverrides;

namespace WoTM.Content.NPCs.ExoMechs.Draedon;

public sealed partial class DraedonBehavior : NPCBehaviorOverride
{
    /// <summary>
    /// The AI method that makes Draedon temporarily leave the Exo Mechs battle.
    /// </summary>
    public void DoBehavior_TemporarilyLeave()
    {
        HologramOverlayInterpolant = LumUtils.Saturate(HologramOverlayInterpolant + AnyDyingExoMechs.ToDirectionInt() * 0.02f);
        if (!AnyDyingExoMechs && HologramOverlayInterpolant <= 0f)
            ChangeAIState(DraedonAIState.MoveAroundDuringBattle);

        if (HologramOverlayInterpolant >= 1f)
            NPC.SmoothFlyNearWithSlowdownRadius(PlayerToFollow.Center - Vector2.UnitY * 540f, 0.075f, 0.9f, 300f);
        else
            NPC.velocity *= 0.7f;

        PerformStandardFraming();
    }
}
