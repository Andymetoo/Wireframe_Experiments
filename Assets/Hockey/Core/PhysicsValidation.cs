using System;
using System.Collections.Generic;
using System.Numerics;

namespace ColdSnap
{
    /// <summary>Deterministic behavioural checks; no Unity objects or physics engine required.</summary>
    public static class PhysicsValidation
    {
        public static void Run(Action<bool, string> check)
        {
            const float dt = 1f / 120;
            var empty = new List<SkaterState>();
            var s = new SkaterState { Id = 0 };
            HockeyInput forward = new HockeyInput { Move = Vector2.UnitY, Aim = Vector2.UnitY };
            Skate(s, forward, 4);
            check(s.Speed > 7 && s.Speed < 8.5f, "Skating accelerates toward a bounded cruising speed");
            float cruising = s.Speed;
            Skate(s, new HockeyInput { Aim = Vector2.UnitY }, 2);
            check(s.Speed > cruising * .9f && s.Speed < cruising, "Coasting retains ice momentum but gradually slows");
            Skate(s, new HockeyInput { Aim = Vector2.UnitY, Brake = true }, 1.5f);
            check(s.Speed < .15f, "Edges brake to a controlled stop");
            s.Velocity = new Vector2(0, 8);
            Skate(s, new HockeyInput { Aim = Vector2.UnitY, Move = -Vector2.UnitY }, .15f);
            check(s.Velocity.Y > 5, "Reverse input cannot instantly reverse momentum");
            Skate(s, new HockeyInput { Aim = Vector2.UnitY, Move = -Vector2.UnitY }, 2);
            check(s.Velocity.Y < -3, "Sustained reverse input enables backward skating");
            s.Velocity = new Vector2(0, 8); s.Facing = 0;
            Skate(s, new HockeyInput { Aim = Vector2.UnitX, Move = Vector2.UnitX }, .1f);
            check(s.Velocity.Y > 6 && s.Velocity.X > .3f && s.Velocity.X < 2, "Lateral steering bends a trajectory without snapping it");
            check(s.Facing > 0 && s.Facing < .4f, "Body rotation obeys a speed dependent yaw limit");
            s = new SkaterState(); Skate(s, new HockeyInput { Move = Vector2.UnitY, Aim = Vector2.UnitY, Sprint = true }, 5);
            check(s.Speed > cruising + 1.7f && s.Speed < 11.3f, "Sprint produces faster sustained skating");
            s.Position = new Vector2(13.5f, 0); s.Velocity = new Vector2(10, 2);
            for (int i = 0; i < 60; i++) Skating.Step(s, new HockeyInput { Aim = Vector2.UnitY }, dt);
            check(RinkPhysics.Inside(s.Position, RinkPhysics.SkaterRadius - .001f) && s.Velocity.X <= 0, "Boards contain skaters and preserve tangential sliding");

            var a = new SkaterState { Position = new Vector2(-.2f, 0), Velocity = new Vector2(3, 0) };
            var b = new SkaterState { Id = 1, Position = new Vector2(.2f, 0), Velocity = new Vector2(-3, 0) };
            RinkPhysics.ResolvePlayers(new[] { a, b });
            check(Vector2.Distance(a.Position, b.Position) >= .799f && a.Velocity.X < 0 && b.Velocity.X > 0, "Skaters separate and exchange collision momentum");

            // A legal skater pressed against the boards leaves less than one puck diameter
            // behind the skates. Body correction must not eject the puck through the rink.
            var boardSkater = new SkaterState { Id = 4, Position = new Vector2(13.6f, 0) };
            var boardPuck = new PuckState { Position = new Vector2(13.85f, 0), Velocity = new Vector2(0, 6) };
            bool boardContactContained = true;
            for (int i = 0; i < 120; i++)
            {
                PuckPhysics.Step(boardPuck, new[] { boardSkater }, dt);
                boardContactContained &= RinkPhysics.Inside(boardPuck.Position, RinkPhysics.PuckRadius - .001f);
            }
            check(boardContactContained, "Player contact at a straight board cannot project the puck outside the rink");
            check(boardPuck.Position.Y > 3 && Vector2.Distance(boardPuck.Position, boardSkater.Position) > 1,
                "A board-side puck retains tangential travel and escapes the skater contact pocket");
            Vector2 diagonal = Vector2.Normalize(Vector2.One), cornerCenter = new Vector2(8, 23);
            boardSkater.Position = cornerCenter + diagonal * (RinkPhysics.CornerRadius - RinkPhysics.SkaterRadius);
            boardPuck.Position = cornerCenter + diagonal * (RinkPhysics.CornerRadius - RinkPhysics.PuckRadius - .02f);
            boardPuck.Velocity = new Vector2(4, -4);
            bool cornerContactContained = true;
            for (int i = 0; i < 120; i++)
            {
                PuckPhysics.Step(boardPuck, new[] { boardSkater }, dt);
                cornerContactContained &= RinkPhysics.Inside(boardPuck.Position, RinkPhysics.PuckRadius - .001f);
            }
            check(cornerContactContained && HockeyMath.Finite(boardPuck.Velocity),
                "Player contact in a rounded corner preserves the rink constraint throughout every fixed step");

            bool netSidePreserved = true;
            for (int side = -1; side <= 1; side += 2)
            {
                var netSkater = new SkaterState { Id = 4, Position = new Vector2(side * 2.22f, 26.2f) };
                var netPuck = new PuckState { Position = new Vector2(side * 1.96f, 26.2f), Velocity = new Vector2(0, -1) };
                for (int i = 0; i < 30; i++)
                {
                    PuckPhysics.Step(netPuck, new[] { netSkater }, dt);
                    netSidePreserved &= netPuck.Position.X * side >= 1.949f;
                }
            }
            check(netSidePreserved, "Skate contact cannot push an outside puck through either side of solid netting");
            var rearSkater = new SkaterState { Id = 4, Position = new Vector2(0, 27.22f) };
            var rearPuck = new PuckState { Position = new Vector2(0, 26.96f), Velocity = new Vector2(1, 0) };
            bool rearSidePreserved = true;
            for (int i = 0; i < 60; i++)
            {
                PuckPhysics.Step(rearPuck, new[] { rearSkater }, dt);
                rearSidePreserved &= rearPuck.Position.Y >= 26.949f;
            }
            check(rearSidePreserved && rearPuck.Position.X > .3f,
                "Rear-net contact preserves the original side while allowing a puck to slide free");
            var postSkater = new SkaterState { Id = 4, Position = new Vector2(1.8f, 24.7f) };
            var postPuck = new PuckState { Position = new Vector2(1.8f, 24.79f) };
            PuckPhysics.Step(postPuck, new[] { postSkater }, dt);
            check(postPuck.Position.Y <= 24.801f && Vector2.Distance(postPuck.Position, new Vector2(1.8f, 25)) >= .199f,
                "Body separation cannot tunnel a resting puck through a goalpost");
            var mouthSkater = new SkaterState { Id = 4, Position = new Vector2(0, 24.72f) };
            var mouthPuck = new PuckState { Position = new Vector2(0, 25.01f) };
            check(PuckPhysics.Step(mouthPuck, new[] { mouthSkater }, dt) == 0,
                "The open goal mouth still permits a physical contact deflection to score");
            var blockedStick = new SkaterState { Id = 4, Position = new Vector2(2.35f, 26), Facing = -(float)Math.PI / 2 };
            var netInsidePuck = new PuckState { Position = new Vector2(1.15f, 26) };
            check(!PuckPhysics.CanReach(blockedStick, netInsidePuck) && PuckPhysics.Guide(netInsidePuck, new[] { blockedStick }, dt) == -1
                && !PuckPhysics.Strike(blockedStick, netInsidePuck, -Vector2.UnitX, 1, false),
                "Stick control and shots cannot reach through the side netting");
            blockedStick.Position = new Vector2(0, 27.4f); blockedStick.Facing = (float)Math.PI;
            netInsidePuck.Position = new Vector2(0, 26.1f);
            check(!PuckPhysics.CanReach(blockedStick, netInsidePuck), "The back netting blocks stick contact from behind the goal");
            blockedStick.Position = new Vector2(0, 24.2f); blockedStick.Facing = 0;
            netInsidePuck.Position = new Vector2(0, 25.45f);
            check(PuckPhysics.CanReach(blockedStick, netInsidePuck), "An unobstructed stick can still reach through the open goal mouth");

            var p = new PuckState { Velocity = new Vector2(10, 0) };
            for (int i = 0; i < 60; i++) PuckPhysics.Step(p, empty, dt);
            check(p.Position.X > 4.8f && p.Velocity.X < 10 && p.Velocity.X > 9.6f, "Puck slides with low continuous ice friction");
            p = new PuckState { Position = new Vector2(13.7f, 0), Velocity = new Vector2(20, 2) };
            PuckPhysics.Step(p, empty, .05f);
            check(p.Velocity.X < -15 && p.Velocity.Y > 1.8f && RinkPhysics.Inside(p.Position, RinkPhysics.PuckRadius - .001f), "Board rebound loses normal energy and retains tangential motion");
            p = new PuckState { Position = new Vector2(12, 27), Velocity = new Vector2(20, 20) };
            for (int i = 0; i < 30; i++) PuckPhysics.Step(p, empty, dt);
            check(RinkPhysics.Inside(p.Position, RinkPhysics.PuckRadius - .001f) && HockeyMath.Finite(p.Velocity), "Rounded corners deflect without trapping or NaN state");

            p = new PuckState { Position = new Vector2(0, 23), Velocity = new Vector2(0, 36) };
            int goal = -1;
            for (int i = 0; i < 20 && goal < 0; i++) goal = PuckPhysics.Step(p, empty, dt);
            check(goal == 0, "A real northbound shot crossing the full goal line scores team zero");
            p = new PuckState { Position = new Vector2(0, -23), Velocity = new Vector2(0, -36) };
            goal = -1;
            for (int i = 0; i < 20 && goal < 0; i++) goal = PuckPhysics.Step(p, empty, dt);
            check(goal == 1, "South goal uses the same full-puck crossing rule");
            p = new PuckState { Position = new Vector2(RinkPhysics.GoalHalfWidth, 23), Velocity = new Vector2(0, 36) };
            goal = -1;
            for (int i = 0; i < 30; i++) goal = Math.Max(goal, PuckPhysics.Step(p, empty, dt));
            check(goal < 0 && p.Velocity.Y < 0, "A hard shot hits a post instead of tunnelling into a false goal");
            p = new PuckState { Position = new Vector2(3, 26), Velocity = new Vector2(-30, 0) };
            for (int i = 0; i < 30; i++) PuckPhysics.Step(p, empty, dt);
            check(p.Velocity.X > 0 && p.Position.X > 1.8f, "Side netting cannot be penetrated from outside");
            p = new PuckState { Position = new Vector2(0, 27.5f), Velocity = new Vector2(0, -30) };
            goal = -1;
            for (int i = 0; i < 15; i++) goal = Math.Max(goal, PuckPhysics.Step(p, empty, dt));
            check(goal < 0 && p.Velocity.Y > 0, "Back netting rebounds the puck without awarding a goal");

            s = new SkaterState { Id = 3, Team = 0 };
            p = new PuckState { Position = new Vector2(.2f, 1.2f), Velocity = new Vector2(1, 0) };
            Vector2 before = p.Position, velocityBefore = p.Velocity;
            PuckPhysics.Guide(p, new[] { s }, dt);
            check(p.Position == before && (p.Velocity - velocityBefore).Length() <= PuckPhysics.GuideAcceleration * dt + .001f, "Stick guide changes velocity with bounded force and never teleports");
            for (int i = 0; i < 180; i++) { PuckPhysics.Guide(p, new[] { s }, dt); PuckPhysics.Step(p, new[] { s }, dt); }
            check(Vector2.Distance(p.Position, new Vector2(.14f, 1.07f)) < .2f && p.Velocity.Length() < .5f, "Contact guide settles without persistent jitter");
            p.Velocity = new Vector2(35, 0);
            PuckPhysics.Guide(p, new[] { s }, dt);
            check(p.ControllerId == -1 && p.Velocity.X == 35, "A hard deflection breaks contact control");
            p.Position = new Vector2(0, 4);
            check(!PuckPhysics.Strike(s, p, Vector2.UnitY, 1, false), "A skater cannot shoot from outside stick reach");
            p.Position = new Vector2(0, 1); p.Velocity = Vector2.Zero;
            check(PuckPhysics.Strike(s, p, Vector2.UnitY, 1, false) && p.Velocity.Y > 35, "A reachable charged shot supplies physical forward momentum");
            Vector2 after = p.Velocity;
            check(!PuckPhysics.Strike(s, p, Vector2.UnitX, 1, false) && p.Velocity == after, "Stick cooldown rejects duplicate impulses");
            s.StickCooldown = 0; s.Cooldown = 0; p.Velocity = Vector2.Zero;
            check(PuckPhysics.Strike(s, p, Vector2.UnitX, 1, true) && p.Velocity.X > 16 && p.Velocity.X < 17 && Math.Abs(p.Velocity.Y) < .001f, "Pass direction and lower impulse are intentional");

            var keeper = new SkaterState { Id = 7, Team = 1, Goalie = true, Position = new Vector2(0, 23.5f), Facing = (float)Math.PI };
            p = new PuckState { Position = new Vector2(.55f, 21), Velocity = new Vector2(0, 30) };
            for (int i = 0; i < 24; i++) PuckPhysics.Step(p, new[] { keeper }, dt);
            check(p.Velocity.Y < 0 && p.LastCollisionId == 7 && p.CollisionSerial > 0 && p.LastTouchTeam == 1, "Moving shots rebound from the goalie's physical pads with save feedback");
            check(PuckPhysics.Guide(p, new[] { keeper }, dt) == -1, "Goalies cannot remotely possess a puck");

            // Stress corners and netting at maximum legal shot speed from reproducible directions.
            var random = new Random(7793);
            bool finite = true, inside = true;
            for (int trial = 0; trial < 80; trial++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                p = new PuckState { Position = new Vector2((float)random.NextDouble() * 16 - 8, (float)random.NextDouble() * 40 - 20), Velocity = new Vector2((float)Math.Sin(angle), (float)Math.Cos(angle)) * 40 };
                for (int i = 0; i < 120; i++)
                {
                    PuckPhysics.Step(p, empty, dt);
                    finite &= HockeyMath.Finite(p.Position) && HockeyMath.Finite(p.Velocity);
                    inside &= RinkPhysics.Inside(p.Position, RinkPhysics.PuckRadius - .003f);
                }
            }
            check(finite && inside, "Eighty seeded high-speed ricochet trajectories remain finite and inside the rink");
        }

        private static void Skate(SkaterState s, HockeyInput input, float duration)
        {
            for (int i = 0; i < (int)Math.Ceiling(duration * 120); i++)
            {
                // Isolate dynamics from board encounters in the small test rink.
                s.Position = Vector2.Zero;
                Skating.Step(s, input, 1f / 120);
            }
        }
    }
}
