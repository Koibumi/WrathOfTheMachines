﻿using CalamityMod.NPCs;
using Luminance.Assets;
using Luminance.Common.Utilities;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using WoTM.Common.Utilities;

namespace WoTM.Content.NPCs.ExoMechs.Ares
{
    public sealed class AresSilhouetteRenderingSystem : ModSystem
    {
        public override void OnModLoad() => On_Main.DrawProjectiles += DrawAresSilhouetteWrapper;

        private static void DrawAresSilhouetteWrapper(On_Main.orig_DrawProjectiles orig, Main self)
        {
            orig(self);

            if (CalamityGlobalNPC.draedonExoMechPrime == -1)
                return;

            NPC ares = Main.npc[CalamityGlobalNPC.draedonExoMechPrime];
            if (!ares.TryGetBehavior(out AresBodyBehavior aresBehavior))
                return;

            // TODO -- Move this elsewhere.
            if (aresBehavior.MotionBlurInterpolant > 0f)
            {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

                float[] blurWeights = new float[12];
                for (int i = 0; i < blurWeights.Length; i++)
                    blurWeights[i] = LumUtils.GaussianDistribution(i / (float)(blurWeights.Length - 1f) * 1.5f, 0.6f) * 0.81f;
                ManagedShader shader = ShaderManager.GetShader("WoTM.MotionBlurShader");
                shader.TrySetParameter("blurInterpolant", aresBehavior.MotionBlurInterpolant);
                shader.TrySetParameter("blurWeights", blurWeights);
                shader.TrySetParameter("blurDirection", Vector2.UnitY * 7.2f);
                shader.Apply();

                Texture2D aresTarget = AresRenderTargetSystem.AresTarget;
                Vector2 drawPosition = Main.screenLastPosition - Main.screenPosition;
                Main.spriteBatch.Draw(aresTarget, drawPosition, Color.White);

                Main.spriteBatch.End();
            }
            if (aresBehavior.SilhouetteOpacity > 0f)
            {
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                DrawAresSilhouette(ares.Center, aresBehavior.SilhouetteOpacity, aresBehavior.SilhouetteDissolveInterpolant);
                Main.spriteBatch.ResetToDefault();

                aresBehavior.RenderAfterSilhouette();
                Main.spriteBatch.End();
            }
        }

        /// <summary>
        /// Renders Ares' silhouette.
        /// </summary>
        /// <param name="aresCenter">Ares' position in world space.</param>
        /// <param name="opacity">The opacity of the silhouette.</param>
        /// <param name="dissolveInterpolant">How much the silhouette should dissolve.</param>
        private static void DrawAresSilhouette(Vector2 aresCenter, float opacity, float dissolveInterpolant)
        {
            Texture2D aresTarget = AresRenderTargetSystem.AresTarget;
            ManagedShader silhouetteShader = ShaderManager.GetShader("WoTM.AresSilhouetteShader");
            silhouetteShader.TrySetParameter("textureSize0", aresTarget.Size());
            silhouetteShader.TrySetParameter("dissolveInterpolant", dissolveInterpolant);
            silhouetteShader.TrySetParameter("dissolveDirection", Vector2.UnitY);
            silhouetteShader.TrySetParameter("screenPosition", Main.screenPosition);
            silhouetteShader.TrySetParameter("dissolveCenter", LumUtils.WorldSpaceToScreenUV(aresCenter));
            silhouetteShader.SetTexture(MiscTexturesRegistry.TurbulentNoise.Value, 1, SamplerState.LinearWrap);
            silhouetteShader.Apply();

            Vector2 drawPosition = Main.screenLastPosition - Main.screenPosition;
            Main.spriteBatch.Draw(aresTarget, drawPosition, Color.Black * opacity);
        }
    }
}
