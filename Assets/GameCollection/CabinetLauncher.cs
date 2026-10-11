using UnityEngine;
using UnityEngine.InputSystem;

namespace VectorCabinet
{
    public sealed class CabinetLauncher : MonoBehaviour
    {
        GUIStyle body,title,small,button;
        readonly Color text=new Color(.82f,.88f,.85f), amber=new Color(.95f,.69f,.32f);
        void Awake()
        {
            Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Application.targetFrameRate=60;
            var cameraObject=new GameObject("Cabinet camera");cameraObject.transform.SetParent(transform);
            var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.018f,.027f,.037f);camera.cullingMask=0;
        }
        void Update()
        {
            var k=Keyboard.current;if(k==null)return;
            if(k.digit1Key.wasPressedThisFrame)GameCollection.Open(GameCollection.HouseScene);
            else if(k.digit2Key.wasPressedThisFrame)GameCollection.Open(GameCollection.AirScene);
            else if(k.escapeKey.wasPressedThisFrame && !Application.isEditor)Application.Quit();
        }
        void Rect(float x,float y,float w,float h,Color c)
        {var previous=GUI.color;GUI.color=c;GUI.DrawTexture(new Rect(x,y,w,h),Texture2D.whiteTexture);GUI.color=previous;}
        void Line(Vector2 a,Vector2 b,Color c,float width=2)
        {
            Matrix4x4 matrix=GUI.matrix;
            GUI.matrix=matrix*Matrix4x4.TRS(a,Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            Rect(0,-width*.5f,Vector2.Distance(a,b),width,c);GUI.matrix=matrix;
        }
        void Label(float x,float y,float w,float h,string value,GUIStyle style=null)
        {GUI.Label(new Rect(x,y,w,h),value,style??body);}
        bool Button(float x,float y,float w,string value)
        {
            var r=new Rect(x,y,w,48);bool hover=r.Contains(Event.current.mousePosition);
            Rect(x,y,w,48,hover?new Color(.17f,.26f,.28f):new Color(.09f,.15f,.18f));
            Rect(x,y,3,48,amber);return GUI.Button(r,value,button);
        }
        void OnGUI()
        {
            if(body==null)
            {
                body=new GUIStyle(GUI.skin.label){fontSize=19,wordWrap=true,normal={textColor=text}};
                title=new GUIStyle(body){fontSize=44,fontStyle=FontStyle.Bold};
                small=new GUIStyle(body){fontSize=14,normal={textColor=new Color(.47f,.65f,.66f)}};
                button=new GUIStyle(body){fontSize=18,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(16,8,3,3)};
            }
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-800*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            Label(66,47,1050,28,"IMPOSSIBLE STRUCTURES  /  TWO GAMES, ONE CABINET",small);
            Label(61,91,1110,75,"THE VECTOR CABINET",title);
            Label(66,175,1110,50,"Choose a world. Each game begins fresh and has its own controls.");
            Rect(65,240,552,424,new Color(.033f,.068f,.080f));Rect(641,240,574,424,new Color(.067f,.078f,.079f));
            Color teal=new Color(.30f,.63f,.63f);
            Line(new Vector2(104,357),new Vector2(104,304),teal);Line(new Vector2(104,304),new Vector2(146,274),teal);
            Line(new Vector2(146,274),new Vector2(188,304),teal);Line(new Vector2(188,304),new Vector2(188,357),teal);
            Line(new Vector2(104,357),new Vector2(188,357),teal);Line(new Vector2(137,357),new Vector2(137,324),teal);Line(new Vector2(155,357),new Vector2(155,324),teal);
            Vector2 p=new Vector2(736,316);
            Line(p+new Vector2(0,-43),p+new Vector2(-8,40),amber);Line(p+new Vector2(0,-43),p+new Vector2(8,40),amber);
            Line(p+new Vector2(-65,4),p+new Vector2(65,4),amber);Line(p+new Vector2(-65,4),p+new Vector2(-8,20),amber);Line(p+new Vector2(65,4),p+new Vector2(8,20),amber);
            Line(p+new Vector2(-25,34),p+new Vector2(25,34),amber);
            Label(98,390,490,48,"HAUNTED HOUSE",new GUIStyle(title){fontSize=31});
            Label(98,450,478,91,"The House That Listens\nInvestigate ten rooms. Learn the dead's rules. Finish what they could not.");
            Label(98,555,475,25,"GRID EXPLORATION  /  DEDUCTION  /  SURVIVAL",small);
            if(Button(98,593,484,"ENTER THE HOUSE                                      [1]"))GameCollection.Open(GameCollection.HouseScene);
            Label(674,390,505,48,"WWII AIR COMBAT",new GUIStyle(title){fontSize=31});
            Label(674,450,505,91,"Meridian 1944\nTrade altitude for speed. Lead your shots. Bring your courier home.");
            Label(674,555,500,25,"FLIGHT  /  DOGFIGHTING  /  COURIER COVER",small);
            if(Button(674,593,507,"FLY A SORTIE                                                [2]"))GameCollection.Open(GameCollection.AirScene);
            Label(66,706,935,58,"Both worlds are silent. All scenery and aircraft are built from code.\nUse each game's pause menu to return here.",small);
            if(!Application.isEditor && Button(1030,705,185,"EXIT  [ESC]"))Application.Quit();
            GUI.matrix=Matrix4x4.identity;
        }
    }
}
