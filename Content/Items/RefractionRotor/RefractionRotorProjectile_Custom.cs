﻿using System;
using CalamityMod;
using CalamityMod.NPCs.NormalNPCs;
using CalamityMod.Particles;
using Luminance.Common.Utilities;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using WoTM.Content.Particles;
using WoTM.Content.Particles.Metaballs;
using static WoTM.Content.Items.RefractionRotor.RefractionRotorReworkManager;

namespace WoTM.Content.Items.RefractionRotor
{
    public class RefractionRotorProjectile_Custom : ModProjectile
    {
        /// <summary>
        /// Whether this rotor has hit an enemy or not.
        /// </summary>
        public bool HasHitEnemy
        {
            get => Projectile.ai[0] == 1f;
            set => Projectile.ai[0] = value.ToInt();
        }

        /// <summary>
        /// How long this rotor has existed for, in frames.
        /// </summary>
        public ref float Time => ref Projectile.ai[1];

        /// <summary>
        /// How amount of extra angular spin that should be applied to this rotor.
        /// </summary>
        public ref float ExtraSpin => ref Projectile.localAI[0];

        public override string Texture => "CalamityMod/Items/Weapons/Rogue/RefractionRotor";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 56;
        }

        public override void SetDefaults()
        {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.penetrate = 16;
            Projectile.MaxUpdates = MaxUpdates;
            Projectile.timeLeft = Projectile.MaxUpdates * 45;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = Projectile.MaxUpdates * 2;
            Projectile.Opacity = 0f;
            Projectile.DamageType = ModContent.GetInstance<RogueDamageClass>();
        }

        public override void AI()
        {
            bool slowDown = Time >= Projectile.MaxUpdates * 5f;
            float slowSpeed = SlowedSpeed;
            float decelerationFactor = BaseDecelerationFactor;
            if (HasHitEnemy)
            {
                slowDown = true;
                slowSpeed *= 0.75f;
                decelerationFactor = GrindingDecelerationFactor;
            }

            if (slowDown && Projectile.velocity.Length() > slowSpeed / Projectile.MaxUpdates)
                Projectile.velocity *= decelerationFactor;

            // Spin rapidly.
            float spinSpeed = Projectile.velocity.Length() * 0.1f;
            Projectile.rotation += Projectile.velocity.X.NonZeroSign() * spinSpeed;

            // Scale down into nothing before dying.
            float scaleInterpolant = Utilities.InverseLerpBump(0f, 5f, 34f, 45f, Time / Projectile.MaxUpdates) * Utilities.InverseLerp(0f, 9f, Projectile.penetrate);
            Projectile.scale = MathHelper.SmoothStep(0f, 1f, scaleInterpolant);

            // Fade in after one frame.
            if (Time >= Projectile.MaxUpdates)
                Projectile.Opacity = Utilities.Saturate(Projectile.Opacity + 0.04f);

            Time++;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Vector2 impactOrigin = (Projectile.Center + target.Center) * 0.5f;

            if (target.Organic() && target.type != ModContent.NPCType<SuperDummyNPC>() && target.type != NPCID.TargetDummy)
                CreateBlood(impactOrigin, target);
            else
                CreateSparks(impactOrigin, target);
            ScreenShakeSystem.StartShakeAtPoint(impactOrigin, 0.8f);

            if (HasHitEnemy)
                return;

            HasHitEnemy = true;
            Projectile.netUpdate = true;
        }

        /// <summary>
        /// Creates impact sparks relative to a given target.
        /// </summary>
        /// <param name="impactOrigin">The origin of the impact.</param>
        /// <param name="target">The NPC target that is being hit.</param>
        public void CreateBlood(Vector2 impactOrigin, NPC target)
        {
            BloodMetaball metaball = ModContent.GetInstance<BloodMetaball>();

            for (int i = 0; i < 14; i++)
            {
                float bloodSpread = Main.rand.NextFloatDirection();
                float bloodSpeed = MathHelper.SmoothStep(20f, 4f, MathF.Abs(bloodSpread));
                Vector2 bloodVelocity = (Projectile.AngleTo(target.Center) + MathHelper.PiOver2 + bloodSpread * 0.51f + Main.rand.NextFloatDirection() * 0.05f).ToRotationVector2() * bloodSpeed;
                Vector2 bloodSpawnPosition = Main.rand.NextVector2Circular(target.width, target.height) * 0.1f + impactOrigin;

                float bloodSize = 30f;
                if (Main.rand.NextBool(8))
                    bloodSize *= 0.67f;
                if (Main.rand.NextBool(16))
                    bloodSize *= 1.7f;

                metaball.CreateParticle(bloodSpawnPosition, bloodVelocity, bloodSize, Main.rand.NextFloat(0.05f));
            }
        }

        /// <summary>
        /// Creates impact sparks relative to a given target.
        /// </summary>
        /// <param name="impactOrigin">The origin of the impact.</param>
        /// <param name="target">The NPC target that is being hit.</param>
        public void CreateSparks(Vector2 impactOrigin, NPC target)
        {
            for (int i = 0; i < 22; i++)
            {
                float sparkSpread = Main.rand.NextFloatDirection();
                float sparkSpeed = MathHelper.SmoothStep(30f, 10f, MathF.Abs(sparkSpread));
                int sparkLifetime = (int)MathHelper.SmoothStep(7f, 20f, MathF.Abs(sparkSpread));
                if (Main.rand.NextBool(15))
                    sparkLifetime += 6;
                if (Main.rand.NextBool(75))
                    sparkLifetime += 14;

                Vector2 sparkVelocity = (Projectile.AngleTo(target.Center) + MathHelper.PiOver2 + sparkSpread * 0.35f + Main.rand.NextFloatDirection() * 0.04f).ToRotationVector2() * sparkSpeed;
                Vector2 sparkSpawnPosition = Main.rand.NextVector2Circular(target.width, target.height) * 0.1f + impactOrigin;

                Color[] sparkPalette = new Color[]
                {
                    Color.White,
                    Color.Wheat.HueShift(Main.rand.NextFloat(0.3f)),
                    Color.Orange.HueShift(Main.rand.NextFloat(-0.04f, 0.1f)) * 0.85f,
                    Color.OrangeRed * 0.5f,
                    Color.Transparent,
                };

                MetalSparkParticle spark = new(sparkSpawnPosition, sparkVelocity, sparkPalette, sparkLifetime, new Vector2(0.02f, 0.06f));
                spark.Spawn();
            }

            StrongBloom bloom = new(impactOrigin + Main.rand.NextVector2Circular(30f, 30f), Vector2.Zero, Projectile.GetAlpha(Color.Wheat) * 0.85f, Projectile.scale * 0.7f, 5);
            GeneralParticleHandler.SpawnParticle(bloom);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Texture2D glowmask = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Rogue/RefractionRotorGlowmask").Value;
            Main.spriteBatch.PrepareForShaders();

            float speed = Projectile.velocity.Length() * Projectile.MaxUpdates;
            float blurInterpolant = Utilities.InverseLerp(6f, 60f, speed);
            float glowIntensity = blurInterpolant * MathHelper.Lerp(0.8f, 1.1f, Utilities.Cos01(Main.GlobalTimeWrappedHourly * 60f)) * 0.019f;

            float spinIncrement = Utilities.InverseLerp(0.1f, 1f, blurInterpolant) * Projectile.velocity.X.NonZeroSign() * 10f;
            ExtraSpin = (ExtraSpin + spinIncrement) % MathHelper.TwoPi;

            Vector4[] glowPalette = new Vector4[7];
            for (int i = 0; i < glowPalette.Length; i++)
            {
                float hue = (i / (float)glowPalette.Length + Main.GlobalTimeWrappedHourly * 5f + Projectile.identity * 0.3f) % 0.99f;
                glowPalette[i] = Projectile.GetAlpha(Utilities.MulticolorLerp(hue, CalamityUtils.ExoPalette)).ToVector4();
            }

            ManagedShader rotorShader = ShaderManager.GetShader("WoTM.RefractionRotorShader");
            rotorShader.TrySetParameter("blur", blurInterpolant * 0.04f);
            rotorShader.TrySetParameter("spin", ExtraSpin);
            rotorShader.TrySetParameter("glowIntensity", glowIntensity);
            rotorShader.TrySetParameter("gradient", glowPalette);
            rotorShader.TrySetParameter("gradientCount", glowPalette.Length);
            rotorShader.TrySetParameter("pixelationFactor", Vector2.One * 1.05f / texture.Size());
            rotorShader.TrySetParameter("sizeCorrection", Vector2.One * MathF.Max(texture.Width, texture.Height) / MathF.Min(texture.Width, texture.Height));
            rotorShader.Apply();

            Vector2 origin = new(71f, 64f);
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i -= 3)
            {
                float afterimageInterpolant = Utilities.InverseLerp(Projectile.oldPos.Length, 0f, i);
                float afterimageOpacity = MathF.Pow(afterimageInterpolant, 3.5f);

                Vector2 drawPosition = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.spriteBatch.Draw(texture, drawPosition, null, Projectile.GetAlpha(lightColor) * afterimageOpacity, Projectile.rotation, origin, Projectile.scale, 0, 0f);
                Main.spriteBatch.Draw(glowmask, drawPosition, null, Projectile.GetAlpha(Color.White) * afterimageOpacity, Projectile.rotation, origin, Projectile.scale, 0, 0f);
            }

            Main.spriteBatch.ResetToDefault();
            return false;
        }
    }
}
