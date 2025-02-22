﻿using System;
using Luminance.Common.Utilities;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace WoTM.Content.Items.SurgeDriver
{
    public class SurgeDriverProjectile : ModProjectile
    {
        /// <summary>
        /// The owner of this gun.
        /// </summary>
        public Player Owner => Main.player[Projectile.owner];

        /// <summary>
        /// The shoot timer for this gun. Once it exceeds a certain threshold, the gun fires.
        /// </summary>
        public ref float ShootTimer => ref Projectile.ai[0];

        /// <summary>
        /// How many shots have been fired by this gun so far.
        /// </summary>
        public ref float ShootCounter => ref Projectile.ai[1];

        /// <summary>
        /// The amount of positional recoil on this gun.
        /// </summary>
        public ref float RecoilDistance => ref Projectile.ai[2];

        public static readonly SoundStyle FireSound = new SoundStyle("WoTM/Assets/Sounds/Custom/ItemReworks/SurgeDriverFire", 3) with { PitchVariance = 0.176f };

        public override string Texture => "CalamityMod/Items/Weapons/Ranged/SurgeDriver";

        public override void SetDefaults()
        {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
            Projectile.MaxUpdates = 2;
            Projectile.tileCollide = false;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void AI()
        {
            AimTowardsMouse();
            ManipulatePlayerValues();

            // Stay alive so long as the owner is using the item.
            if (Owner.channel)
                Projectile.timeLeft = 2;

            ShootTimer++;
            if (ShootTimer >= Owner.HeldMouseItem().useAnimation * Projectile.MaxUpdates)
            {
                SoundEngine.PlaySound(FireSound with { MaxInstances = 0 }, Projectile.Center);

                Vector2 upwardCorrection = Projectile.velocity.RotatedBy(-MathHelper.PiOver2) * Projectile.scale * Projectile.velocity.X.NonZeroSign() * 20f;
                Vector2 blastSpawnPosition = Projectile.Center + Projectile.velocity * Projectile.scale * 160f + upwardCorrection;
                if (Main.myPlayer == Projectile.owner)
                {
                    ScreenShakeSystem.StartShakeAtPoint(Owner.Center, 2.2f);

                    int lifetime = 15;
                    float hue = Main.rand.NextFloat(0.053f, 0.123f);
                    if (ShootCounter % 2f == 0f)
                        hue += 0.33f;

                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), blastSpawnPosition, Projectile.velocity, ModContent.ProjectileType<SurgeDriverBlast>(), Projectile.damage, Projectile.knockBack, Projectile.owner, lifetime, hue);
                }

                ShootCounter++;
                ShootTimer = 0f;
                Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.velocity.X.NonZeroSign() * -0.09f);
                RecoilDistance -= 15f;
                Projectile.netUpdate = true;
            }

            RecoilDistance *= 0.81f;
        }

        /// <summary>
        /// Manipulates player hold variables and ensures that this cannon stays attached to the owner.
        /// </summary>
        public void ManipulatePlayerValues()
        {
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Owner.heldProj = Projectile.whoAmI;
            Owner.ChangeDir(Projectile.velocity.X.NonZeroSign());
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.velocity.ToRotation() - MathHelper.PiOver2);

            Projectile.Center = Owner.RotatedRelativePoint(Owner.MountedCenter + Projectile.velocity * RecoilDistance);
        }

        /// <summary>
        /// Makes this cannon aim towards the mouse.
        /// </summary>
        public void AimTowardsMouse()
        {
            if (Main.myPlayer == Projectile.owner)
            {
                Vector2 idealDirection = Projectile.SafeDirectionTo(Main.MouseWorld);
                Vector2 newDirection = Vector2.Lerp(Projectile.velocity, idealDirection, 0.15f).SafeNormalize(Vector2.Zero);
                if (Projectile.velocity != newDirection)
                {
                    Projectile.velocity = newDirection;
                    Projectile.netUpdate = true;
                    Projectile.netSpam = 0;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            float rotation = Projectile.rotation;
            SpriteEffects direction = SpriteEffects.None;
            Vector2 origin = new(0.14f, 0.71f);
            if (MathF.Cos(rotation) < 0f)
            {
                origin.X = 1f - origin.X;
                rotation += MathHelper.Pi;
                direction = SpriteEffects.FlipHorizontally;
            }

            Main.EntitySpriteDraw(texture, drawPosition, null, Projectile.GetAlpha(Color.White), rotation, texture.Size() * origin, Projectile.scale, direction);

            return false;
        }

        public override bool? CanDamage() => false;
    }
}
