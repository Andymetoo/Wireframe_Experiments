using System;
using System.Collections.Generic;
using System.Reflection;

namespace IceResurfacer.Core
{
    [Serializable]
    public struct RecordedRouteSample
    {
        public int sequence;
        public double seconds, distanceMeters;
        public float vehicleX, vehicleZ, vehicleHeadingRadians, speedMetersPerSecond;
        public int direction; // -1 reverse, 0 stopped, +1 forward (speed deadband .02 m/s).
        public float conditionerX, conditionerZ, conditionerHeadingRadians, conditionerLift;
        public float bladeDepthMm, floodValve, washValve, tankLift;
        public bool horizontalAuger, verticalAuger, washPump, boardBrush;
    }

    /// <summary>Versioned raw observations in the existing route's rear-axle coordinate frame.</summary>
    [Serializable]
    public sealed class RecordedRouteData
    {
        public const int CurrentVersion=1, MaximumSamples=50000;
        public const string Coordinates="meters; X right, Z forward; heading radians from +Z toward +X; vehicle rear axle; actual conditioner frame";
        public int schemaVersion;
        public string id, name, createdUtc, provenance, coordinates;
        public SimulatorConfig configuration;
        public float startSimulationSeconds;
        public double durationSeconds, distanceMeters;
        public float distanceThresholdMeters, headingThresholdDegrees;
        public bool complete;
        public string stopReason;
        public List<RecordedRouteSample> samples=new List<RecordedRouteSample>();
        // An unchanged imported route can be copied verbatim, including its raw
        // decimal spellings and any future optional fields. Not part of the schema.
        [NonSerialized] internal string sourceJson=null,parsedJsonFingerprint=null;

        public void Validate()
        {
            if(schemaVersion!=CurrentVersion||coordinates!=Coordinates)Invalid("Unsupported route schema or coordinate convention.");
            if(!Guid.TryParseExact(id,"N",out _)||string.IsNullOrWhiteSpace(name)||name.Length>80||
                string.IsNullOrWhiteSpace(provenance)||provenance.Length>500||
                !DateTimeOffset.TryParse(createdUtc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out _))
                Invalid("Missing or invalid route identity, name, provenance or UTC creation time.");
            if(configuration==null)Invalid("Missing configuration snapshot.");
            foreach(var field in typeof(SimulatorConfig).GetFields(BindingFlags.Public|BindingFlags.Instance))
                if(field.FieldType==typeof(float)&&!Finite((float)field.GetValue(configuration)))Invalid("Non-finite configuration.");
            configuration.Validate();
            if(!Finite(durationSeconds)||durationSeconds<0||!Finite(distanceMeters)||distanceMeters<0||!Finite(startSimulationSeconds)||
                !Finite(distanceThresholdMeters)||distanceThresholdMeters<=0||distanceThresholdMeters>1||
                !Finite(headingThresholdDegrees)||headingThresholdDegrees<=0||headingThresholdDegrees>10)
                Invalid("Invalid recording timing, distance or sampling policy.");
            if(samples==null||samples.Count==0||samples.Count>MaximumSamples)Invalid("Missing samples or route exceeds the sample budget.");
            for(int i=0;i<samples.Count;i++)
            {
                var p=samples[i];
                if(p.sequence!=i||!Finite(p.seconds)||!Finite(p.distanceMeters)||p.seconds<0||p.distanceMeters<0||
                    p.seconds>durationSeconds+.00001||p.distanceMeters>distanceMeters+.00001||p.direction< -1||p.direction>1)
                    Invalid("Invalid sequence, timestamp or distance at sample "+i+".");
                if(!Finite(p.vehicleX)||!Finite(p.vehicleZ)||!Finite(p.vehicleHeadingRadians)||!Finite(p.speedMetersPerSecond)||
                    !Finite(p.conditionerX)||!Finite(p.conditionerZ)||!Finite(p.conditionerHeadingRadians)||
                    Math.Abs(p.vehicleX)>10000||Math.Abs(p.vehicleZ)>10000||Math.Abs(p.conditionerX)>10000||Math.Abs(p.conditionerZ)>10000||
                    !Unit(p.conditionerLift)||!Unit(p.floodValve)||!Unit(p.washValve)||!Unit(p.tankLift)||!Finite(p.bladeDepthMm)||p.bladeDepthMm<0)
                    Invalid("Invalid physical observation at sample "+i+".");
                int direction=p.speedMetersPerSecond>.02f?1:p.speedMetersPerSecond<-.02f?-1:0;
                if(p.direction!=direction)Invalid("Travel direction disagrees with actual speed at sample "+i+".");
                if(i==0){if(p.seconds!=0||p.distanceMeters!=0)Invalid("First sample must start at zero time and distance.");continue;}
                var before=samples[i-1];
                double dx=p.vehicleX-before.vehicleX,dz=p.vehicleZ-before.vehicleZ;
                if(p.seconds<=before.seconds||p.distanceMeters<before.distanceMeters||
                    p.distanceMeters-before.distanceMeters+.0001<Math.Sqrt(dx*dx+dz*dz))Invalid("Out-of-order route or inconsistent distance at sample "+i+".");
                if(SamePoseAndState(p,before))Invalid("Redundant stationary sample at "+i+".");
            }
            if(complete&&Math.Abs(samples[samples.Count-1].distanceMeters-distanceMeters)>.0001)Invalid("Completed route is missing its final observation.");
        }
        internal static bool Finite(double value) => !double.IsNaN(value)&&!double.IsInfinity(value);
        static bool Unit(float value) => Finite(value)&&value>=0&&value<=1;
        static void Invalid(string message) {throw new FormatException(message);}
        internal static bool SamePoseAndState(RecordedRouteSample a,RecordedRouteSample b)
        {
            return a.distanceMeters==b.distanceMeters&&a.vehicleX==b.vehicleX&&a.vehicleZ==b.vehicleZ&&a.vehicleHeadingRadians==b.vehicleHeadingRadians&&
                a.conditionerX==b.conditionerX&&a.conditionerZ==b.conditionerZ&&a.conditionerHeadingRadians==b.conditionerHeadingRadians&&
                !StateChanged(a,b)&&a.conditionerLift==b.conditionerLift&&a.tankLift==b.tankLift&&
                a.bladeDepthMm==b.bladeDepthMm&&a.floodValve==b.floodValve&&a.washValve==b.washValve;
        }
        internal static bool StateChanged(RecordedRouteSample a,RecordedRouteSample b)
        {
            return a.direction!=b.direction||a.horizontalAuger!=b.horizontalAuger||a.verticalAuger!=b.verticalAuger||
                a.washPump!=b.washPump||a.boardBrush!=b.boardBrush||Math.Abs(a.conditionerLift-b.conditionerLift)>=.05f||
                Math.Abs(a.tankLift-b.tankLift)>=.05f||Math.Abs(a.bladeDepthMm-b.bladeDepthMm)>=.025f||
                Math.Abs(a.floodValve-b.floodValve)>=.02f||Math.Abs(a.washValve-b.washValve)>=.02f;
        }
    }

    /// <summary>Observes fixed simulation steps. Never writes vehicle, conditioner, controls or ice.</summary>
    public sealed class RouteRecorder
    {
        public RecordedRouteData Data {get;private set;}
        public bool Recording {get;private set;}
        public int Revision {get;private set;}
        Simulation session;
        RecordedRouteSample latest;
        float lastSimulationTime;

        public bool Start(Simulation sim,string name,string provenance="Manual operator driving")
        {
            if(Recording)return false;
            if(sim==null||sim.GuidedTraining||sim.Finished)throw new InvalidOperationException("Record in an unfinished Free Simulation / Practice session.");
            if(string.IsNullOrWhiteSpace(name)||name.Trim().Length>80)throw new ArgumentException("Give the route a name of 1 to 80 characters.");
            var snapshot=new SimulatorConfig();
            foreach(var field in typeof(SimulatorConfig).GetFields(BindingFlags.Public|BindingFlags.Instance))field.SetValue(snapshot,field.GetValue(sim.Config));
            Data=new RecordedRouteData{schemaVersion=RecordedRouteData.CurrentVersion,id=Guid.NewGuid().ToString("N"),
                name=name.Trim(),createdUtc=DateTimeOffset.UtcNow.ToString("O"),provenance=provenance,coordinates=RecordedRouteData.Coordinates,
                configuration=snapshot,startSimulationSeconds=sim.Time,distanceThresholdMeters=.20f,headingThresholdDegrees=2,
                stopReason="Recording in progress"};
            session=sim;lastSimulationTime=sim.Time;Recording=true;latest=Capture(sim);Append();return true;
        }
        public bool Observe(Simulation sim,double stepSeconds=.02)
        {
            if(!Recording)return false;
            if(sim!=session){Stop("Session changed; original trajectory retained");return false;}
            if(sim.Time==lastSimulationTime)return false; // Duplicate observer invocation is harmless.
            if(!RecordedRouteData.Finite(stepSeconds)||stepSeconds<=0||stepSeconds>.12)throw new ArgumentOutOfRangeException(nameof(stepSeconds));
            if(sim.Time<lastSimulationTime){Stop("Simulation clock reset");return false;}
            var next=Capture(sim);double dx=next.vehicleX-latest.vehicleX,dz=next.vehicleZ-latest.vehicleZ;
            Data.distanceMeters+=Math.Sqrt(dx*dx+dz*dz);Data.durationSeconds+=stepSeconds;
            next.seconds=Data.durationSeconds;next.distanceMeters=Data.distanceMeters;latest=next;lastSimulationTime=sim.Time;
            var previous=Data.samples[Data.samples.Count-1];
            double cx=next.conditionerX-previous.conditionerX,cz=next.conditionerZ-previous.conditionerZ;
            bool sample=next.distanceMeters-previous.distanceMeters>=Data.distanceThresholdMeters||
                cx*cx+cz*cz>=Data.distanceThresholdMeters*Data.distanceThresholdMeters||
                Angle(next.vehicleHeadingRadians,previous.vehicleHeadingRadians)>=Data.headingThresholdDegrees*Math.PI/180||
                Angle(next.conditionerHeadingRadians,previous.conditionerHeadingRadians)>=Data.headingThresholdDegrees*Math.PI/180||
                RecordedRouteData.StateChanged(next,previous);
            if(sample)Append();
            // Reserve the last slot for the final sub-threshold endpoint.
            if(Data.samples.Count>=RecordedRouteData.MaximumSamples-1)Stop("Sample budget reached; recording safely stopped");
            return sample;
        }
        public bool Stop(string reason="Stopped by operator")
        {
            if(!Recording)return false;
            var previous=Data.samples[Data.samples.Count-1];
            if(latest.seconds>previous.seconds&&!RecordedRouteData.SamePoseAndState(latest,previous))Append();
            Data.complete=true;Data.stopReason=reason;Recording=false;session=null;Revision++;return true;
        }
        public void Load(RecordedRouteData data)
        {
            if(Recording)throw new InvalidOperationException("Stop recording before loading another route.");
            data.Validate();Data=data;Revision++;
        }
        void Append() {latest.sequence=Data.samples.Count;Data.samples.Add(latest);Revision++;}
        static double Angle(float a,float b) {double d=a-b;return Math.Abs(Math.Atan2(Math.Sin(d),Math.Cos(d)));}
        static RecordedRouteSample Capture(Simulation s)
        {
            var c=s.Controls;var v=s.Vehicle;var p=s.Conditioner;
            return new RecordedRouteSample{vehicleX=v.X,vehicleZ=v.Z,vehicleHeadingRadians=v.Heading,speedMetersPerSecond=v.Speed,
                direction=v.Speed>.02f?1:v.Speed<-.02f?-1:0,conditionerX=p.X,conditionerZ=p.Z,conditionerHeadingRadians=p.Heading,
                conditionerLift=p.Lift,bladeDepthMm=s.ActualBladeDepthMm,floodValve=c.FloodFlow,washValve=c.WashValve,tankLift=s.TankLift,
                horizontalAuger=c.HorizontalAuger,verticalAuger=c.VerticalAuger,washPump=c.WashPump,boardBrush=c.BoardBrush};
        }
    }
}
