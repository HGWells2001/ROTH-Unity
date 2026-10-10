using System.IO;
using UnityEngine;

namespace ROTHUnity.Runtime
{
    /// <summary>Development-only runtime instrumentation for map/command verification.</summary>
    public sealed class RothRuntimeHud : MonoBehaviour
    {
        public RothMapMeshBuilder MapBuilder;
        public RothCommandMonitor Commands;
        public bool Visible = true;
        private GUIStyle _style;
        private GUIStyle _crosshair;

        private void Awake()
        {
            if(MapBuilder==null) MapBuilder=GetComponent<RothMapMeshBuilder>();
            if(Commands==null) Commands=GetComponent<RothCommandMonitor>();
        }

        private void OnGUI()
        {
            if(!Visible) return;
            if(_style==null)
            {
                _style=new GUIStyle(GUI.skin.box); _style.alignment=TextAnchor.UpperLeft; _style.fontSize=13; _style.normal.textColor=Color.white;
                _crosshair=new GUIStyle(GUI.skin.label); _crosshair.alignment=TextAnchor.MiddleCenter; _crosshair.fontSize=18; _crosshair.normal.textColor=Color.white;
            }
            string map=MapBuilder!=null&&!string.IsNullOrEmpty(MapBuilder.RawMapPath)?Path.GetFileNameWithoutExtension(MapBuilder.RawMapPath):"?";
            string sector=Commands!=null?Commands.CurrentSectorId.ToString():"?";
            string trigger=Commands!=null&&!string.IsNullOrEmpty(Commands.LastTrigger)?Commands.LastTrigger:"-";
            string command=Commands!=null&&!string.IsNullOrEmpty(Commands.LastCommand)?Commands.LastCommand:"-";
            string inv=Commands!=null?Commands.InventoryCount.ToString():"?";
            GUI.Box(new Rect(10,10,430,104),"ROTH Unity 0.7 dev\nMap: "+map+"    Sector ID: "+sector+"    Inventory: "+inv+"\nTrigger: "+trigger+"\nCommand: "+command,_style);
            GUI.Label(new Rect(Screen.width/2-12,Screen.height/2-12,24,24),"+",_crosshair);
        }
    }
}
