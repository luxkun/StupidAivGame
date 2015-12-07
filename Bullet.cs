﻿using System;
using System.Diagnostics;
using System.Drawing;
using Aiv.Engine;
using Futuridium.Spells;
using OpenTK;

namespace Futuridium
{
    public class Bullet : Spell
    {
        private const int MinSpeed = 2;
        
        private readonly bool bounceBullet = false;
        private readonly float bounceDelay = 2.5f;
        private readonly double bounceMod = 0.8; // speed = bounceMod * speed

        private CircleObject body;
        private Vector2 direction;

        private float fadeAwayStep;

        private float lastBounce;

        private Vector2 lastPoint;

        private float virtRadius;

        private float vx;
        private float vy;

        public Bullet()
        {
            order = 6;
            SpellName = "Energy Bullet";

            body = new CircleObject();

            OnDestroy += DestroyEvent;
            OnStart += StartEvent;
            OnUpdate += UpdateEvent;
        }

        public int Radius
        {
            get { return body.radius; }
            set { body.radius = value; }
        }

        private float VirtRadius
        {
            get { return virtRadius; }

            set
            {
                if (value >= 1f && Radius >= 1 + (int) value)
                {
                    var deltaPos = value/2;
                    X += (int) deltaPos;
                    Y += (int) deltaPos;
                    Radius -= (int) value;
                    value -= (int) value;
                    hitBoxes["mass"].height = Radius*2;
                    hitBoxes["mass"].width = Radius*2;
                    if (Radius <= 1)
                        Console.WriteLine(Radius);
                }
                virtRadius = value;
            }
        }

        public float FadeAwayRange { get; set; } = 0.33f;

        public bool Fill
        {
            get { return body.fill; }
            set { body.fill = value; }
        }

        public override int X
        {
            get { return x; }
            set
            {
                x = value;
                body.x = value;
            }
        }

        public override int Y
        {
            get { return y; }
            set
            {
                y = value;
                body.y = value;
            }
        }

        public Color Color
        {
            get { return body.color; }

            set { body.color = value; }
        }

        private void DestroyEvent(object sender)
        {
            var roomName = ((Game) engine.objects["game"]).CurrentFloor.CurrentRoom.name;
            var particleRadius = Radius/2;
            if (particleRadius < 1)
                particleRadius = 1;
            var particleSystem = new ParticleSystem($"{roomName}_{name}_psys", "homogeneous", 30, particleRadius, Color,
                400,
                (int) Speed, Radius)
            {
                order = order,
                x = X,
                y = Y,
                fade = 200
            };
            Debug.WriteLine(particleSystem.name);
            engine.SpawnObject(particleSystem.name, particleSystem);

            body.Destroy();
        }

        private void StartEvent(Object sender)
        {
            base.Start();
            AddHitBox("mass", 0, 0, Radius*2, Radius*2);
            lastPoint = new Vector2(X, Y);
            Fill = true;

            body.name = this.name + "_body";
            engine.SpawnObject(body);
        }

        // simulate collision between two GameObject rectangles
        // returns 0: X collision ; 1: Y collision
        private int SimulateCollision(Collision collision)
        {
            var hitBox1 = hitBoxes[collision.hitBox]; // bullet
            var hitBox2 = collision.other.hitBoxes[collision.otherHitBox];

            var x2 = hitBox2.x + collision.other.x;
            var y2 = hitBox2.y + collision.other.y;
            var w2 = hitBox2.width;
            var h2 = hitBox2.height;
            var w1 = hitBox1.width;
            var h1 = hitBox1.height;

            // should have same abs value
            var diffX = X - (int) lastPoint.X;
            var diffY = Y - (int) lastPoint.Y;
            Debug.Assert(Math.Abs(diffX) == Math.Abs(diffY));
            // ignores first Step
            // could optimize by starting near second hitbox
            var xCollisions = 0;
            var yCollisions = 0;
            var steps = Math.Max(Math.Abs(diffX), Math.Abs(diffY));
            for (var step = steps; step >= 0; step--)
            {
                var x1 = hitBox1.x + X - Math.Sign(diffX)*step;
                var y1 = hitBox1.y + Y - Math.Sign(diffY)*step;

                var tempxCollisions = Math.Min(x2 + w2, x1 + w1) - Math.Max(x2, x1);
                if (y1 != y2 && y1 + h1 != y2 + h2 && y1 != y2 + h2 && y1 + h1 != y2)
                    tempxCollisions = 0;
                var tempyCollisions = Math.Min(y2 + h2, y1 + h1) - Math.Max(y1, y2);
                if (x1 != x2 && x1 + w1 != x2 + w2 && x1 != x2 + w2 && x1 + w1 != x2)
                    tempyCollisions = 0;
                if (tempxCollisions > xCollisions)
                    xCollisions = tempxCollisions;
                if (tempyCollisions > yCollisions)
                    yCollisions = tempyCollisions;
                // keeping tempcollisions for now
                if (yCollisions > 0 || xCollisions > 0)
                    break;
            }
            var result = -1;
            if (yCollisions < xCollisions)
                result = 1;
            else if (yCollisions > xCollisions || (yCollisions == xCollisions && yCollisions > 0))
                result = 0;
            Debug.WriteLine("Collision Simulation result: {0} ({1} vs {2})", result, xCollisions, yCollisions);

            return result;
        }

        // doesn't and won't handle collision between two moving objects
        private bool BounceOrDie(Collision collision)
        {
            if (bounceBullet)
            {
                var otherHitBox = collision.other.hitBoxes[collision.otherHitBox];
                if (lastBounce > 0)
                    return false;
                if (otherHitBox == null)
                {
                    Debug.WriteLine("Collission with non-character objects not supported.");
                }
                else
                {
                    return BounceOrDie(SimulateCollision(collision));
                    /*this.x = (int)lastPoint.X;
					this.y = (int)lastPoint.Y;*/
                }
                return false;
            }
            Destroy();
            return false;
        }

        // collisionDirection: 0: X collision ; 1: Y collision
        private bool BounceOrDie(int collisionDirection)
        {
            if (bounceBullet)
            {
                Debug.WriteLine("Collision Direction:" + collisionDirection);
                if (collisionDirection == 0)
                {
                    direction.X *= -1;
                    Vx = 0;
                }
                else if (collisionDirection == 1)
                {
                    direction.Y *= -1;
                    Vy = 0;
                }
                else
                {
                    // if the collission simulation failed (ex. reason: multiple moving stuff?)
                    //  then bounce back
                    Vx = 0;
                    Vy = 0;
                    direction.X *= -1;
                    direction.Y *= -1;
                }
                Speed = (int) (Speed*bounceMod);
                if (Speed <= MinSpeed)
                    Speed = MinSpeed;
                else
                    RangeToGo = (int) (RangeToGo*bounceMod);
                lastBounce = bounceDelay;
                X = (int) lastPoint.X;
                Y = (int) lastPoint.Y;
                return true;
            }
            Destroy();
            return false;
        }

        private void UpdateEvent(Object sender)
        {
            if (((Game) engine.objects["game"]).MainWindow == "game")
            {
                if (lastBounce > 0)
                    lastBounce -= deltaTime;
                ManageFade();

                var collisions = CheckCollisions();
                if (collisions.Count > 0)
                    Debug.WriteLine("Bullet collides with n." + collisions.Count);
                var bounced = false;
                foreach (var collision in collisions)
                {
                    if (collision.other.name == Owner.name || collision.other.name.StartsWith("bullet") ||
                        collision.other.name.StartsWith("orb"))
                        continue;
                    Debug.WriteLine("Bullet hits enemy: " + collision.other.name);
                    if (BounceOrDie(collision))
                        bounced = true;
                    var other = collision.other as Character;
                    if (other != null)
                    {
                        Owner.DoDamage(this, other, collision);
                        break;
                    }
                }
                if (bounced)
                    NextMove();
                lastPoint = new Vector2(X, Y);
            }
        }

        private void ManageFade()
        {
            if (RangeToGo > FadeAwayRange*Range) return;
            if (fadeAwayStep == 0)
            {
                fadeAwayStep = (Radius - 1)/LifeSpan;
            }
            var deltaRadius = fadeAwayStep*deltaTime;
            if (deltaRadius > 0)
            {
                VirtRadius += deltaRadius;
            }
        }
    }
}