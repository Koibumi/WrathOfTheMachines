﻿using System;
using CalamityMod;
using CalamityMod.NPCs.ExoMechs.Ares;
using CalamityMod.Skies;
using Luminance.Assets;
using Luminance.Common.DataStructures;
using Luminance.Common.Utilities;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace WoTM.Content.NPCs.ExoMechs.Projectiles
{
    public class GaussNuke : ModProjectile, IExoMechProjectile, IProjOwnedByBoss<AresBody>
    {
        public bool SetActiveFalseInsteadOfKill => true;

        public ExoMechDamageSource DamageType => ExoMechDamageSource.BluntForceTrauma;

        /// <summary>
        /// How long this nuke should exist before exploding, in frames.
        /// </summary>
        public static int Lifetime => AresBodyBehaviorOverride.NukeAoEAndPlasmaBlasts_NukeExplosionDelay;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 12;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 4;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 20000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 100;
            Projectile.height = 100;
            Projectile.hostile = true;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.Calamity().DealsDefenseDamage = true;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI()
        {
            Projectile.frameCounter++;
            Projectile.frame = Projectile.frameCounter / 6 % Main.projFrames[Projectile.type];
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            if (!Projectile.WithinRange(target.Center, 100f))
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.SafeDirectionTo(target.Center) * 7f, 0.13f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Main.spriteBatch.EnterShaderRegion();
            DrawAreaOfEffectTelegraph();
            Main.spriteBatch.ExitShaderRegion();

            Texture2D glowmask = ModContent.Request<Texture2D>("CalamityMod/Projectiles/Boss/AresGaussNukeProjectileGlow").Value;
            Utilities.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], lightColor, 1);
            Utilities.DrawAfterimagesCentered(Projectile, ProjectileID.Sets.TrailingMode[Projectile.type], Color.White, 1, texture: glowmask);

            return false;
        }

        public void DrawAreaOfEffectTelegraph()
        {
            float lifetimeRatio = 1f - Projectile.timeLeft / (float)Lifetime;
            float opacity = Utilities.Saturate(lifetimeRatio * 8f) * 0.36f;
            float maxFlashIntensity = Utilities.InverseLerp(0.25f, 0.75f, lifetimeRatio);
            float flashColorInterpolant = Utilities.Cos01(Main.GlobalTimeWrappedHourly * 10f).Squared() * maxFlashIntensity;
            Color innerColor = Color.Lerp(Color.Goldenrod, Color.Gold, MathF.Pow(Utilities.Sin01(Main.GlobalTimeWrappedHourly), 3f) * 0.85f);
            Color edgeColor = Color.Lerp(Color.Yellow, Color.Wheat, 0.6f);

            innerColor = Color.Lerp(innerColor, Color.Crimson, MathF.Pow(flashColorInterpolant, 0.7f));
            edgeColor = Color.Lerp(edgeColor, Color.Red, flashColorInterpolant);

            var aoeShader = GameShaders.Misc["CalamityMod:CircularAoETelegraph"];
            aoeShader.UseOpacity(opacity);
            aoeShader.UseColor(innerColor);
            aoeShader.UseSecondaryColor(edgeColor);
            aoeShader.UseSaturation(lifetimeRatio);
            aoeShader.Apply();

            float explosionDiameter = AresBodyBehaviorOverride.NukeAoEAndPlasmaBlasts_NukeExplosionDiameter * MathF.Pow(Utilities.InverseLerp(0f, 0.25f, lifetimeRatio), 1.6f);
            Texture2D pixel = MiscTexturesRegistry.InvisiblePixel.Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(pixel, drawPosition, null, Color.White, 0, pixel.Size() * 0.5f, Vector2.One * explosionDiameter / pixel.Size(), 0, 0);
        }

        // This IS a heavy chunk of metal, and as such it should do damage as it's flying forward, but otherwise it should just sit in place.
        // It'd be rather silly for a nuke that's just sitting in place to do damage.
        public override bool? CanDamage() => Projectile.velocity.Length() >= 8f;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => Utilities.CircularHitboxCollision(Projectile.Center, 45f, targetHitbox);

        public override void OnKill(int timeLeft)
        {
            ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 15f, intensityTaperStartDistance: 3000f, intensityTaperEndDistance: 6000f);
            SoundEngine.PlaySound(AresGaussNuke.NukeExplosionSound, Projectile.Center);

            if (Main.netMode != NetmodeID.Server)
            {
                Mod calamity = ModContent.GetInstance<CalamityMod.CalamityMod>();
                Gore.NewGore(Projectile.GetSource_Death(), Projectile.Center, Projectile.velocity, calamity.Find<ModGore>("AresGaussNuke1").Type, Projectile.scale);
                Gore.NewGore(Projectile.GetSource_Death(), Projectile.Center, Projectile.velocity, calamity.Find<ModGore>("AresGaussNuke3").Type, Projectile.scale);
            }

            ExoMechsSky.CreateLightningBolt(12);

            if (Main.netMode != NetmodeID.MultiplayerClient)
                Utilities.NewProjectileBetter(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, ModContent.ProjectileType<GaussNukeBoom>(), AresBodyBehaviorOverride.NukeExplosionDamage, 0f);
        }
    }
}
