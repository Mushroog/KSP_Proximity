using System;
using UnityEngine;
using KSP.IO;
using Proximity.Extensions;

namespace Proximity
{
    class Proximity : PartModule
    {
        private static Rect windowPos = new Rect();
        private static int[] heightArray = {15, 25, 50, 75, 100, 150, 200, 300, 400, 500, 750, 1000, 1500, 2001, 3000, 4000, 5000, 7500, 10001};
        private static int[] velocityArray = {3, 5, 8, 12, 16, 20, 25, 30, 40, 50, 60, 70, 80, 90, 100, 120, 150, 175, 200, 250, 10000};

        private static string[] beepType = { "Square", "Saw", "Sine", "No audio" };
        private static int beepIndex = 0;

        private static string[] pitchType = { "Variable", "440 Hz", "1760 Hz", "3520 Hz"};
        private static int pitchIndex = 0;

        private static bool beepAscent = false;

        private static string[] visualType = { "Distance", "Speed", "Dist >%m, Speed <%m", "No visuals"};
        private static int visualIndex = 0;

        private static string strshowsettings = "Show settings";
        bool showsettings = false;

        public enum eReasonNotDrawn {DrawnOK, NotActiveVessel, NotPrimary, AltitudeRange, TimedoutAndStatic,
            Timewarp, Orbiting, Prelaunch};

        private int ActivationHeight = 2000;
        private int DSThreshold = 200;

        private bool sizechange = true;

        private int skip = 0;
        private int audioskip = 0;
        private int warnPos = 0;
        string warnstring = "------------------------------------------";
        string warn = "";

        public int position = 0;
        public int sampleRate = 0;
        public float frequency = 300;
        private AudioClip beep;
        private GameObject obj;
        private int beepLength = 4;

        double timeSinceLanding = 0;

        private GUIStyle styleBtn;

        public override void OnStart(StartState state)
        {
            if (state != StartState.Editor)
            {
                RenderingManager.AddToPostDrawQueue(0, OnDraw);
                sampleRate = AudioSettings.outputSampleRate;
                
                obj = new GameObject();
                obj.AddComponent("AudioSource");
                MakeBeep();
                obj.audio.ignoreListenerVolume = true;
                obj.audio.volume = 1f;
                DontDestroyOnLoad(obj);
            }
        }

        void OnAudioRead(float[] data) 
        {
            int count = 0;

            while(velocityArray[count++] < vessel.verticalSpeed * -1)
            {}

            float actfrequency;
            
            switch(pitchIndex)
            {
                case 0:
                    actfrequency = frequency + (7f * count * count);
                    break;
                case 1:
                    actfrequency = 440;
                    break;
                case 2:
                    actfrequency = 1760;
                    break;
                case 3:
                    actfrequency = 3520;
                    break;
                default:
                    actfrequency = 880;
                    break;
            }

            count = 0;

            switch (beepIndex)
            { 
                case 0: // square
                    while (count < data.Length) 
                    {
                        //data[count++] = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * actfrequency * position++ / sampleRate));
                        data[count++] = Mathf.Sign(Mathf.Sin(6 * actfrequency * position++ / sampleRate));
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
                        data[count++] = Mathf.Sin(2 * Mathf.PI * actfrequency * position++ / sampleRate);
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

            config.save();
        }

        public override void OnLoad(ConfigNode node)
        {
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
            if (vessel == null)
            {

            }
            else
            {
                eReasonNotDrawn reason;
                if (RightConditionsToDraw(GetAltitude()))
                {
                    if (sizechange)
                    {
                        windowPos.yMax = windowPos.yMin + 20;
                        sizechange = false;
                    }
                    windowPos = GUILayout.Window(10, windowPos, OnWindow, "Proximity");

                    if (windowPos.x == 0 && windowPos.y == 0)
                    {
                        windowPos = windowPos.CentreScreen();
                    }
                }
/*
                else
                {
                    print("Proximity inactive - " + reason.ToString());
                }
 */
            }
        }

        public override void OnUpdate()
        {
        }

        private int GetAltitude()
        {
            if (vessel.situation == Vessel.Situations.FLYING)
            {
                return Convert.ToInt32(vessel.altitude);
            }
            else 
            {
                return Convert.ToInt32(vessel.GetHeightFromTerrain());            
            }
        }

        //private bool RightConditionsToDraw(int alt, out eReasonNotDrawn reason)
        private bool RightConditionsToDraw(int alt)
        {
            //bool retval = true;
            //reason = eReasonNotDrawn.DrawnOK;
/*
            if (this.vessel != FlightGlobals.ActiveVessel)
            {
                retval = false;
                reason = eReasonNotDrawn.NotActiveVessel;
            }
            else if (!this.part.IsPrimary(this.vessel.parts, this.ClassID))
            {
                retval = false;
                reason = eReasonNotDrawn.NotPrimary;
            }
            else if (timeSinceLanding + 5.0 < vessel.missionTime && (Mathf.Abs(Convert.ToInt32(vessel.verticalSpeed)) < 1))
            {
                retval = false;
                reason = eReasonNotDrawn.TimedoutAndStatic;
            }
            else if (TimeWarp.CurrentRateIndex != 0)
            {
                retval = false;
                print("timewarp = " + TimeWarp.CurrentRateIndex.ToString());
                reason = eReasonNotDrawn.Timewarp;
            }
            else if (vessel.situation == Vessel.Situations.ORBITING )
            {
                retval = false;
                reason = eReasonNotDrawn.Orbiting;
            }
            else if (vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                retval = false;
                reason = eReasonNotDrawn.Prelaunch;
            }
            else if (alt > ActivationHeight || alt < 0)
            {
                retval = false;
              
                if (vessel.situation == Vessel.Situations.FLYING)
                {
                    alt = GetAltitude();
                    if (alt > ActivationHeight || alt < 0)
                    { 
                    
                    }
                    //this.vessel.situation = Vessel.Situations.SUB_ORBITAL;
                    print("altitude: " + vessel.altitude.ToString());
                    print("pqsaltitude: " + vessel.pqsAltitude.ToString());
                    print("revealAltitude(): " + vessel.RevealAltitude().ToString());
                    print("terrainAltitude: " + vessel.terrainAltitude.ToString());
                }
                print("sit = " + vessel.situation.ToString() + ", height terrain = " + alt.ToString() + ", height surface = " + vessel.GetHeightFromSurface().ToString() + "altitude: " + vessel.altitude.ToString());

                reason = eReasonNotDrawn.AltitudeRange;
            }
            
            return retval;
*/
                 return (this.vessel == FlightGlobals.ActiveVessel) &&
                     this.part.IsPrimary(this.vessel.parts, this.ClassID) &&
                     alt < ActivationHeight && alt >= 0 &&
                     (timeSinceLanding + 5.0 > vessel.missionTime || Mathf.Abs(Convert.ToInt32(vessel.verticalSpeed)) > 1) &&
                     TimeWarp.CurrentRateIndex == 0 &&
                     vessel.situation != Vessel.Situations.ORBITING &&
                     vessel.situation != Vessel.Situations.PRELAUNCH;
         }

        private void OnWindow(int windowID)
        {
            DoWindow();
        }

        private void DoWindow()
        {
            eReasonNotDrawn reason;
            if (RightConditionsToDraw(GetAltitude()))
            {
                DoProximityContent();
            }
/*
            else
            {
                print("Proximity inactive - " + reason.ToString());
            }
 */
            GUI.DragWindow();
        }

        private void DoProximityContent()
        {
            if (!vessel.LandedOrSplashed)
            {
                timeSinceLanding = vessel.missionTime;
            }

            int alt = GetAltitude();

            Color colour = GetColour(alt);

            int count = 0;
            while (alt > heightArray[count])
            {
                count++;
            }

            skip--;

            if (skip <= 0)
            {
                skip = count;

                if (warnPos < 0)
                {
                    warnPos = 0;
                }

                if (visualIndex == 1 || (visualIndex == 2 && GetAltitude() < DSThreshold)) // visualType = speed
                {
                    warn = GetWarnStringSpeed();
                }
                else if (visualIndex == 0)// visualType = distance
                {
                    warn = GetWarnStringDistance();
                }

                if (vessel.verticalSpeed <= 0 || beepAscent) 
                {
                    DoSound();
                }
            }

            //ShowSituation(alt);

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

        private void ShowSituation(int alt)
        { 
            GUIStyle styleValue = new GUIStyle(GUI.skin.label);
            styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.green;
            styleValue.alignment = TextAnchor.MiddleCenter;

            GUILayout.BeginHorizontal();
            GUILayout.Label(vessel.situation.ToString() + ", alt:" + GetAltitude().ToString(), styleValue);
            GUILayout.EndHorizontal();
        }

        private Color GetColour(int alt)
        { 
            Color colour = Color.white;

            if (vessel.verticalSpeed * -2 > alt && vessel.verticalSpeed > 5)
            {
                colour = Color.magenta;
            }
            else if (vessel.verticalSpeed * -5 > alt)
            {
                colour = Color.yellow;
            }
            else
            {
                colour = Color.green;
            }

            return colour;
        }

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

        private string GetWarnStringSpeed()
        { 
            string warn = warnstring;
            warn = warn.Insert(warnPos, "O");
            warn = warn.Insert(warnstring.Length - (warnPos + 1), "O");

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

        private string GetWarnStringDistance()
        {
            warnPos = warnstring.Length / 2 - ((warnstring.Length / 2) * Convert.ToInt32(vessel.GetHeightFromTerrain()) / ActivationHeight);

            string warn = warnstring;
            warn = warn.Insert(warnPos, "O");
            warn = warn.Insert(warnstring.Length - (warnPos + 1), "O");
            return warn;
        }

        private void DoSound()
        { 
            audioskip--;
            if (audioskip <= 0 && beepIndex != 3)
            {
                audioskip = 10;
                if (obj.audio.isPlaying)
                {
                    obj.audio.Stop();
                }
                obj.audio.Play();
            }
        }
    }
}
