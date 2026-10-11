using System;
using System.Numerics;

namespace Meridian
{
    /// <summary>Runs without Unity. Physical relationships are checked, not implementation snapshots.</summary>
    public static class DynamicsValidation
    {
        public static void Run(Action<bool, string> check)
        {
            AircraftState level = Spawn();
            Advance(level, 30, t => new FlightInput { Throttle = .7f });
            check(level.Alive && Math.Abs(level.Position.Y - 1500) < 30 && level.Speed > 95 && level.Speed < 155,
                "Neutral trim remains airborne and approximately level for 30 seconds");
            check(Math.Abs(level.LoadFactor - 1) < .12f && level.StallFactor < .1f,
                "Steady wing lift balances gravitational weight");

            AircraftState powered = Spawn(), idle = Spawn();
            Advance(powered, 15, t => new FlightInput { Throttle = 1 });
            Advance(idle, 15, t => new FlightInput { Throttle = 0 });
            check(powered.Speed > idle.Speed + 20, "Engine power increases airspeed against drag");

            AircraftState climb = Spawn(), dive = Spawn();
            Advance(climb, 10, t => new FlightInput { Throttle = .7f, Pitch = t < 3 ? .6f : 0 });
            Advance(dive, 10, t => new FlightInput { Throttle = .7f, Pitch = t < 3 ? -.6f : 0 });
            check(climb.Position.Y > 1750 && climb.Speed < 105, "A commanded climb exchanges speed for altitude");
            check(dive.Position.Y < 1200 && dive.Speed > 135, "A commanded dive converts height into speed");

            AircraftState bank = Spawn();
            Advance(bank, 12, t => new FlightInput { Throttle = .8f, Roll = t < .7f ? .7f : 0, Pitch = .15f });
            check(bank.Alive && bank.Position.X > 250 && bank.Velocity.X > 35 && bank.Right.Y < -.2f,
                "Banked lift produces a right turn with retained forward momentum");
            AircraftState inertia = Spawn();
            FlightDynamics.Step(inertia, new FlightInput { Roll = 1, Yaw = 1, Pitch = 1, Throttle = 1 }, FlightDynamics.FixedStep, null);
            check(inertia.Velocity.Z > 109 && Math.Abs(inertia.Velocity.X) < 1 && Math.Abs(inertia.Forward.X) < .02f,
                "Control input cannot instantly rotate or redirect the airframe");

            AircraftState stalled = Spawn(35);
            FlightDynamics.Step(stalled, new FlightInput { Throttle = 1 }, FlightDynamics.FixedStep, null);
            check(stalled.StallFactor > .4f && stalled.LoadFactor < .7f,
                "Below stall speed the wing cannot support the aircraft");
            Advance(stalled, 22, t => new FlightInput { Throttle = 1, Pitch = t < 2 ? -.3f : 0 });
            check(stalled.Alive && stalled.Speed > 95 && stalled.StallFactor < .05f && stalled.Position.Y < 1450,
                "Lowering the nose and adding power recovers a low-speed stall at the expense of height");
            AircraftState highAlpha = Spawn();
            highAlpha.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -.65f);
            FlightDynamics.Step(highAlpha, new FlightInput { Throttle = .7f }, FlightDynamics.FixedStep, null);
            check(highAlpha.StallFactor > .8f && highAlpha.AngularVelocity.X > 0,
                "Excessive angle of attack stalls the wing and generates a nose-down recovery moment");

            AircraftState overspeed = Spawn(220);
            Advance(overspeed, 5, t => new FlightInput { Throttle = 1 });
            check(overspeed.Alive && overspeed.Health < 99 && overspeed.Health > 60,
                "Sustained overspeed causes progressive damage after a recovery grace interval");
            AircraftState damaged = Spawn(), healthy = Spawn();
            damaged.Health = 35;
            Advance(damaged, 12, t => new FlightInput { Throttle = 1 });
            Advance(healthy, 12, t => new FlightInput { Throttle = 1 });
            check(damaged.Speed < healthy.Speed - 8, "Combat damage reduces available engine power");

            var terrain = new TerrainField(31415);
            AircraftState crash = Spawn();
            crash.Position.Y = terrain.Height(0, 0) + 1;
            FlightDynamics.Step(crash, new FlightInput { Throttle = .7f }, FlightDynamics.FixedStep, terrain);
            check(!crash.Alive && crash.Health == 0 && crash.DestructionCause == "Terrain impact",
                "Terrain collision produces a definite fatal airframe state");
            Vector3 deadPosition = crash.Position;
            FlightDynamics.Step(crash, new FlightInput { Throttle = 1 }, 1, terrain);
            check(crash.Position == deadPosition, "Destroyed aircraft stop participating in flight integration");

            AircraftState repeatA = Spawn(), repeatB = Spawn();
            for (int frame = 0; frame < 7200; ++frame)
            {
                float t = frame / 60f;
                FlightInput control = new FlightInput
                {
                    Throttle = .72f,
                    Pitch = .15f * (float)Math.Sin(t * .3f),
                    Roll = .12f * (float)Math.Sin(t * .17f),
                    Yaw = .04f * (float)Math.Sin(t * .4f)
                };
                FlightDynamics.Step(repeatA, control, FlightDynamics.FixedStep, terrain);
                FlightDynamics.Step(repeatB, control, FlightDynamics.FixedStep, terrain);
            }
            check(repeatA.Position == repeatB.Position && repeatA.Velocity == repeatB.Velocity && repeatA.Rotation == repeatB.Rotation,
                "Identical fixed-step inputs reproduce an identical two-minute trajectory");

            AircraftDefinition[] types = { AircraftDefinition.AlliedFighter, AircraftDefinition.AxisFighter, AircraftDefinition.Courier };
            for (int seed = 0; seed < 12; ++seed)
            {
                var random = new Random(seed);
                var field = new TerrainField(seed);
                var sameField = new TerrainField(seed);
                bool validTerrain = true;
                for (int point = 0; point < 100; ++point)
                {
                    float x = (float)(random.NextDouble() * 30000 - 15000), z = (float)(random.NextDouble() * 30000 - 15000);
                    float height = field.Height(x, z);
                    validTerrain &= height >= 0 && height < 650 && height == sameField.Height(x, z);
                }
                check(validTerrain, "Terrain seed " + seed + " is deterministic and within the safe spawn envelope");
                AircraftState airframe = Spawn();
                airframe.Position.Y = 5500;
                airframe.Definition = types[seed % types.Length];
                bool finite = true;
                FlightInput input = new FlightInput { Throttle = .8f };
                for (int frame = 0; frame < 60 * 180 && airframe.Alive; ++frame)
                {
                    if (frame % 90 == 0)
                    {
                        input.Pitch = (float)(random.NextDouble() - .5) * 1.7f;
                        input.Roll = (float)(random.NextDouble() - .5) * 1.8f;
                        input.Yaw = (float)(random.NextDouble() - .5) * .7f;
                        input.Throttle = .35f + .65f * (float)random.NextDouble();
                    }
                    FlightDynamics.Step(airframe, input, FlightDynamics.FixedStep, field);
                    finite &= FlightDynamics.Finite(airframe.Position) && FlightDynamics.Finite(airframe.Velocity)
                        && FlightDynamics.Finite(airframe.Rotation) && Math.Abs(airframe.Rotation.LengthSquared() - 1) < .002f
                        && airframe.DestructionCause != "Airframe simulation exceeded its operating envelope";
                }
                check(finite, "Three-minute varied-control trajectory remains finite for airframe seed " + seed);
            }
        }

        private static AircraftState Spawn(float speed = 110)
        { return new AircraftState { Position = new Vector3(0, 1500, 0), Velocity = Vector3.UnitZ * speed }; }
        private static void Advance(AircraftState s, float duration, Func<float, FlightInput> input)
        {
            for (int frame = 0; frame < duration * 60; ++frame)
                FlightDynamics.Step(s, input(frame / 60f), FlightDynamics.FixedStep, null);
        }
    }
}
