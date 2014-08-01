using System.IO;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

namespace Proximity
{
    [KSPAddon(KSPAddon.Startup.Flight, true)]
    public class StockToolbar : MonoBehaviour
    {
        private static Texture2D shownOn;
        private static Texture2D shownOff;
        private static Texture2D hiddenOn;
        private static Texture2D hiddenOff;

        private ApplicationLauncherButton stockToolbarBtn;

        private bool buttonNeeded = false;
        public bool ButtonNeeded
        {
            get { return buttonNeeded; }
            set { buttonNeeded = value; }
        }

        void Start()
        {
            print ("@@@Toolbar Start");
            if (Proximity.UseStockToolBar)
            {
                Load(ref shownOn, "ProxGrey.png");
                Load(ref shownOff, "ProxGreyCross.png");
                Load(ref hiddenOn, "ProxColour.png");
                Load(ref hiddenOff, "ProxColourCross.png");

                GameEvents.onGUIApplicationLauncherReady.Add(CreateButton);
            }
            DontDestroyOnLoad(this); // twiddle - new
        }

        private void Load(ref Texture2D tex, string file)
        { 
            if (tex == null)
            {
                tex = new Texture2D(36, 36, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), file)));
            }
        }

        public void CreateButton()
        {
            //print("@@@CreateButton - CreateButtons called");
            
            buttonNeeded = Proximity.IsRelevant();
            if (buttonNeeded)
            {
                MakeButton();
            }
            else
            {
                ApplicationLauncher.Instance.RemoveModApplication(stockToolbarBtn);
            }
        }

        private void MakeButton()
        { 
            //print("@@@MakeButton");

            if (stockToolbarBtn != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(stockToolbarBtn);
            }

            stockToolbarBtn = ApplicationLauncher.Instance.AddModApplication(
                ProximityHide, ProximityShow, null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT, GetTexture());
            
            DontDestroyOnLoad(stockToolbarBtn);

            if (!Proximity.ToolbarShowSettings)
            {
                stockToolbarBtn.SetTrue(false);
            }
            else
            {
                stockToolbarBtn.SetFalse(false);
            }

            //print("@@@MakeButton - finished");
        }

        public void RefreshButtonTexture()
        {
            if (stockToolbarBtn != null)
            {
                // here be twiddles
                stockToolbarBtn.SetTexture(GetTexture());
            }
        }

        private void ProximityHide()
        {
            if (Proximity.ToolbarShowSettings)
            {
                Proximity.ToolbarShowSettings = false;
                RefreshButtonTexture();
            }
        }

        private void ProximityShow()
        {
            if (!Proximity.ToolbarShowSettings)
            {
                Proximity.ToolbarShowSettings = true;
                RefreshButtonTexture();
            }
        }

        private Texture2D GetTexture()
        { 
            Texture2D tex;

            if (Proximity.SystemOn)
            {
                tex = (Proximity.ToolbarShowSettings ? shownOn : hiddenOn);
            }
            else
            { 
                tex = (Proximity.ToolbarShowSettings ? shownOff : hiddenOff);
            }

            return tex;
        }

        private void OnDestroy()
        {
            //print("@@@OnDestroy - StockToolbar");

            if (stockToolbarBtn != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(stockToolbarBtn);
            }
        }
    }
}
