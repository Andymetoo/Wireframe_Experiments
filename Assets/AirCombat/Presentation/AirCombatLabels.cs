using UnityEngine;

namespace Meridian
{
    public sealed partial class AirCombatGame
    {
        readonly GUIContent worldLabelContent = new GUIContent();
        GUIStyle worldLabelShadow, worldLabelFace;

        // Thin four-sided outlines use the existing font and virtual 1280x800 scale.
        // Visibility and projection stay with callers; no scene sampling or generated artwork.
        void WorldLabel(float x,float y,float w,float h,string text,GUIStyle style=null,Color? color=null)
        {
            GUIStyle face=style??small;
            if(worldLabelShadow==null)
            {worldLabelShadow=new GUIStyle(small);worldLabelFace=new GUIStyle(small);}
            worldLabelShadow.fontSize=face.fontSize;
            worldLabelShadow.fontStyle=face.fontStyle;
            worldLabelShadow.normal.textColor=Color.black;
            worldLabelFace.fontSize=face.fontSize;
            worldLabelFace.fontStyle=face.fontStyle;
            // Set the text color directly: tinting the existing colored font multiplies
            // its channels and makes objective blue and friendly green needlessly dark.
            worldLabelFace.normal.textColor=Color.Lerp(color??ink,Color.white,.24f);
            worldLabelContent.text=text;
            Color previous=GUI.color;GUI.color=Color.white;
            GUI.Label(new Rect(x-1,y,w,h),worldLabelContent,worldLabelShadow);
            GUI.Label(new Rect(x+1,y,w,h),worldLabelContent,worldLabelShadow);
            GUI.Label(new Rect(x,y-1,w,h),worldLabelContent,worldLabelShadow);
            GUI.Label(new Rect(x,y+1,w,h),worldLabelContent,worldLabelShadow);
            GUI.Label(new Rect(x,y,w,h),worldLabelContent,worldLabelFace);
            GUI.color=previous;
        }
    }
}
