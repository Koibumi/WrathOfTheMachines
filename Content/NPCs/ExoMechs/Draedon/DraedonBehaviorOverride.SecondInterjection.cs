﻿using CalamityMod.NPCs.ExoMechs;
using Luminance.Common.Utilities;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;

namespace WoTM.Content.NPCs.ExoMechs
{
    public sealed partial class DraedonBehaviorOverride : NPCBehaviorOverride
    {
        /// <summary>
        /// The monologue that Draedon uses upon his second Exo Mech being defeated, prior to the final Exo Mech doing battle with the player.
        /// </summary>
        public static readonly DraedonDialogueChain SecondInterjection = new DraedonDialogueChain("Mods.WoTM.NPCs.Draedon.").
            Add("Interjection7").
            Add("Interjection8").
            Add("Interjection9").
            Add("Interjection10").
            Add("Interjection11", Draedon.TextColorEdgy);

        /// <summary>
        /// The AI method that makes Draedon speak to the player after an Exo Mech has been defeated.
        /// </summary>
        public void DoBehavior_SecondInterjection()
        {
            int speakTimer = (int)AITimer - 90;
            var monologue = SecondInterjection;
            for (int i = 0; i < monologue.Count; i++)
            {
                if (speakTimer == monologue[i].SpeakDelay)
                    monologue[i].SayInChat();
            }

            Vector2 hoverDestination = PlayerToFollow.Center + new Vector2((PlayerToFollow.Center.X - NPC.Center.X).NonZeroSign() * -420f, -20f);
            NPC.SmoothFlyNear(hoverDestination, 0.05f, 0.94f);

            bool monologueIsFinished = speakTimer >= monologue.OverallDuration;

            if (speakTimer == monologue.OverallDuration - 60)
            {
                ScreenShakeSystem.StartShake(6f);
                SoundEngine.PlaySound(Draedon.LaughSound);
            }

            // Reset the variables to their controls by healing the player.
            if (speakTimer == monologue[3].SpeakDelay - 60)
                HealPlayer();

            if (monologueIsFinished)
            {
                AIState = DraedonAIState.MoveAroundDuringBattle;
                AITimer = 0f;
                NPC.netUpdate = true;
            }

            PerformStandardFraming();
        }
    }
}
