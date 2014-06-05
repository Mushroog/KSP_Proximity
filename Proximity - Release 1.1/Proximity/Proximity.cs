using System;
using UnityEngine;
using KSP.IO;
using Proximity.Extensions;

namespace Proximity
{
    class Proximity : PartModule
    {
        private static Rect windowPos = new Rect();

        // beep characteristics
        private static string[] beepType = { "Square", "Saw", "Sine", "No audio" };
        private static int beepIndex = 0;

        private static string[] pitchType = { "Variable", "440 Hz", "880 Hz", "1760 Hz"};
        private static int pitchIndex = 0;

        // beep on ascending
        private static bool beepAscent = false;

        // visual display
        private static string[] visualType = { "Distance", "Speed", "Dist >%m, Speed <%m", "No visuals"};
        private static int visualIndex = 0;

        // expand window
        private static string strshowsettings = "Show settings";
        private static bool showsettings = false;

        private static int ActivationHeight = 3000;
        private static int DSThreshold = 300;

        private int altitude = 0;

        // resize window? - prevents blinking when buttons clicked
        private bool sizechange = true;

        // rate of visual and audio output
        private int skip = 0;
        private int audioskip = 0;
        
        // visual output 
        string warnstring = "------------------------------------------";
        string warn = "";
        private int warnPos = 0;

        // beep characteristics
        public int position = 0;
        public int sampleRate = 0;
        public float frequency = 280;
        private AudioClip beep;
        private GameObject obj;
        private static int beepLength = 3;

        // gives 5 second delay before deactivating after landing
        double timeSinceLanding = 0;

        private GUIStyle styleBtn;

        private bool alwaysShow = false;

        public override void OnStart(StartState state)
        {
            if (state != StartState.Editor)
            {
                //print("@@@OnStart");
                RenderingManager.AddToPostDrawQueue(0, OnDraw);

                sampleRate = AudioSettings.outputSampleRate;
                obj = new GameObject();
                obj.AddComponent("AudioSource");
                MakeBeep();
                obj.audio.ignoreListenerVolume = true;
                obj.audio.volume = 1f;
                DontDestroyOnLoad(obj);

                timeSinceLanding = vessel.missionTime;
            }
        }

        void OnAudioRead(float[] data) 
        {
            // create beep for current situation

            float actfrequency;
            double absspeed = vessel.verticalSpeed;
            if (absspeed > 0)
            {
                absspeed = 0;
            }
            float velocity = Mathf.Min((float)(-1.0 * absspeed), 250f);
            
            switch(pitchIndex)
            {
                case 0:
                    actfrequency = frequency + (velocity * 15f);
                    break;
                case 1:
                    actfrequency = 440;
                    break;
                case 2:
                    actfrequency = 880;
                    break;
                case 3:
                    actfrequency = 1760;
                    break;
                default:
                    actfrequency = 880;
                    break;
            }

            int count = 0;

            switch (beepIndex)
            { 
                case 0: // square
                    while (count < data.Length) 
                    {
                        data[count++] = Mathf.Sign(Mathf.Sin(6.3f * actfrequency * position++ / sampleRate));
                    }
                    break;
                case 1: // saw
                    while (count < data.Length) 
                    {
                        data[count++] = Mathf.PingPong(actfrequency * position++ / sampleRate, 0.5f);
                    }
                    break;
                case 2: // sine
                    while (count < data.Length) 
                    {
                        data[count++] = Mathf.Sin(6.3f * actfrequency * position++ / sampleRate);
                    }
                    break;
                case 3: // shouldn't happen
                    while (count < data.Length) 
                    {
                        data[count++] = 0;
                    }
                    break;
            }
        }

        void OnAudioSetPosition(int newPosition) 
        {
            position = newPosition;
        }

        public override void OnSave(ConfigNode node)
        {
            //print("@@@OnSave");
            PluginConfiguration config = PluginConfiguration.CreateForType<Proximity>();

            config.SetValue("Window Position", windowPos);
            config.SetValue("Beep type", beepIndex);
            config.SetValue("Beep length", beepLength);
            config.SetValue("Visual type", visualIndex);
            config.SetValue("Activation height", ActivationHeight);
            config.SetValue("Distance Speed threshold", DSThreshold);
            config.SetValue("Show settings", showsettings);
            config.SetValue("Pitch", pitchIndex);
            config.SetValue("On Ascent", beepAscent);
            config.SetValue("Always show", alwaysShow);

            config.save();
        }

        public override void OnLoad(ConfigNode node)
        {
            //print("@@@OnLoad");
            PluginConfiguration config = PluginConfiguration.CreateForType<Proximity>();

            config.load();
            windowPos = config.GetValue<Rect>("Window Position");
            beepIndex = config.GetValue<int>("Beep type");
            beepLength = config.GetValue<int>("Beep length");
            visualIndex = config.GetValue<int>("Visual type");
            ActivationHeight = config.GetValue<int>("Activation height");
            DSThreshold = config.GetValue<int>("Distance Speed threshold");
            showsettings = config.GetValue<bool>("Show settings");
            pitchIndex = config.GetValue<int>("Pitch type");
            beepAscent = config.GetValue<bool>("On Ascent");
            alwaysShow = config.GetValue<bool>("Always show");

            if (ActivationHeight < 500)
            {
                ActivationHeight = 2000;
            }

            if (DSThreshold < 50)
            {
                DSThreshold = 200;
            }

            if (ActivationHeight > 10000)
            {
                ActivationHeight = 10000;
            }

            if (DSThreshold > 2000)
            {
                DSThreshold = 2000;
            }

            if (beepLength < 1)
            {
                beepLength = 1;
            }
        }

        private void OnDraw()
        {
            //print("@@@OnDraw");
            if (vessel != null)
            {
                altitude = GetAltitude();
                //print("@@@Altitude = " + altitude.ToString());

                if (RightConditionsToDraw(altitude))
                {
                    if (sizechange)
                    {
                        windowPos.yMax = windowPos.yMin + 20;
                        sizechange = false;
                    }

                    //print("@@@OnDraw - calling OnWindow");
                    windowPos = GUILayout.Window(this.ClassID, windowPos, OnWindow, "Proximity");

                    if (windowPos.x == 0 && windowPos.y == 0)
                    {
                        windowPos = windowPos.CentreScreen();
                    }
                }
            }
        }

        private int GetAltitude()
        {
            //print("@@@GetAltitude");
            float distance;

            // who knows what all the different XaltitudeY methods and fields are supposed to be, but many have unexpected values 
            // in certain situations. These seem to all be appropriate to the situation.
            if (vessel.situation == Vessel.Situations.FLYING)
            {
                if (FlightGlobals.ActiveVessel.heightFromTerrain >= 0)
                {
                    distance = Mathf.Min(FlightGlobals.ActiveVessel.heightFromTerrain, (float)FlightGlobals.ActiveVessel.altitude);
                }
                else 
                {
                    distance = Convert.ToInt32(FlightGlobals.ActiveVessel.altitude);
                }
            }
            else 
            {
                distance = Mathf.Max(vessel.GetHeightFromTerrain(), FlightGlobals.ActiveVessel.heightFromTerrain);
            }
            return Convert.ToInt32(distance);
        }

        private bool RightConditionsToDraw(int alt)
        {
            //print("@@@RightConditionsToDraw");
            // ignore if not this vessel
            if (this.vessel != FlightGlobals.ActiveVessel)
            {
                //print("@@@Not processing - Not active vessel");
                return false;
            }
            // ignore all but one if there are multiple parts on same vessel
            else if (!this.part.IsPrimary(this.vessel.parts, this.ClassID))
            {
                //print("@@@Not processing - multiple part, clsID = " + this.ClassID);
                return false;
            }
            // alwaysShow cancels any further checks - once we know the vessel is active and the part is primary
            else if (alwaysShow)
            {
                return true;
            }
            // switch off 5 seconds after landing
            else if (timeSinceLanding + 5.0 < vessel.missionTime && (Mathf.Abs(Convert.ToInt32(vessel.verticalSpeed)) < 1))
            {
                //print("@@@Not processing - Landed");
                return false;
            }
            // switch off if timewarp on
            else if (TimeWarp.CurrentRateIndex != 0)
            {
                //print("@@@Not processing - Timewarp");
                return false;
            }
            // switch off if not SUB_ORBITAL or FLYING (5 second grace period)
            else if (!(timeSinceLanding + 5.0 > vessel.missionTime || vessel.situation == Vessel.Situations.FLYING ||
                    vessel.situation == Vessel.Situations.SUB_ORBITAL))
            {
                //print("@@@Not processing - Not flying or suborbital");
                return false;
            }
            // because timeSinceLanding hasn't been set yet
            else if (vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                //print("@@@Not processing - prelaunch");
                return false;
            }
            // ignore if not within (ActivationHeight) of surface
            else if (alt > ActivationHeight || alt < 1)
            {
                //print("@@@Not processing - Not in alt range");
                return false;
            }
            // don't display / beep if game paused
            else if (FlightDriver.Pause || PauseMenu.isOpen)
            {
                //print("@@@Not processing - paused");
                return false;
            }
            
            return true;
         }
/*
        private void DebugHeightPrintout()
        {
            print("sit: " + vessel.situation.ToString() + ", GetAltitude() = " + altitude.ToString() +
                ", GetHeightFromSurface() = " + vessel.GetHeightFromSurface().ToString() +
                ", heightFromSurface = " + vessel.heightFromSurface +
                ", heightFromTerrain = " + vessel.heightFromTerrain +
                ", pqsAltitude = " + vessel.pqsAltitude +
                ", PQSAltitude = " + vessel.PQSAltitude() +
                ", FlightGlobals.ActiveVessel.altitude = " + FlightGlobals.ActiveVessel.altitude.ToString() +
                ", FlightGlobals.ship_altitude = " + FlightGlobals.ship_altitude
                );
        }
*/
        private void OnWindow(int windowID)
        {
            //print("@@@OnWindow");
            DoProximityContent();
            GUI.DragWindow();
        }

        private void DoProximityContent()
        {
            CheckLanded();

            Color colour = GetColour(altitude);

            int count = GetBeepInterval();

            skip--;

            if (skip <= 0)
            {
                skip = count;

                if (warnPos < 0)
                {
                    warnPos = 0;
                }

                if (visualIndex == 1 || (visualIndex == 2 && altitude <= DSThreshold)) // visualType = speed
                {
                    warn = GetWarnStringSpeed();
                }
                else if (visualIndex == 0 || (visualIndex == 2 && altitude > DSThreshold)) // visualType = distance
                {
                    warn = GetWarnStringDistance();
                }

                if (vessel.verticalSpeed <= 0 || beepAscent) 
                {
                    DoSound();
                }
            }

            // debugging
            //ShowSituation(altitude);

            // visual
            if (visualIndex != 3)
            {
                GUIStyle style = new GUIStyle(GUI.skin.textArea);
                style.normal.textColor = style.focused.textColor = style.hover.textColor = style.active.textColor = colour;
                style.alignment = TextAnchor.MiddleCenter;

                GUILayout.BeginHorizontal(GUILayout.Width(200f));
                GUILayout.Label(warn, style);
                GUILayout.EndHorizontal();
            }

            ShowSettings();
        }

        private int GetBeepInterval()
        {
            return altitude * ActivationHeight * 15 / (ActivationHeight * ActivationHeight);
        }

        private void CheckLanded()
        {
            // gives 5 second delay before deactivating after landing
            if (!vessel.LandedOrSplashed)
            {
                timeSinceLanding = vessel.missionTime;
            }
        }
/*
        private void ShowSituation(int alt)
        { 
            // debugging - ship situation and actual altitude above terrain
            GUIStyle styleValue = new GUIStyle(GUI.skin.label);
            styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.green;
            styleValue.alignment = TextAnchor.MiddleCenter;

            GUILayout.BeginHorizontal();
            GUILayout.Label(vessel.situation.ToString() + ", alt:" + alt, styleValue);
            GUILayout.EndHorizontal();
        
            styleValue = new GUIStyle(GUI.skin.textArea);
            styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.green;
            styleValue.alignment = TextAnchor.MiddleCenter;
        }
*/
        // colour for visual display bar - cyan if distance mode, else by prox / speed ratio
        private Color GetColour(int alt)
        { 
            Color colour = Color.green;

            if (visualIndex == 0 || (visualIndex == 2 && altitude > DSThreshold))
            {
                colour = Color.cyan;
            }
            else if (vessel.verticalSpeed * -3 > alt && vessel.verticalSpeed * -1 > 3)
            {
                colour = Color.magenta;
            }
            else if (vessel.verticalSpeed * -5 > alt)
            {
                colour = Color.yellow;
            }

            return colour;
        }

        // show settings buttons / fields
        private void ShowSettings()
        { 
            styleBtn = new GUIStyle(GUI.skin.button);
            styleBtn.normal.textColor = styleBtn.focused.textColor = styleBtn.hover.textColor = styleBtn.active.textColor = Color.white;
      		styleBtn.padding = new RectOffset(0, 0, 0, 0);

            GUIStyle styleValue = new GUIStyle(GUI.skin.label);
            styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.green;
            styleValue.alignment = TextAnchor.MiddleCenter;

            GUILayout.BeginHorizontal();
			if (GUILayout.Button(strshowsettings, styleBtn, GUILayout.ExpandWidth(true)))
			{
                showsettings = !showsettings;
                strshowsettings = (showsettings ? "Hide" : "Show") + " settings";
                sizechange = true;
			}
		    GUILayout.EndHorizontal();

            if (showsettings)
            {
                // Activation height
                GUILayout.BeginHorizontal();
                GUILayout.Label("Activation height: ");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.Width(200f));
                if (GUILayout.Button("-", styleBtn, GUILayout.ExpandWidth(true)))
                {
                    ActivationHeight -= 500;
                    if (ActivationHeight < 500)
                    {
                        ActivationHeight = 500;
                    }
                }

                GUILayout.Label(ActivationHeight.ToString() + " m", styleValue);
                
                if (GUILayout.Button("+", styleBtn, GUILayout.ExpandWidth(true)))
                {
                    ActivationHeight += 500;
                    if (ActivationHeight > 10000)
                    {
                        ActivationHeight = 10000;
                    }
                }

                GUILayout.EndHorizontal();

                // Visual type
                GUILayout.BeginHorizontal();
                string cap = "Visual: " + visualType[visualIndex];
                cap = cap.Replace("%", DSThreshold.ToString());
                if (GUILayout.Button(cap, styleBtn, GUILayout.ExpandWidth(true)))
                {
                    visualIndex++;
                    if (visualIndex == visualType.Length)
                    {
                        visualIndex = 0;
                    }
                    if (visualIndex == 2 || visualIndex == 3) // change size due to threshold field appearing / disappearing
                    {
                        sizechange = true;
                    }
                }
                GUILayout.EndHorizontal();

                // Visual threshold subtype
                if (visualIndex == 2)
                { 
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Distance / speed threshold: ");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal(GUILayout.Width(200f));
                    if (GUILayout.Button("-", styleBtn, GUILayout.ExpandWidth(true)))
                    {
                        DSThreshold -= 50;
                        if (DSThreshold < 50)
                        {
                            DSThreshold = 50;
                        }
                    }

                    GUILayout.Label(DSThreshold.ToString() + " m", styleValue);
                
                    if (GUILayout.Button("+", styleBtn, GUILayout.ExpandWidth(true)))
                    {
                        DSThreshold += 50;
                        if (DSThreshold > 2000)
                        {
                            DSThreshold = 2000;
                        }
                    }
                    GUILayout.EndHorizontal();
                }

                // Sound type
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Sound: " + beepType[beepIndex], styleBtn, GUILayout.ExpandWidth(true)))
                {
                    beepIndex++;
                    if (beepIndex == beepType.Length)
                    {
                        beepIndex = 0;
                    }
                }
                GUILayout.EndHorizontal();

                // beep pitch 
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Pitch: " + pitchType[pitchIndex], styleBtn, GUILayout.ExpandWidth(true)))
                {
                    pitchIndex++;
                    if (pitchIndex == pitchType.Length)
                    {
                        pitchIndex = 0;
                    }
                }
                GUILayout.EndHorizontal();
                
                // beep ascending
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Beep on ascent: " + beepAscent.ToString(), styleBtn, GUILayout.ExpandWidth(true)))
                {
                    beepAscent = !beepAscent;
                }
                GUILayout.EndHorizontal();

                // beep length
                GUILayout.BeginHorizontal();
                GUILayout.Label("Beep length (1-20): ");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.Width(200f));
                if (GUILayout.Button("-", styleBtn, GUILayout.ExpandWidth(true)))
                {
                    beepLength--;
                    if (beepLength < 1)
                    {
                        beepLength = 1;
                    }
                    MakeBeep();
                }

                GUILayout.Label(beepLength.ToString(), styleValue);
                
                if (GUILayout.Button("+", styleBtn, GUILayout.ExpandWidth(true)))
                {
                    beepLength++;
                    if (beepLength > 20)
                    {
                        beepLength = 20;
                    }
                    MakeBeep();
                }

                GUILayout.EndHorizontal();
            }
        }

        private void MakeBeep()
        { 
            beep = AudioClip.Create("beepx", beepLength * 512, 1, 44100, false, true, OnAudioRead, OnAudioSetPosition);
            obj.audio.clip = beep;
        }

        // make visual display string in Speed mode
        private string GetWarnStringSpeed()
        { 
            string warn = warnstring;
            warn = warn.Insert(warnPos, "O");
            warn = warn.Insert(warnstring.Length - warnPos, "O");

            if (vessel.verticalSpeed <= 0) // falling
            {
                if (++warnPos >= warnstring.Length / 2)
                {
                    warnPos = 0;
                }
            }
            else // rising
            {
                if (--warnPos < 1)
                {
                    warnPos = warnstring.Length / 2;
                }
            }

            return warn;
        }

        // make visual display string in Distance mode
        private string GetWarnStringDistance()
        {
            warnPos = warnstring.Length / 2 - ((warnstring.Length / 2) * Convert.ToInt32(vessel.GetHeightFromTerrain()) / ActivationHeight);

            string warn = warnstring;
            warn = warn.Insert(warnPos, "O");
            warn = warn.Insert(warnstring.Length - (warnPos + 1), "O");
            return warn;
        }

        // play beep
        private void DoSound()
        { 
            if (FlightDriver.Pause || PauseMenu.isOpen)
            {
                return;
            }

            audioskip--;
            if (audioskip <= 0 && beepIndex != 3)
            {
                audioskip = 8;
                if (obj.audio.isPlaying)
                {
                    obj.audio.Stop();
                }
                obj.audio.Play();
            }
        }
    }
}
