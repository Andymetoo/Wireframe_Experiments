using UnityEngine;

namespace IceResurfacer.Presentation
{
    public static class IceOperatorPreferences
    {
        public const string SpeedKey="AuroraR54.Operator.MaxForwardSpeedMph.v1";
        public const float MetersPerSecondPerMph=.44704f;
        public static float SnapMph(float value)=>Mathf.Round(Mathf.Clamp(value,3,8)*4)/4;
        public static float LoadMph(float original)
        {
            if(!PlayerPrefs.HasKey(SpeedKey))return original;
            float value=PlayerPrefs.GetFloat(SpeedKey,original);
            return float.IsNaN(value)||float.IsInfinity(value)?original:Mathf.Clamp(value,3,8);
        }
        public static void SaveMph(float value){PlayerPrefs.SetFloat(SpeedKey,value);PlayerPrefs.Save();}
        public static void ResetSpeed(){PlayerPrefs.DeleteKey(SpeedKey);PlayerPrefs.Save();}
    }
}
