using System;
using System.Collections.Generic;
using System.Numerics;

namespace Meridian
{
    /// <summary>Persistent tactical intentions layered on the original bank/elevator controller.
    /// No branch writes aircraft transforms, velocity, health or ammunition.</summary>
    public sealed partial class PilotAI
    {
        public int TargetId => targetId;
        public int TargetSwitches { get; private set; }
        public int StateTransitions { get; private set; }
        public float TimeSlow { get; private set; }
        public float FormationError { get; private set; }
        public string Personality => Skill == PilotSkill.Ace ? "Ace / energy fighter" : aggression > .91f ? "Bold" : aggression < .82f ? "Cautious" : "Steady";
        public float AwarenessRange => 6200;
        float targetCommit, contactMemory, defenseCooldown, energyCooldown, gunPitchIntegral;
        int orderRevision = -1, groundTargetId = -1;
        Vector3 lastContact, runDirection;
        bool rejoining, groundAttackRun, groundPullout, groundAlign;
        Vector3 acePreviousVelocity, aceTargetAcceleration;
        int aceTrackedTarget = -1;

        public FlightInput Update(AircraftState self, IList<AircraftState> aircraft, TerrainField terrain,
            PilotOrders orders, float dt)
        {
            if (orders == null) return Update(self, aircraft, terrain, Vector3.Zero, dt);
            elapsed += dt; thinkTimer -= dt; maneuverTimer -= dt; pursuitTime += dt;
            targetCommit -= dt; contactMemory -= dt; defenseCooldown -= dt; energyCooldown -= dt;
            if (self.Speed < self.Definition.StallSpeed * 1.16f) TimeSlow += dt;
            if (orderRevision != orders.Revision)
            {
                orderRevision = orders.Revision; thinkTimer = 0; targetCommit = 0;
                if (State != "Terrain escape" && State != "Recover airspeed" && State != "Break") maneuverTimer = 0;
                // A fresh target designation must not cancel a dive's safety exit.
                if (orders.Order != SquadronOrder.Engage)
                { groundAttackRun = false; groundPullout = false; groundAlign = false; groundTargetId = -1; }
            }
            if (thinkTimer <= 0)
            {
                string before = State;
                int previousTarget = targetId;
                thinkTimer = Skill == PilotSkill.Ace ? .11f + (float)random.NextDouble() * .06f
                    : .19f + (float)random.NextDouble() * .12f;
                ThinkTactically(self, aircraft, terrain, orders);
                if (State != before) StateTransitions++;
                if (previousTarget >= 0 && targetId >= 0 && previousTarget != targetId) TargetSwitches++;
            }
            FlightInput input = FlyToward(self, aimPoint, desiredThrottle);
            // Fine gun tracking remains a bounded stick/rudder correction. The slow tactical
            // decision is retained, but aiming cannot use a quarter-second-old world point.
            if (State == "Attack" || State == "Strafe")
            {
                Vector3 gunPoint = aimPoint;
                if (State == "Attack")
                    for (int i = 0; i < aircraft.Count; i++)
                        if (aircraft[i].Id == targetId && aircraft[i].Alive)
                        {
                            gunPoint = Weapons.ComputeLead(self, aircraft[i]);
                            if (Skill == PilotSkill.Ace)
                            {
                                AircraftState target = aircraft[i];
                                if (aceTrackedTarget != target.Id) { aceTrackedTarget = target.Id; acePreviousVelocity = target.Velocity; aceTargetAcceleration = Vector3.Zero; }
                                Vector3 observed = (target.Velocity - acePreviousVelocity) / Math.Max(.001f, dt);
                                acePreviousVelocity = target.Velocity;
                                if (observed.LengthSquared() > 35f * 35f) observed = Vector3.Normalize(observed) * 35f;
                                aceTargetAcceleration += (observed - aceTargetAcceleration) * Math.Min(1f, dt * 7f);
                                float travel = Math.Min(1f, Vector3.Distance(self.Position, target.Position) / Weapons.MuzzleSpeed);
                                gunPoint += aceTargetAcceleration * (.35f * travel * travel);
                            }
                        }
                if (State == "Strafe" && orders.GroundTargets != null)
                    for (int i = 0; i < orders.GroundTargets.Count; i++)
                    {
                        GroundTarget groundTarget = orders.GroundTargets[i];
                        if (groundTarget.Id != targetId || !groundTarget.Alive) continue;
                        float travel = Vector3.Distance(self.Position, groundTarget.Position) / Weapons.MuzzleSpeed;
                        gunPoint = groundTarget.Position + (groundTarget.Velocity - self.Velocity) * travel
                            + Vector3.UnitY * (4.905f * travel * travel);
                    }
                Vector3 local = Vector3.Transform(Unit(gunPoint - self.Position, self.Forward), Quaternion.Conjugate(self.Rotation));
                if (local.Z > .94f)
                {
                    gunPitchIntegral = Clamp(gunPitchIntegral + local.Y * dt * 4, -.35f, .35f);
                    input.Pitch = Clamp(local.Y * 11 + self.AngularVelocity.X * .3f + gunPitchIntegral, -1, 1);
                    float bank = (float)Math.Atan2(-self.Right.Y, self.Up.Y);
                    float trackingBank = Clamp(local.X * 5, -.65f, .65f);
                    input.Roll = Clamp((trackingBank - bank) * 2f + self.AngularVelocity.Z * .4f, -1, 1);
                    input.Yaw = Clamp(local.X * 5f, -.8f, .8f);
                }
                else gunPitchIntegral = 0;
            }
            else gunPitchIntegral = 0;
            input.Fire = fireIntent && self.Ammo > 0;
            if (input.Fire) ShotsRequested++;
            return input;
        }

        void ThinkTactically(AircraftState self, IList<AircraftState> planes, TerrainField terrain, PilotOrders orders)
        {
            fireIntent = false;
            float ground = terrain.Height(self.Position.X, self.Position.Z);
            float aheadGround = ground;
            for (int i = 1; i <= 3; i++)
                aheadGround = Math.Max(aheadGround, terrain.Height(self.Position.X + self.Velocity.X * i * 1.7f,
                    self.Position.Z + self.Velocity.Z * i * 1.7f));
            float agl = self.Position.Y - ground;
            float predictedClearance = self.Position.Y + Math.Min(0, self.Velocity.Y) * 2.7f - aheadGround;
            bool strafing = orders.CanStrafe && groundAttackRun;
            float clearance = strafing ? 105 : 220;
            if (agl < clearance || predictedClearance < (strafing ? 75 : 170))
            {
                SetEscape(self, "Terrain escape", Horizontal(self.Forward), Math.Max(self.Position.Y + 550, aheadGround + 550));
                groundAttackRun = false; groundPullout = orders.CanStrafe;
                return;
            }
            if (self.Speed < self.Definition.StallSpeed * 1.27f || self.StallFactor > .34f)
            {
                State = "Recover airspeed";
                aimPoint = self.Position + Horizontal(self.Forward) * 1450 - Vector3.UnitY * Math.Min(280, agl - 280);
                desiredThrottle = 1; return;
            }
            if (AvoidTraffic(self, planes)) return;
            if (orders.CombatRadius > 0f && HorizontalLength(self.Position - orders.ObjectiveAnchor) > orders.CombatRadius)
            {
                State = "Return to sector"; targetId = -1; pursuitTime = 0;
                aimPoint = orders.ObjectiveAnchor;
                aimPoint.Y = Math.Max(aheadGround + 450f, orders.ObjectiveAnchor.Y + 100f);
                desiredThrottle = SpeedThrottle(self, 120f); return;
            }

            AircraftState threat = FindThreat(self, planes, terrain);
            if (maneuverTimer > 0 && (State == "Break" || State == "Extend" || State == "Energy climb" || State == "Withdraw"))
            {
                aimPoint = self.Position + escapeDirection * 1800;
                aimPoint.Y = Math.Max(aheadGround + 300, aimPoint.Y);
                desiredThrottle = 1; return;
            }
            if (self.Health < 28 || self.Ammo == 0 && self.Role != AircraftRole.Bomber && self.Role != AircraftRole.Courier)
            {
                Vector3 home = orders.Leader != null && orders.Leader.Alive ? orders.Leader.Position : orders.ObjectiveAnchor;
                Vector3 direction = Horizontal(home - self.Position);
                if (threat != null) direction = Horizontal(self.Position - threat.Position + direction * 600);
                BeginManeuver(self, "Withdraw", direction + Vector3.UnitY * .05f, 7);
                return;
            }
            if (threat != null && defenseCooldown <= 0 && random.NextDouble() < (Skill == PilotSkill.Ace ? .8 : .48))
            {
                float side = Vector3.Dot(threat.Position - self.Position, self.Right) > 0 ? -1 : 1;
                Vector3 direction = Horizontal(self.Forward) + Horizontal(self.Right) * side * 1.7f;
                direction.Y = agl > 650 && self.Speed < 140 ? -.16f : .06f;
                BeginManeuver(self, "Break", direction, (Skill == PilotSkill.Ace ? 2.8f : 3.5f) + (1 - aggression) * 5);
                defenseCooldown = Skill == PilotSkill.Ace ? 6f : 10f; return;
            }
            if (orders.FollowRoute)
            {
                if (orders.Leader != null && orders.Leader.Alive) Formation(self, orders, true);
                else
                {
                    State = "Outbound"; targetId = -1;
                    aimPoint = orders.Destination;
                    aimPoint.Y = Math.Max(orders.Destination.Y, aheadGround + 430);
                    desiredThrottle = SpeedThrottle(self, self.Definition.CruiseSpeed);
                }
                // Route aircraft retain their mission but may fire defensively at a visible aircraft ahead.
                if (self.Role != AircraftRole.Bomber && self.Role != AircraftRole.Courier)
                {
                    AircraftState opportunity = SelectTacticalTarget(self, planes, terrain, orders);
                    if (opportunity != null) fireIntent = FiringSolution(self, opportunity, planes, terrain);
                }
                return;
            }
            AircraftState leader = orders.Leader != null && orders.Leader.Alive ? orders.Leader : null;
            if (leader != null)
            {
                float separation = Vector3.Distance(self.Position, leader.Position);
                if (separation > orders.LeashRadius) rejoining = true;
                if (separation < 2200) rejoining = false;
                if (orders.Order == SquadronOrder.FormUp || orders.Order == SquadronOrder.Regroup || rejoining)
                { Formation(self, orders, false); return; }
            }

            AircraftState target = SelectTacticalTarget(self, planes, terrain, orders);
            if (orders.CanStrafe && (target == null || orders.DesignatedTargetId >= 100
                || threat == null && Vector3.Distance(self.Position, target.Position) > 1100))
            {
                if (Strafe(self, orders, terrain)) return;
            }
            if (target == null)
            {
                if (leader != null) { Formation(self, orders, false); return; }
                State = contactMemory > 0 ? "Search contact" : "Patrol";
                aimPoint = contactMemory > 0 ? lastContact : orders.ObjectiveAnchor + new Vector3(
                    (float)Math.Sin(elapsed * .015f + weave) * 2100, 200, (float)Math.Cos(elapsed * .015f + weave) * 2100);
                aimPoint.Y = Math.Max(aimPoint.Y, aheadGround + 500);
                desiredThrottle = SpeedThrottle(self, 112); return;
            }
            float distance = Vector3.Distance(self.Position, target.Position);
            Vector3 directionToTarget = Unit(target.Position - self.Position, self.Forward);
            float nose = Vector3.Dot(self.Forward, directionToTarget);
            if (distance < 150 && nose > .1f || pursuitTime > 24 + aggression * 12 && nose < .35f && distance < 3000)
            {
                Vector3 direction = Horizontal(self.Forward) + Horizontal(self.Right) * (self.Id % 2 == 0 ? .5f : -.5f);
                direction.Y = self.Speed > 125 ? .15f : -.08f;
                BeginManeuver(self, "Extend", direction, 6); pursuitTime = 0; return;
            }
            float energy = .5f * (self.Speed * self.Speed - target.Speed * target.Speed) / FlightDynamics.Gravity
                + self.Position.Y - target.Position.Y;
            if (Skill == PilotSkill.Ace && energy < -180 && target.Position.Y > self.Position.Y + 200
                && self.Speed < 118 && distance < 2300 && energyCooldown <= 0)
            {
                BeginManeuver(self, "Extend", Horizontal(self.Forward) - Vector3.UnitY * .12f, 5f);
                energyCooldown = 12f; return;
            }
            if (energy > (Skill == PilotSkill.Ace ? 420 : 600) && distance < (Skill == PilotSkill.Ace ? 1250 : 800)
                && nose < (Skill == PilotSkill.Ace ? .3f : .1f) && self.Speed > 126 && energyCooldown <= 0)
            {
                BeginManeuver(self, "Energy climb", Horizontal(self.Forward) + Vector3.UnitY * .24f, 5);
                energyCooldown = Skill == PilotSkill.Ace ? 12f : 22f; return;
            }
            State = distance > 1050 ? "Intercept" : "Attack";
            Vector3 lead = Weapons.ComputeLead(self, target);
            float closing = Math.Max(65, self.Speed - Vector3.Dot(target.Velocity, directionToTarget));
            float interceptTime = Math.Min(6, distance / closing);
            aimPoint = distance > 1050 ? target.Position + target.Velocity * interceptTime : lead;
            float climbLimit = self.Speed > 125 ? .33f : .13f;
            aimPoint.Y = Clamp(aimPoint.Y, Math.Max(aheadGround + 280, self.Position.Y - distance * .30f),
                Math.Max(aheadGround + 320, self.Position.Y + distance * climbLimit));
            aimPoint += self.Right * ((float)Math.Sin(elapsed * .8f + weave)
                * (Skill == PilotSkill.Ace ? distance < 700 ? .5f : 3f : distance < 700 ? 1.5f : 7f));
            float wantedSpeed = Clamp(target.Speed + (distance - 240) * .028f, self.Definition.StallSpeed * 1.58f, 165);
            if (nose < .45f && distance < 2300) wantedSpeed = Math.Min(wantedSpeed, 108 + distance * .008f);
            desiredThrottle = SpeedThrottle(self, wantedSpeed);
            fireIntent = FiringSolution(self, target, planes, terrain);
        }

        void Formation(AircraftState self, PilotOrders orders, bool route)
        {
            AircraftState leader = orders.Leader;
            Vector3 forward = Horizontal(leader.Forward), right = new Vector3(forward.Z, 0, -forward.X);
            Vector3 slot = leader.Position + right * orders.FormationOffset.X + Vector3.UnitY * orders.FormationOffset.Y
                + forward * orders.FormationOffset.Z;
            Vector3 error = slot - self.Position;
            FormationError = error.Length();
            State = FormationError > 650 ? "Rejoin" : route ? "Bomber formation" : "Formation";
            targetId = -1;
            float along = Vector3.Dot(error, forward);
            float catchup = Clamp(along * .027f, -23, 35);
            float lateral = Math.Abs(Vector3.Dot(error, right));
            float wanted = Clamp(leader.Speed + catchup + Math.Min(8, lateral * .009f), self.Definition.StallSpeed * 1.45f, 163);
            aimPoint = slot + forward * (FormationError > 1200 ? 320 : 650);
            aimPoint.Y = slot.Y + leader.Velocity.Y * .6f;
            desiredThrottle = SpeedThrottle(self, wanted);
        }

        AircraftState FindThreat(AircraftState self, IList<AircraftState> planes, TerrainField terrain)
        {
            AircraftState result = null; float nearest = Skill == PilotSkill.Ace ? 1250 * 1250 : 1050 * 1050;
            for (int i = 0; i < planes.Count; i++)
            {
                AircraftState other = planes[i]; if (!other.Alive || other.Team == self.Team) continue;
                Vector3 toSelf = self.Position - other.Position; float d = toSelf.LengthSquared();
                if (d < 1 || d > nearest || Vector3.Dot(other.Forward, toSelf / (float)Math.Sqrt(d)) < (Skill == PilotSkill.Ace ? .9f : .93f)) continue;
                if (Vector3.Dot(self.Forward, -Unit(toSelf, self.Forward)) > .5f || !Visible(self, other, terrain, 1300)) continue;
                result = other; nearest = d;
            }
            return result;
        }

        AircraftState SelectTacticalTarget(AircraftState self, IList<AircraftState> planes, TerrainField terrain, PilotOrders orders)
        {
            AircraftState best = null, currentTarget = null;
            float bestScore = float.MaxValue, currentScore = float.MaxValue;
            for (int i = 0; i < planes.Count; i++)
            {
                AircraftState other = planes[i];
                if (!other.Alive || other.Team == self.Team || !Visible(self, other, terrain, AwarenessRange)) continue;
                float distance = Vector3.Distance(self.Position, other.Position);
                float score = distance;
                if (orders.PreferBombers && (other.Role == AircraftRole.Bomber || other.Role == AircraftRole.Courier)) score *= .32f;
                if (orders.DesignatedTargetId == other.Id) score *= .10f;
                if (orders.Order == SquadronOrder.CoverMe && orders.Leader != null)
                {
                    float leaderDistance = Vector3.Distance(other.Position, orders.Leader.Position);
                    if (leaderDistance > 2200) continue;
                    Vector3 towardLeader = Unit(orders.Leader.Position - other.Position, other.Forward);
                    score *= Vector3.Dot(other.Forward, towardLeader) > .72f ? .28f : .85f;
                }
                if (orders.ProtectedAssets != null)
                    for (int p = 0; p < orders.ProtectedAssets.Count; p++)
                    {
                        AircraftState asset = orders.ProtectedAssets[p]; if (!asset.Alive) continue;
                        Vector3 offset = asset.Position - other.Position;
                        if (offset.LengthSquared() < 1800 * 1800 && Vector3.Dot(other.Forward, Unit(offset, other.Forward)) > .5f)
                        { score *= .48f; break; }
                    }
                // Small per-pilot geometric preference distributes attacks without sharing perfect target knowledge.
                score *= 1 + .07f * (float)Math.Sin(other.Id * 2.3f + weave);
                if (other.Id == targetId) { currentTarget = other; currentScore = score; }
                if (score < bestScore) { bestScore = score; best = other; }
            }
            if (currentTarget != null && (best == null || best.Id != orders.DesignatedTargetId)
                && (targetCommit > 0 || bestScore > currentScore * .62f)) best = currentTarget;
            if (best != null)
            {
                if (targetId != best.Id) { targetCommit = 5 + aggression * 4; pursuitTime = 0; }
                targetId = best.Id; contactMemory = 5; lastContact = best.Position + best.Velocity * 2;
            }
            else targetId = -1;
            return best;
        }

        bool Visible(AircraftState self, AircraftState other, TerrainField terrain, float range)
        {
            Vector3 delta = other.Position - self.Position; float d2 = delta.LengthSquared();
            if (d2 > range * range || d2 < .001f) return false;
            if (d2 > 1600 * 1600 && Vector3.Dot(self.Forward, delta / (float)Math.Sqrt(d2)) < -.68f) return false;
            for (int i = 1; i <= 3; i++)
            {
                Vector3 p = Vector3.Lerp(self.Position, other.Position, i * .25f);
                if (p.Y < terrain.Height(p.X, p.Z) + 20) return false;
            }
            return true;
        }

        bool FiringSolution(AircraftState self, AircraftState target, IList<AircraftState> planes, TerrainField terrain)
        {
            float distance = Vector3.Distance(self.Position, target.Position);
            if (distance > (Skill == PilotSkill.Ace ? 850 : 1000) || distance < 65 || !Visible(self, target, terrain, 1050)) return false;
            Vector3 aim = Unit(Weapons.ComputeLead(self, target) - self.Position, self.Forward);
            if (Skill == PilotSkill.Ace)
            {
                if (Vector3.Dot(self.Forward, aim) < .9988f || (elapsed + weave) % 2.05f > 1.25f) return false;
            }
            else if (Vector3.Dot(self.Forward, aim) < .9982f || (elapsed + weave) % 2.6f > 1.15f + aggression * .22f) return false;
            for (int i = 0; i < planes.Count; i++)
            {
                AircraftState ally = planes[i]; if (!ally.Alive || ally == self || ally.Team != self.Team) continue;
                Vector3 offset = ally.Position - self.Position;
                float along = Vector3.Dot(offset, aim);
                if (along > 0 && along < distance && (offset - aim * along).LengthSquared() < 25 * 25) return false;
            }
            return true;
        }

        bool AvoidTraffic(AircraftState self, IList<AircraftState> planes)
        {
            for (int i = 0; i < planes.Count; i++)
            {
                AircraftState other = planes[i]; if (other == self || !other.Alive) continue;
                Vector3 relative = other.Position - self.Position, velocity = other.Velocity - self.Velocity;
                if (relative.LengthSquared() > 600 * 600) continue;
                float t = Clamp(-Vector3.Dot(relative, velocity) / Math.Max(1, velocity.LengthSquared()), 0, 3);
                float margin = other.Role == AircraftRole.Bomber || self.Role == AircraftRole.Bomber ? 48 : 32;
                if ((relative + velocity * t).LengthSquared() > margin * margin) continue;
                // Head-on traffic turns to each pilot's own right, hence opposite world sides.
                float side = Vector3.Dot(self.Forward, other.Forward) < -.5f ? 1 : self.Id < other.Id ? -1 : 1;
                Vector3 away = Horizontal(self.Right) * side;
                State = "Avoid traffic";
                aimPoint = self.Position + Horizontal(self.Forward) * 1000 + away * 1000 + Vector3.UnitY * (self.Id % 2 == 0 ? 120 : -80);
                desiredThrottle = .75f; return true;
            }
            return false;
        }

        bool Strafe(AircraftState self, PilotOrders orders, TerrainField terrain)
        {
            GroundTarget target = null; float best = float.MaxValue;
            if (orders.GroundTargets == null) return false;
            for (int i = 0; i < orders.GroundTargets.Count; i++)
            {
                GroundTarget candidate = orders.GroundTargets[i]; if (!candidate.Alive || candidate.Team == self.Team) continue;
                float score = Vector3.DistanceSquared(self.Position, candidate.Position);
                if (candidate.Id == orders.DesignatedTargetId) score *= .05f;
                if (candidate.Id == groundTargetId) score *= .45f;
                if (score < best) { best = score; target = candidate; }
            }
            if (target == null) return false;
            if (target.Id != groundTargetId)
            {
                bool finishingPass = groundAttackRun || groundPullout;
                groundTargetId = target.Id; runDirection = Horizontal(target.Position - self.Position);
                groundAttackRun = false; groundPullout = finishingPass; groundAlign = false;
            }
            targetId = target.Id;
            float horizontal = HorizontalLength(target.Position - self.Position);
            float agl = self.Position.Y - terrain.Height(self.Position.X, self.Position.Z);
            if (groundPullout)
            {
                State = "Pull up"; desiredThrottle = 1;
                aimPoint = self.Position + Horizontal(self.Forward) * 1700 + Vector3.UnitY * 550;
                if (agl > 550 && horizontal > 2400 && self.Velocity.Y > 0)
                { groundPullout = false; runDirection = Horizontal(target.Position - self.Position); }
                return true;
            }
            float targetNose = Vector3.Dot(self.Forward, Unit(target.Position - self.Position, self.Forward));
            if (groundAttackRun && (horizontal < 340 || Vector3.Dot(target.Position - self.Position, runDirection) < 0 || agl < 155 || targetNose < .55f))
            {
                groundAttackRun = false; groundPullout = true;
                State = "Pull up"; desiredThrottle = 1;
                aimPoint = self.Position + Horizontal(self.Forward) * 1800 + Vector3.UnitY * 600;
                return true;
            }
            if (!groundAttackRun)
            {
                State = "Line up strafe";
                Vector3 entry = target.Position - runDirection * 1800 + Vector3.UnitY * 360;
                if(Vector3.Distance(self.Position,entry)<650)groundAlign=true;
                aimPoint = groundAlign ? target.Position + runDirection * 500 + Vector3.UnitY * 360 : entry;
                desiredThrottle = SpeedThrottle(self, 116);
                // Reaching a rendezvous point is not the same as lining up a gun run.
                // Keep full turning authority until the nose actually faces the target.
                float headingAlignment=Vector3.Dot(Horizontal(self.Forward),Horizontal(target.Position-self.Position));
                if (horizontal > 900 && horizontal < 2500 && headingAlignment > .965f && agl < 750)
                { groundAttackRun = true; groundAlign=false; runDirection=Horizontal(target.Position-self.Position); }
                else if(groundAlign&&horizontal<800)
                {groundAlign=false;runDirection=Horizontal(target.Position-self.Position);}
                return true;
            }
            State = "Strafe";
            float time = Vector3.Distance(self.Position, target.Position) / Weapons.MuzzleSpeed;
            aimPoint = target.Position + (target.Velocity - self.Velocity) * time + Vector3.UnitY * (4.905f * time * time);
            desiredThrottle = SpeedThrottle(self, 113);
            fireIntent = horizontal < 1100 && horizontal > 340 && Vector3.Dot(self.Forward, Unit(aimPoint - self.Position, self.Forward)) > .9985f;
            return true;
        }

        float SpeedThrottle(AircraftState self, float speed)
        {
            AircraftDefinition d = self.Definition;
            float density = 1.225f * (float)Math.Exp(-Math.Max(0, self.Position.Y) / 10000);
            float qs = .5f * density * speed * speed * d.WingArea;
            float cl = d.Mass * 9.81f / Math.Max(qs, 1);
            float trim = qs * (d.BaseDrag + d.InducedDrag * cl * cl) * speed / d.MaxPower;
            return Clamp(trim + (speed - self.Speed) * .026f, .03f, 1);
        }
        void BeginManeuver(AircraftState self, string state, Vector3 direction, float duration)
        {
            State = state; escapeDirection = Unit(direction, self.Forward); maneuverTimer = duration;
            aimPoint = self.Position + escapeDirection * 1800; desiredThrottle = 1;
        }
        void SetEscape(AircraftState self, string state, Vector3 horizontal, float altitude)
        { State = state; aimPoint = self.Position + horizontal * 1500; aimPoint.Y = altitude; desiredThrottle = 1; }
        static Vector3 Unit(Vector3 value, Vector3 fallback)
        { return value.LengthSquared() > .000001f ? Vector3.Normalize(value) : fallback; }
        static float HorizontalLength(Vector3 value) => (float)Math.Sqrt(value.X * value.X + value.Z * value.Z);
    }
}
