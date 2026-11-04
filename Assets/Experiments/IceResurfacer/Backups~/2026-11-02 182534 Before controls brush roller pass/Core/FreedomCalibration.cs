using System;

namespace IceResurfacer.Core
{
    public enum SimulationMode { FreeSimulation, GuidedTraining }
    public sealed partial class SimulatorConfig
    {
        // Restraint appropriate to studded traction tires, not a drift-car model.
        public float IceLateralGrip = 1.35f;
        public float TireSlipRecovery = 1.5f;
        public float MaximumLateralSlip = .22f;
        public float RaisedTankGripPenalty = .22f;
        public float DumpExitSpeed = .65f;
        void ValidateFreedomCalibration()
        {
            float[] values={IceLateralGrip,TireSlipRecovery,MaximumLateralSlip,RaisedTankGripPenalty,DumpExitSpeed};
            foreach(float value in values)if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentException("Freedom/traction calibration must be finite.");
            if(IceLateralGrip<=0||TireSlipRecovery<=0||MaximumLateralSlip<0||MaximumLateralSlip>1||RaisedTankGripPenalty<0||RaisedTankGripPenalty>.8f||DumpExitSpeed<0||DumpExitSpeed>3)
                throw new ArgumentException("Invalid freedom/traction calibration.");
        }
    }
}
