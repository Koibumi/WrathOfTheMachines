﻿using System;
using System.Collections.Generic;
using CalamityMod.NPCs.ExoMechs;
using CalamityMod.NPCs.ExoMechs.Apollo;
using CalamityMod.NPCs.ExoMechs.Ares;
using CalamityMod.NPCs.ExoMechs.Artemis;
using CalamityMod.NPCs.ExoMechs.Thanatos;
using Luminance.Common.DataStructures;
using Luminance.Common.Utilities;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace WoTM.Content.NPCs.ExoMechs
{
    public sealed class ExoMechFightStateManager : ModSystem
    {
        /// <summary>
        /// The set of all previously summoned Exo Mechs throughout the fight. Used to keep track of which Exo Mechs existed in the past, after they are killed.
        /// </summary>
        internal static List<int> PreviouslySummonedMechIDs = [];

        /// <summary>
        /// The set of all defined Exo Mech phases.
        /// </summary>
        internal static List<PhaseDefinition> ExoMechPhases = [];

        /// <summary>
        /// Whether the fight is ongoing.
        /// </summary>
        public static bool FightOngoing
        {
            get;
            private set;
        }

        /// <summary>
        /// The current phase of the Exo Mechs fight, as calculated via the <see cref="PreUpdateEntities"/> hook in this system every frame.
        /// </summary>
        public static PhaseDefinition CurrentPhase
        {
            get;
            private set;
        }

        /// <summary>
        /// The overall state of the Exo Mechs fight, as calculated via the <see cref="PreUpdateEntities"/> hook in this system every frame.
        /// </summary>
        public static ExoMechFightState FightState
        {
            get;
            private set;
        }

        /// <summary>
        /// A representation of an undefined Exo Mech phase transition condition that always evaluates false regardless of context.
        /// </summary>
        /// 
        /// <remarks>
        /// This is primarily used in the context of a fallback when the fight is not ongoing at all.
        /// </remarks>
        public static readonly PhaseTransitionCondition UndefinedPhaseTransitionCondition = new(_ => false);

        /// <summary>
        /// The definition of an Exo Mech phase, defined by its ordering in the fight and overall condition.
        /// </summary>
        /// <param name="PhaseOrdering">The ordering of the phase definition. This governs when this phase should be entered, relative to all other phases.</param>
        /// <param name="StartCondition">The phase transition condition. This decides whether this phase should be transitioned to from the previous one.</param>
        /// <param name="FightIsHappening">Whether the fight is actually happening or not.</param>
        /// <param name="OnStart">An optional action to perform when the phase is started.</param>
        public record PhaseDefinition(int PhaseOrdering, bool FightIsHappening, PhaseTransitionCondition StartCondition, Action<ExoMechFightState>? OnStart);

        /// <summary>
        /// Represents a condition by which a phase transition should occur.
        /// </summary>
        /// <param name="fightState">The state of the overall Exo Mechs fight.</param>
        public delegate bool PhaseTransitionCondition(ExoMechFightState fightState);

        public override void PreUpdateEntities()
        {
            DetermineBattleState();
        }

        /// <summary>
        /// Creates and registers a new phase for the Exo Mechs fight.
        /// </summary>
        /// <param name="phaseOrdering">The ordering of the phase definition. This governs when this phase should be entered, relative to all other phases.</param>
        /// <param name="phaseStartingCondition">The phase transition condition.</param>
        /// <param name="onStart">An optional action to perform when the phase is started.</param>
        /// <returns>The newly created phase.</returns>
        internal static PhaseDefinition CreateNewPhase(int phaseOrdering, PhaseTransitionCondition phaseStartingCondition, Action<ExoMechFightState>? onStart = null)
        {
            PhaseDefinition phase = new(phaseOrdering, true, phaseStartingCondition, onStart);
            ExoMechPhases.Add(phase);
            return phase;
        }

        /// <summary>
        /// Evaluates the overall fight state, storing the result in the <see cref="FightState"/> member.
        /// </summary>
        /// 
        /// <remarks>
        /// This method should only be called when the fight is ongoing.
        /// </remarks>
        private static void CalculateFightState()
        {
            bool checkForPrimaryMech = false;
            List<int> evaluatedMechs = [];
            foreach (int exoMechID in ExoMechNPCIDs.ManagingExoMechIDs)
            {
                evaluatedMechs.Add(exoMechID);
                if (NPC.AnyNPCs(exoMechID))
                    checkForPrimaryMech = true;
            }

            // Search for the primary mech. If one doesn't exist (such as a result of summoning an Exo Mech with a cheat mod) simply make the first one found into the primary mech.
            if (TryFindPrimaryMech(out NPC? primaryMech))
                evaluatedMechs.Remove(primaryMech!.type);
            else if (checkForPrimaryMech)
            {
                MakeFirstExoMechIntoPrimaryMech();
                return;
            }

            DraedonBehaviorOverride.DraedonAIState? draedonState = null;
            int draedonIndex = NPC.FindFirstNPC(ModContent.NPCType<Draedon>());
            if (draedonIndex >= 0 && Main.npc[draedonIndex].TryGetBehavior(out DraedonBehaviorOverride behavior))
                draedonState = behavior.AIState;

            // Determine the overall fight state.
            ExoMechState[] stateOfOtherExoMechs = new ExoMechState[evaluatedMechs.Count];
            for (int i = 0; i < evaluatedMechs.Count; i++)
            {
                int otherExoMechIndex = NPC.FindFirstNPC(evaluatedMechs[i]);
                bool exoMechWasSummonedAtOnePoint = PreviouslySummonedMechIDs.Contains(evaluatedMechs[i]);
                NPC? otherExoMech = Main.npc.IndexInRange(otherExoMechIndex) ? Main.npc[otherExoMechIndex] : null;

                stateOfOtherExoMechs[i] = ExoMechStateFromNPC(otherExoMech, exoMechWasSummonedAtOnePoint);
            }
            FightState = new(draedonState, ExoMechStateFromNPC(primaryMech, true), stateOfOtherExoMechs);

            FightOngoing = true;
        }

        /// <summary>
        /// Turns the first Exo Mech this manager can find into the primary Exo Mech.
        /// </summary>
        private static void MakeFirstExoMechIntoPrimaryMech()
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (ExoMechNPCIDs.ManagingExoMechIDs.Contains(npc.type))
                {
                    TryToMakePrimaryMech(npc);
                    break;
                }
            }
        }

        /// <summary>
        /// Evaluates the current phase of the fight, determining whether it needs to update due to changes in the fight state.
        /// </summary>
        private static void EvaluatePhase()
        {
            // This is a bit weird but it's necessary to ensure that the static readonly fields are initialized and the ExoMechPhases list is populated properly.
            _ = ExoMechFightDefinitions.StartingTwoAtOncePhaseDefinition;

            PhaseDefinition? nextPhase = ExoMechPhases.Find(f => f.PhaseOrdering == CurrentPhase.PhaseOrdering + 1);
            if (nextPhase is null)
                return;

            if (nextPhase.StartCondition(FightState))
            {
                CurrentPhase = nextPhase;
                nextPhase.OnStart?.Invoke(FightState);
            }
        }

        // The wasSummoned parameter is necessary because it's sometimes not possible to check PreviouslySummonedMechIDs in this method, due to the NPC itself being null.
        /// <summary>
        /// Converts an NPC into an <see cref="ExoMechState"/>.
        /// </summary>
        /// 
        /// <remarks>
        /// This method gracefully accepts null or inactive NPCs, counting them as having been killed and having 0 health.
        /// </remarks>
        /// <param name="exoMech">The Exo Mech NPC.</param>
        /// <param name="wasSummoned">Whether the Exo Mech being checked was summoned previously or not.</param>
        private static ExoMechState ExoMechStateFromNPC(NPC? exoMech, bool wasSummoned)
        {
            // Yes, I know. Null safety operators. I prefer it like this, due to the fact that the
            // expression that requires null safety uses a Not operation.
            // '!(exoMech?.active ?? false)' just feels unnecessarily dense.
            if (exoMech is null || !exoMech.active)
                return new(0f, wasSummoned, true);

            return new(Utilities.Saturate(exoMech.life / (float)exoMech.lifeMax), true, false);
        }

        /// <summary>
        /// Evaluates the overall battle state, keeping track of the current phase and list of Exo Mechs that have been summoned throughout the battle.
        /// </summary>
        private static void DetermineBattleState()
        {
            bool hadesIsPresent = NPC.AnyNPCs(ExoMechNPCIDs.HadesHeadID);
            bool aresIsPresent = NPC.AnyNPCs(ExoMechNPCIDs.AresBodyID);
            bool artemisAndApolloArePresent = NPC.AnyNPCs(ExoMechNPCIDs.ArtemisID) || NPC.AnyNPCs(ExoMechNPCIDs.ApolloID);
            bool draedonIsPresent = NPC.AnyNPCs(ModContent.NPCType<Draedon>());
            bool fightIsOngoing = hadesIsPresent || aresIsPresent || artemisAndApolloArePresent || draedonIsPresent;
            if (!fightIsOngoing)
            {
                ResetBattleState();
                return;
            }

            RecordPreviouslySummonedMechs();
            CalculateFightState();
            EvaluatePhase();
        }

        /// <summary>
        /// Resets various battle state variables in this class due to the Exo Mechs battle not happening.
        /// </summary>
        private static void ResetBattleState()
        {
            if (PreviouslySummonedMechIDs.Count >= 1)
                PreviouslySummonedMechIDs.Clear();

            FightOngoing = false;
            CurrentPhase = new(0, false, UndefinedPhaseTransitionCondition, null);
            FightState = ExoMechFightState.UndefinedFightState;
            ExoTwinsStateManager.SharedState.ResetForEntireBattle();

            if (Main.LocalPlayer.TryGetModPlayer(out ExoMechDamageRecorderPlayer recorderPlayer) && !NPC.AnyNPCs(ModContent.NPCType<Draedon>()))
                recorderPlayer.ResetIncurredDamage();
        }

        /// <summary>
        /// Records all Exo Mechs that were previously summoned in the <see cref="PreviouslySummonedMechIDs"/> registry.
        /// </summary>
        private static void RecordPreviouslySummonedMechs()
        {
            foreach (int exoMechID in ExoMechNPCIDs.ExoMechIDs)
            {
                if (!PreviouslySummonedMechIDs.Contains(exoMechID) && NPC.AnyNPCs(exoMechID))
                    PreviouslySummonedMechIDs.Add(exoMechID);
            }
        }

        /// <summary>
        /// Attempts to find the mech that the player initially chose at the start of the fight.
        /// </summary>
        /// <param name="primaryMech">The primary mech that was found. Is null if it was not found.</param>
        private static bool TryFindPrimaryMech(out NPC? primaryMech)
        {
            primaryMech = null;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || !IsPrimaryMech(npc))
                    continue;

                primaryMech = npc;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Summons all Exo Mechs not currently present.
        /// </summary>
        internal static void SummonNotPresentExoMechs()
        {
            Player toSummonNear = Main.player[Player.FindClosest(new(Main.maxTilesX * 8f, (float)Main.worldSurface * 16f), 1, 1)];
            foreach (int exoMechID in ExoMechNPCIDs.ManagingExoMechIDs)
            {
                if (NPC.AnyNPCs(exoMechID))
                    continue;

                NPC.NewNPC(new EntitySource_WorldEvent(), (int)toSummonNear.Center.X, (int)toSummonNear.Center.Y - 1000, exoMechID, 1);
            }
        }

        /// <summary>
        /// Applies a given action to all present Exo Mechs.
        /// </summary>
        /// <param name="action">The action to apply.</param>
        public static void ApplyToAllExoMechs(Action<NPC> action)
        {
            foreach (NPC npc in Main.ActiveNPCs)
            {
                if (!ExoMechNPCIDs.ExoMechIDs.Contains(npc.type))
                    continue;

                action(npc);
            }
        }

        /// <summary>
        /// Evaluates whether an NPC is a primary Exo Mech or not, based on whether it's a managing Exo Mech and has the appropriate AI flag.
        /// </summary>
        /// <param name="npc">The NPC to evaluate.</param>
        public static bool IsPrimaryMech(NPC npc) => npc.TryGetBehavior(out NPCBehaviorOverride b) && b is IExoMech exoMech && exoMech.IsPrimaryMech;

        /// <summary>
        /// Marks an NPC as the primary mech.
        /// </summary>
        /// 
        /// <remarks>
        /// As a sanity check, this method will not do anything if <paramref name="npc"/> is not a managing Exo Mech.
        /// </remarks>
        /// <param name="npc">The NPC to try to turn into a primary mech.</param>
        public static void TryToMakePrimaryMech(NPC npc)
        {
            if (!ExoMechNPCIDs.IsManagingExoMech(npc))
                return;

            if (!npc.TryGetBehavior(out NPCBehaviorOverride b) || b is not IExoMech exoMech)
                return;

            exoMech.IsPrimaryMech = true;
            npc.netUpdate = true;
        }

        /// <summary>
        /// Clears all Exo Mech projectiles.
        /// </summary>
        public static void ClearExoMechProjectiles()
        {
            IProjOwnedByBoss<AresBody>.KillAll();
            IProjOwnedByBoss<ThanatosHead>.KillAll();
            IProjOwnedByBoss<Artemis>.KillAll();
            IProjOwnedByBoss<Apollo>.KillAll();
            IProjOwnedByBoss<Draedon>.KillAll();
        }
    }
}
