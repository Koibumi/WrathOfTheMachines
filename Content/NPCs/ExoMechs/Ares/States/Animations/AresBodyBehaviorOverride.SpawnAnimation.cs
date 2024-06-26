﻿using Luminance.Common.Utilities;
using Microsoft.Xna.Framework;
using Terraria;

namespace WoTM.Content.NPCs.ExoMechs
{
    public sealed partial class AresBodyBehaviorOverride : NPCBehaviorOverride
    {
        /// <summary>
        /// AI update loop method for the inactive state.
        /// </summary>
        public void DoBehavior_SpawnAnimation()
        {
            ZPosition = Utilities.InverseLerp(35f, 0f, AITimer).Cubed() * 6f;
            NPC.Center = Target.Center - Vector2.UnitY * (ZPosition * 20f + 200f);
            NPC.velocity *= 0.6f;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;

            BasicHandUpdateWrapper();

            if (AITimer >= 35f)
            {
                CurrentState = AresAIState.DetachHands;
                ZPosition = 0f;
                AITimer = 0;
                NPC.netUpdate = true;
            }
        }

        public void BasicHandUpdateWrapper()
        {
            InstructionsForHands[0] = new(h => BasicHandUpdate(h, new Vector2(-430f, 50f), 0));
            InstructionsForHands[1] = new(h => BasicHandUpdate(h, new Vector2(-280f, 224f), 1));
            InstructionsForHands[2] = new(h => BasicHandUpdate(h, new Vector2(280f, 224f), 2));
            InstructionsForHands[3] = new(h => BasicHandUpdate(h, new Vector2(430f, 50f), 3));
        }

        public void BasicHandUpdate(AresHand hand, Vector2 hoverOffset, int armIndex)
        {
            NPC handNPC = hand.NPC;
            handNPC.Opacity = Utilities.Saturate(handNPC.Opacity + 0.025f);
            handNPC.SmoothFlyNear(NPC.Center + hoverOffset * NPC.scale, 0.7f, 0.5f);

            hand.UsesBackArm = armIndex == 0 || armIndex == ArmCount - 1;
            hand.ArmSide = (armIndex >= ArmCount / 2).ToDirectionInt();
            hand.Frame = AITimer / 3 % 12;

            hand.ArmEndpoint = Vector2.Lerp(hand.ArmEndpoint, handNPC.Center + handNPC.velocity, handNPC.Opacity);
            hand.EnergyDrawer.chargeProgress *= 0.7f;

            if (handNPC.Opacity <= 0f)
                hand.GlowmaskDisabilityInterpolant = 0f;
        }
    }
}
