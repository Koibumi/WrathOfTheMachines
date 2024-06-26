﻿using System;
using System.Collections.Generic;
using Luminance.Assets;
using Luminance.Common.Utilities;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace WoTM.Content.NPCs.ExoMechs
{
    public static class CommonExoTwinFunctionalities
    {
        /// <summary>
        /// How perpendicularly offset the optic nerves next to the Exo Twins' thrusters are at the start of the rendered primitive.
        /// </summary>
        public const float StartingOpticNerveExtrusion = 32f;

        /// <summary>
        /// How perpendicularly offset the optic nerves next to the Exo Twins' thrusters are at the end of the rendered primitive.
        /// </summary>
        public const float EndingOpticNerveExtrusion = 6f;

        /// <summary>
        /// How long the optic nerves behind the Exo Twins are.
        /// </summary>
        public const float EndingOpticLength = 540f;

        private static float NerveEndingWidthFunction(float completionRatio)
        {
            float baseWidth = Utilities.InverseLerp(1f, 0.54f, completionRatio) * 6f;
            float endTipWidth = Utilities.Convert01To010(Utilities.InverseLerp(0.96f, 0.83f, completionRatio)) * 6f;
            return baseWidth + endTipWidth;
        }

        /// <summary>
        /// Draws optic nerve endings for a given Exo Twin.
        /// </summary>
        /// <param name="twin">The Exo Twin's NPC instance.</param>
        /// <param name="nerveEndingPalette">The palette for the nerve endings.</param>
        public static void DrawNerveEndings(NPC twin, params Color[] nerveEndingPalette)
        {
            Color nerveEndingColorFunction(float completionRatio)
            {
                float blackInterpolant = Utilities.InverseLerp(0.17f, 0.34f, completionRatio);
                Color paletteColor = Utilities.MulticolorLerp(completionRatio.Squared(), nerveEndingPalette);
                return Color.Lerp(new(0f, 0.1f, 0.2f), paletteColor, blackInterpolant) * twin.Opacity;
            }

            ManagedShader nerveEndingShader = ShaderManager.GetShader("WoTM.ExoTwinNerveEndingShader");
            nerveEndingShader.SetTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/GreyscaleGradients/Neurons"), 1, SamplerState.LinearWrap);

            // Draw nerve endings near the main thruster
            for (int direction = -1; direction <= 1; direction += 2)
            {
                Vector2 backwards = -twin.rotation.ToRotationVector2();
                List<Vector2> nerveDrawPositions = new();

                float totalAngularChange = 0f;
                for (int i = 0; i < 8; i++)
                    totalAngularChange += MathHelper.WrapAngle(twin.rotation - twin.oldRot[i]) / 8f;

                for (int i = 0; i < 8; i++)
                {
                    float completionRatio = i / 7f;
                    float inwardBendInterpolant = Utilities.InverseLerp(0f, 0.38f, completionRatio) * completionRatio;
                    float outwardExtrusion = MathHelper.Lerp(StartingOpticNerveExtrusion, EndingOpticNerveExtrusion, MathF.Pow(inwardBendInterpolant, 1.2f));
                    Vector2 backwardsOffset = backwards.RotatedBy(totalAngularChange * i * -0.14f) * completionRatio * 540f;
                    Vector2 perpendicularOffset = new Vector2(direction * outwardExtrusion, -30f).RotatedBy(twin.oldRot[i] + MathHelper.PiOver2);

                    nerveDrawPositions.Add(twin.Center + backwardsOffset + perpendicularOffset);
                }

                PrimitiveSettings settings = new(NerveEndingWidthFunction, nerveEndingColorFunction, Shader: nerveEndingShader);
                PrimitiveRenderer.RenderTrail(nerveDrawPositions, settings, 40);
            }
        }

        /// <summary>
        /// Draws an Exo Twin's wingtip vortices.
        /// </summary>
        /// <param name="twin">The Exo Twin's NPC data.</param>
        /// <param name="twinInterface">The Exo Twin's interfaced data.</param>
        public static void DrawWingtipVortices(NPC twin, IExoTwin twinInterface)
        {
            if (twinInterface.WingtipVorticesOpacity <= 0f || Vector2.Dot(twin.velocity, twin.rotation.ToRotationVector2()) < 0f)
                return;

            float windWidthFunction(float completionRatio) => 6f - completionRatio * 5f;
            Color windColorFunction(float completionRatio)
            {
                Color baseColor = twin.GetAlpha(Color.Gray);
                float completionRatioOpacity = MathF.Pow(1f - completionRatio, (1f - twinInterface.WingtipVorticesOpacity) * 5f + 1f);
                float generalOpacity = twin.Opacity * twinInterface.WingtipVorticesOpacity * 0.65f;
                return baseColor * completionRatioOpacity * generalOpacity;
            }

            ManagedShader windShader = ShaderManager.GetShader("WoTM.WingtipVortexTrailShader");
            windShader.SetTexture(ModContent.Request<Texture2D>("CalamityMod/ExtraTextures/Trails/BasicTrail"), 1, SamplerState.LinearWrap);

            PrimitivePixelationSystem.RenderToPrimsNextFrame(() =>
            {
                bool isArtemis = twin.type == ExoMechNPCIDs.ArtemisID;
                Vector2 forward = Vector2.UnitY.RotatedBy(twin.rotation + MathHelper.PiOver2) * twin.scale * (isArtemis ? 10f : 32f);
                Vector2 side = Vector2.UnitX.RotatedBy(twin.rotation + MathHelper.PiOver2) * -twin.scale * 96f;

                PrimitiveSettings leftSettings = new(windWidthFunction, windColorFunction, _ =>
                {
                    return twin.Size * 0.5f - side + forward;
                }, Pixelate: true, Shader: windShader);
                PrimitiveSettings rightSettings = new(windWidthFunction, windColorFunction, _ =>
                {
                    return twin.Size * 0.5f + side + forward;
                }, Pixelate: true, Shader: windShader);

                PrimitiveRenderer.RenderTrail(twin.oldPos, leftSettings, 24);
                PrimitiveRenderer.RenderTrail(twin.oldPos, rightSettings, 24);

            }, PixelationPrimitiveLayer.BeforeNPCs);
        }

        /// <summary>
        /// Draws an Exo Twin's back thrusters.
        /// </summary>
        /// <param name="twin">The Exo Twin's NPC data.</param>
        /// <param name="twinInterface">The Exo Twin's interfaced data.</param>
        public static void DrawThrusters(NPC twin, IExoTwin twinInterface)
        {
            float thrusterWidth = twinInterface.ThrusterBoost * 20f + 15f;

            float thrusterWidthFunction(float completionRatio)
            {
                float thrusterPulse = Utilities.Cos01(Main.GlobalTimeWrappedHourly * -42.3f + twin.whoAmI + completionRatio * 5f) * (1f - completionRatio) * 0.5f;
                return (MathHelper.Lerp(1f, 0.1f, MathF.Pow(completionRatio, 0.5f)) + thrusterPulse) * thrusterWidth;
            }
            Color thrusterColorFunction(float completionRatio)
            {
                Color baseColor = twin.GetAlpha(new(0.3f, 0.5f, 1f));
                float completionRatioOpacity = MathF.Pow(Utilities.InverseLerp(0.9f, 0f, completionRatio), 1.85f);
                float generalOpacity = twin.Opacity;
                return baseColor * completionRatioOpacity * generalOpacity;
            }

            // Draw some bloom over everything.
            Vector2 thrusterBloomPosition = twin.Center - Main.screenPosition - twin.rotation.ToRotationVector2() * twin.scale * 12f;
            Texture2D thrusterBloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            Color thrusterBloomColor = Color.SkyBlue * (twinInterface.ThrusterBoost * 0.6f + 0.33f);
            thrusterBloomColor.A = 0;
            Main.spriteBatch.Draw(thrusterBloom, thrusterBloomPosition, null, thrusterBloomColor, 0f, thrusterBloom.Size() * 0.5f, 0.5f, 0, 0f);
            Main.spriteBatch.Draw(thrusterBloom, thrusterBloomPosition, null, thrusterBloomColor * 0.5f, 0f, thrusterBloom.Size() * 0.5f, 1f, 0, 0f);

            PrimitivePixelationSystem.RenderToPrimsNextFrame(() =>
            {
                Vector2 backward = Vector2.UnitY.RotatedBy(twin.rotation + MathHelper.PiOver2) * twin.scale * 6f;
                Vector2[] flameTrailCache = new Vector2[twin.oldPos.Length];
                for (int i = 0; i < flameTrailCache.Length; i++)
                {
                    Vector2 oldPosition = twin.oldPos[i];
                    if (oldPosition == Vector2.Zero)
                        continue;

                    flameTrailCache[i] = Vector2.Lerp(oldPosition, twin.position, 0.6f) - twin.rotation.ToRotationVector2() * i / (float)(flameTrailCache.Length - 1f) * 200f;
                }

                ManagedShader shader = ShaderManager.GetShader("WoTM.ExoTwinThrusterShader");
                shader.TrySetParameter("whiteHotNoiseInterpolant", twinInterface.ThrusterBoost);
                shader.SetTexture(MiscTexturesRegistry.DendriticNoiseZoomedOut.Value, 1, SamplerState.LinearWrap);

                PrimitiveSettings settings = new(thrusterWidthFunction, thrusterColorFunction, _ =>
                {
                    return twin.Size * 0.5f + backward;
                }, Pixelate: true, Shader: shader);
                PrimitiveRenderer.RenderTrail(flameTrailCache, settings, 32);

            }, PixelationPrimitiveLayer.BeforeNPCs);
        }

        /// <summary>
        /// Draws an Exo Twin's barest things.
        /// </summary>
        /// <param name="twin">The Exo Twin's NPC data.</param>
        /// <param name="twinInterface">The Exo Twin's interfaced data.</param>
        /// <param name="texture">The base texture.</param>
        /// <param name="glowmask">The glowmask texture.</param>
        /// <param name="lightColor">The color of light at the Exo Twin's position.</param>
        /// <param name="screenPos">The screen position offset.</param>
        /// <param name="frame">The frame of the Exo Twin.</param>
        public static void DrawBase(NPC twin, IExoTwin twinInterface, Texture2D texture, Texture2D glowmask, Color lightColor, Vector2 screenPos, int frame)
        {
            Main.instance.GraphicsDevice.BlendState = BlendState.AlphaBlend;
            DrawNerveEndings(twin, twinInterface.OpticNervePalette);

            Vector2 drawPosition = twin.Center - screenPos;
            Rectangle frameRectangle = texture.Frame(10, 9, frame / 9, frame % 9);

            Main.spriteBatch.PrepareForShaders();

            float[] blurWeights = new float[12];
            for (int i = 0; i < blurWeights.Length; i++)
                blurWeights[i] = Utilities.GaussianDistribution(i / (float)(blurWeights.Length - 1f) * 1.5f, 0.6f);

            ManagedShader shader = ShaderManager.GetShader("WoTM.MotionBlurShader");
            shader.TrySetParameter("blurInterpolant", twinInterface.MotionBlurInterpolant);
            shader.TrySetParameter("blurWeights", blurWeights);
            shader.Apply();

            Vector2 scale = Vector2.One * twin.scale;
            Main.spriteBatch.Draw(texture, drawPosition, frameRectangle, twin.GetAlpha(lightColor), twin.rotation + MathHelper.PiOver2, frameRectangle.Size() * 0.5f, scale, 0, 0f);
            Main.spriteBatch.Draw(glowmask, drawPosition, frameRectangle, twin.GetAlpha(Color.White), twin.rotation + MathHelper.PiOver2, frameRectangle.Size() * 0.5f, scale, 0, 0f);

            Main.spriteBatch.ResetToDefault();

            DrawWingtipVortices(twin, twinInterface);
            DrawThrusters(twin, twinInterface);

            twinInterface.SpecificDrawAction?.Invoke();
        }
    }
}
