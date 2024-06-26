﻿using System;
using CalamityMod.NPCs.ExoMechs.Thanatos;
using Luminance.Common.Utilities;
using Microsoft.Xna.Framework;
using Terraria;

namespace WoTM.Content.NPCs.ExoMechs
{
    public sealed partial class HadesHeadBehaviorOverride : NPCBehaviorOverride
    {
        /// <summary>
        /// AI update loop method for the inactive state.
        /// </summary>
        public void DoBehavior_Inactive()
        {
            BodyBehaviorAction = new(AllSegments(), CloseSegment());

            Vector2 hoverDestination = Target.Center + new Vector2(MathF.Cos(MathHelper.TwoPi * AITimer / 150f) * 600f, 4200f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.SafeDirectionTo(hoverDestination) * 50f, 0.05f);

            // This is necessary to ensure that the map icon goes away.
            NPC.As<ThanatosHead>().SecondaryAIState = (int)ThanatosHead.SecondaryPhase.PassiveAndImmune;

            NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}
