using UnityEngine;

namespace IceResurfacer.Presentation
{
    /// <summary>Shared paint, lettering and hardware for the operator interface.</summary>
    public static class IceUITheme
    {
        public static readonly Color PanelColor=new Color32(48,53,47,255);
        public static readonly Color Inset=new Color32(32,36,32,255);
        public static readonly Color ButtonFace=new Color32(80,86,74,255);
        public static readonly Color Text=new Color32(235,231,213,255);
        public static readonly Color Muted=new Color32(180,185,169,255);
        public static readonly Color Accent=new Color32(173,190,133,255);
        public static readonly Color Warning=new Color32(225,180,100,255);
        public static readonly Color Danger=new Color32(224,142,120,255);
        public static readonly Color Paper=new Color32(221,216,195,255);
        public static readonly Color Ink=new Color32(43,48,41,255);
        public static readonly Color PaperMuted=new Color32(91,96,81,255);
        public static readonly Color Edge=new Color32(119,127,108,255);
        public static readonly Color ActiveFace=new Color32(69,83,56,255);
        public static readonly Color AlertFace=new Color32(92,58,45,255);
        static Texture2D raised,hover,pressed,selected,focused,field,thumb;
        static GUIStyle slider,sliderThumb;

        public static GUIStyle LabelStyle(int fontSize=18,bool bold=false)
        {
            var style=new GUIStyle(GUI.skin.label){fontSize=fontSize,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,
                wordWrap=true,richText=false,padding=new RectOffset(0,0,0,0)};
            style.normal.textColor=Lettering(Text);return style;
        }
        public static GUIStyle ButtonStyle(int fontSize=17,bool wrap=true,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            EnsureTextures();
            var style=new GUIStyle(GUI.skin.button){fontSize=fontSize,fontStyle=FontStyle.Normal,wordWrap=wrap,
                alignment=alignment,richText=false,border=new RectOffset(2,2,2,2),padding=new RectOffset(10,10,4,4)};
            State(style.normal,raised,Text);State(style.hover,hover,Text);State(style.active,pressed,Text);
            State(style.focused,focused,Text);State(style.onNormal,selected,Text);State(style.onHover,selected,Text);
            State(style.onActive,pressed,Text);State(style.onFocused,focused,Text);
            return style;
        }
        public static GUIStyle FieldStyle(int fontSize=18)
        {
            EnsureTextures();var style=new GUIStyle(GUI.skin.textField){fontSize=fontSize,
                border=new RectOffset(2,2,2,2),padding=new RectOffset(9,9,5,5),richText=false};
            State(style.normal,field,Text);State(style.hover,field,Text);State(style.active,focused,Text);
            State(style.focused,focused,Text);return style;
        }
        static void State(GUIStyleState state,Texture2D background,Color ink)
        {state.background=background;state.textColor=Lettering(ink);}

        // IMGUI converts GUIStyle text colors to linear values in a linear
        // project, while the flat panel textures retain their display colors.
        // Keep lettering at the intended palette value instead of darkening it.
        public static Color Lettering(Color color)
        {return QualitySettings.activeColorSpace==ColorSpace.Linear?color.gamma:color;}

        public static bool DrawButton(Rect rect,string caption,GUIStyle style,bool selected=false)
        {
            if(!selected)return GUI.Button(rect,caption,style);
            var normal=style.normal.background;var over=style.hover.background;
            style.normal.background=style.onNormal.background;style.hover.background=style.onHover.background;
            bool clicked=GUI.Button(rect,caption,style);
            style.normal.background=normal;style.hover.background=over;
            Fill(new Rect(rect.x+5,rect.y+6,3,Mathf.Max(0,rect.height-12)),Accent);
            return clicked;
        }
        public static float HorizontalSlider(Rect rect,float value,float minimum,float maximum)
        {
            EnsureTextures();
            if(slider==null)
            {
                // Native skin offsets assume a 12 px grip. Explicit geometry
                // keeps our grip centered for every existing control height.
                slider=new GUIStyle(){padding=new RectOffset(0,0,0,0)};
                sliderThumb=new GUIStyle(){fixedWidth=15,fixedHeight=20,border=new RectOffset(2,2,2,2)};
                State(sliderThumb.normal,thumb,Text);State(sliderThumb.hover,hover,Text);State(sliderThumb.active,pressed,Text);State(sliderThumb.focused,focused,Text);
            }
            slider.padding.top=slider.padding.bottom=Mathf.RoundToInt((rect.height-sliderThumb.fixedHeight)*.5f);
            Fill(new Rect(rect.x,rect.center.y-2,rect.width,4),Inset);
            Fill(new Rect(rect.x,rect.center.y+2,rect.width,1),Edge);
            return GUI.HorizontalSlider(rect,value,minimum,maximum,slider,sliderThumb);
        }
        public static void Panel(Rect rect)
        {
            Fill(rect,Inset);Fill(new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2),PanelColor);
            Fill(new Rect(rect.x+1,rect.y+1,rect.width-2,1),Edge);
            Fill(new Rect(rect.x+1,rect.y+2,1,rect.height-3),Color.Lerp(PanelColor,Edge,.5f));
        }
        public static void PaperPanel(Rect rect)
        {
            Fill(rect,Ink);Fill(new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2),Paper);
            Fill(new Rect(rect.x+5,rect.y+5,rect.width-10,1),Color.Lerp(Paper,Color.white,.45f));
        }
        public static void Rule(Rect rect){Fill(rect,Edge);}
        public static void Fill(Rect rect,Color color)
        {
            var previous=GUI.color;GUI.color=previous*color;
            GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;
        }
        // Small nine-slice textures provide an edge and a pressed state, without
        // lighting effects, noise overlays or per-frame texture allocation.
        static Texture2D Swatch(string name,Color face,Color top,Color bottom)
        {
            var texture=new Texture2D(5,5,TextureFormat.RGBA32,false){name="Aurora UI / "+name,
                hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[25];
            for(int y=0;y<5;y++)for(int x=0;x<5;x++)
                pixels[y*5+x]=(x==0||y==4)?top:(x==4||y==0)?bottom:face;
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }
        static void EnsureTextures()
        {
            if(raised!=null)return;
            raised=Swatch("painted button",ButtonFace,Edge,Inset);
            hover=Swatch("button hover",Color.Lerp(ButtonFace,Edge,.28f),Text,Inset);
            pressed=Swatch("button pressed",Inset,Color.black,Edge);
            selected=Swatch("engaged switch",ActiveFace,Accent,Inset);
            focused=Swatch("keyboard focus",ButtonFace,Accent,Accent);
            field=Swatch("recessed field",Inset,Color.black,Edge);
            thumb=Swatch("slider handle",Muted,Text,Inset);
        }
        public static void Dispose()
        {
            Destroy(ref raised);Destroy(ref hover);Destroy(ref pressed);Destroy(ref selected);Destroy(ref focused);
            Destroy(ref field);Destroy(ref thumb);slider=sliderThumb=null;
        }
        static void Destroy(ref Texture2D texture)
        {if(texture!=null)Object.Destroy(texture);texture=null;}
    }
}
