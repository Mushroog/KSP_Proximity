//#define PERSONAL_VERSION

using System;
using System.Collections.Generic;
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

        private static bool deactivateIfRover = true;

        // visual display
        private static string[] visualType = { "Distance", "Speed", "Dist >%m, Speed <%m", "No visuals"};
        private static int visualIndex = 0;

        // expand window - showsettings is toggled by the button on the prox window itself. It is not used
        // if settingsMode (controlled from the toolbar button) is true, in which case settings are always shown and 
        // the button on the prox window is not shown
        private static bool GUIShowSettings = false;
        private static bool ToolbarShowSettings = false;
        private static bool UseToolbar = false;

        private static int ActivationHeight = 3000; // for proximity as a whole
        private static int DSThreshold = 300; // threshold height for both (a) switching between the two visual modes,
            // and (b) for activating landing lights and legs.

        private static ToolbarButtonWrapper toolbarButton = null;

        private int altitude = 0;

        // resize window? - prevents blinking when buttons clicked
        private bool sizechange = true;

        // rate of visual and audio output
        private int skip = 0;
        private int audioskip = 0;
        
        // visual output 
        private static string warnstring = "--------------------------------------------------"; // -2

        private static string warn = "";
        private static string[] breakup = {"_", " ", "=", "o", "+", "O", ".", " "}; // visual display glitch chars for hard landing
 
        private int warnPos = 0;

        // beep characteristics
        public int position = 0;
        public int sampleRate = 0;
        public float frequency = 280;
        private AudioClip beep;
        private GameObject obj;
        private static int beepLength = 3;

#if PERSONAL_VERSION
        private static bool autoExtendLandingLegs = false; 
        private bool readyToLand = false; // true when proximity has switched on lights and extended landing legs
        private static bool autoParachute = false; 
#endif

        private static float volume = 0.5f;

        private static bool deactivateOnParachute = true;
        private bool parachutesOpen = false;

        // makes note of speed so that one call to the function can compare speed with the previous call
        private double OldSpeed = 0.0;

        // gives 5 second delay before deactivating after landing
        double timeSinceLanding = 0;

        // switches off if ascending for > 5 seconds
        double timeSinceDescending = 0;

        // switches off for 10 sec after launch
        double timeSinceOnGround = 0;

        private GUIStyle styleBtn;

        private static bool prevConditionalShow = false;
        private static bool ConditionalShow = false;

        private static bool isPowered = true;

        // used in beep generation optimizing 
        private static float[] Sines;
        //private static float[] PingPongs;
        private static int oldBeepIndex = -1;
        private static float oldActualFrequency = -1f;

        private bool SystemOn = true;

        private float fixedwidth = 210f;

        public override void OnStart(StartState state)
        {
            try
            {
                //print("@@@Toolbar init");
                toolbarButton = new ToolbarButtonWrapper("Proximity", "toolbarButton");
                SetButtonTexture();
                toolbarButton.ToolTip = "Proximity settings";
                toolbarButton.Visible = true;
                toolbarButton.AddButtonClickHandler((e) =>
                {
                    //print("@@@ prox toolbar button clicked");
                    ToolbarShowSettings = !ToolbarShowSettings;
                    SetButtonTexture();
                    windowPos.yMax = windowPos.yMin + 20;
                });
                UseToolbar = true;
            }
            catch (Exception ex)
            { 
                //print("@@@Exception on toolbar init, msg = " + ex.Message);
            }

            if (state != StartState.Editor)
            {
                //print("@@@OnStart");
                RenderingManager.AddToPostDrawQueue(0, OnDraw);

                sampleRate = AudioSettings.outputSampleRate;
                obj = new GameObject();
                obj.AddComponent("AudioSource");
                MakeBeep();
                obj.audio.ignoreListenerVolume = true;
                obj.audio.volume = volume;
                //DontDestroyOnLoad(obj);

                timeSinceLanding = vessel.missionTime;

                if (Sines == null)
                {
                    Sines = new float[10000];
                    for (int i = 0; i < 10000; i++)
                    {
                        Sines[i] = Mathf.Sin((float)i);
                    }
                }
/*
                if (PingPongs == null)
                {
                    PingPongs = new float[10000];
                    for (int i = 0; i < 10000; i++)
                    {
                        PingPongs[i] = Mathf.PingPong((float)i, 0.5f);
                    }
                }

                string s = "@@@";
                for (int i = 0; i < 10000; i += 100)
                {
                    s += PingPongs[i].ToString() + ", ";
                }
                print(s);
*/
            }
        }

        private void SetButtonTexture()
        {
            string path = "Proximity/ToolbarIcons/ProxS";
            //path += ConditionalShow ? "S" : "B";
            path += ToolbarShowSettings ? "G" : "C";

            toolbarButton.TexturePath = path;
        }

        void OnAudioRead(float[] data) 
        {
            // create beep for current situation
            
            //print("@@@OnAudioRead" + Time.time.ToString());
            if (!ShouldBeep())
            {
                return;
            }

            float actfrequency;
            
            switch(pitchIndex)
            {
                case 0:
                    double absspeed = vessel.verticalSpeed;
                    if (absspeed > 0)
                    {
                        absspeed = 0;
                    }
                    float velocity = Mathf.Min((float)(-1.0 * absspeed), 250f);
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

            if (Mathf.Abs(oldActualFrequency - actfrequency) < 8 && oldBeepIndex == beepIndex)
            {
                // todo - does this need to be copied?
                obj.audio.clip.GetData(data, 0);
                return;
            }
            else 
            {
                oldActualFrequency = actfrequency;
                oldBeepIndex = beepIndex;
            }

            int count = 0;
            int num = 0;
            switch (beepIndex)
            { 
                case 0: // square
                    while (count < data.Length) 
                    {
                        num = (int)(6.3f * actfrequency * position++ / sampleRate);
                        data[count++] = Mathf.Sign(Sines[num]);
                    }
                    break;
                case 1: // saw
                    while (count < data.Length) 
                    {
                        data[count++] = Mathf.PingPong(actfrequency * position++ / sampleRate, 0.5f);
                        data[count] = data[count++ - 1];
                        position++;
                    }
                    break;
                case 2: // sine
                    while (count < data.Length) 
                    {
                        num = (int)(6.3f * actfrequency * position++ / sampleRate);
                        data[count++] = Sines[num];
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
            config.SetValue("Show settings", UseToolbar ? ToolbarShowSettings: GUIShowSettings);
            config.SetValue("Pitch type", pitchIndex);
            config.SetValue("Off if parachute", deactivateOnParachute);
            config.SetValue("Off if rover", deactivateIfRover);
            config.SetValue("Volume", (int)(volume * 100));
#if PERSONAL_VERSION
            config.SetValue("Autodeploy parachute", autoParachute);
            config.SetValue("Autodeploy landing legs", autoExtendLandingLegs);
#endif
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
            GUIShowSettings = config.GetValue<bool>("Show settings");
            ToolbarShowSettings = GUIShowSettings;
            pitchIndex = config.GetValue<int>("Pitch type");
            deactivateOnParachute = config.GetValue<bool>("Off if parachute");
            deactivateIfRover = config.GetValue<bool>("Off if rover");
            int vol = config.GetValue<int>("Volume");
            volume = ((float)vol) / 100f;
#if PERSONAL_VERSION
            autoParachute = config.GetValue<bool>("Autodeploy parachute");
            autoExtendLandingLegs = config.GetValue<bool>("Autodeploy landing legs");
#endif
            if (volume < 0.01f)
            {
                // no config file, set defaults
                ActivationHeight = 4000;
                DSThreshold = 500;

                beepLength = 3;

                beepIndex = 1;
                pitchIndex = 0;
                visualIndex = 1;

                volume = 0.5f;

                deactivateIfRover = true;
            }
            else
            {
                if (ActivationHeight < 500) ActivationHeight = 500;
                if (ActivationHeight > 10000) ActivationHeight = 10000;

                if (DSThreshold < 200) DSThreshold = 200;
                if (DSThreshold > 2000) DSThreshold = 2000;

                if (beepLength < 1) beepLength = 1;
                if (beepLength > 10) beepLength = 10;

                if (beepIndex < 0 || beepIndex > 3) beepIndex = 1;
                if (pitchIndex < 0 || pitchIndex > 3) pitchIndex = 0;
                if (visualIndex < 0 || visualIndex > 3) visualIndex = 1;

                if (volume > 1.0f) volume =1.0f;
            }
        }

        private void OnDraw()
        {
            //print("@@@OnDraw");
            if (vessel != null)
            {
                altitude = GetAltitude();

                isPowered = IsPowered();

#if PERSONAL_VERSION
                PrepareToLand();
#endif
                if (RightConditionsToDraw())
                {
                    // no window if no visuals && no settings
                    if (visualIndex == 3 && !((!UseToolbar && GUIShowSettings && ConditionalShow) || (UseToolbar && ToolbarShowSettings)))
                    {
                        return;
                    }

                    if (ConditionalShow != prevConditionalShow)
                    {
                        sizechange = true;
                    }

                    if (sizechange)
                    {
                        windowPos.yMax = windowPos.yMin + 20;
                        sizechange = false;
                        windowPos.xMax = 240;
                    }

                    windowPos = GUILayout.Window(this.ClassID, windowPos, OnWindow, ConditionalShow ? "Proximity" : "Proximity settings");

                    if (windowPos.width < 220) windowPos.width = 220;

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

        private bool RightConditionsToDraw()
        {
            //print("@@@RightConditionsToDraw");
            bool retval = true;

            if (!part.IsPrimary(vessel.parts, ClassID))
            {
                //print("@@@Not processing - multiple part, clsID = " + this.ClassID);
                retval = false;
                return false; // this is such a hack
            }
            
            // switch off 5 seconds after landing
            if (timeSinceLanding + 5.0 < vessel.missionTime && (Mathf.Abs(Convert.ToInt32(vessel.verticalSpeed)) < 1))
            {
                //print("@@@Not processing - Landed");
                retval = false;
            }
            // switch off if timewarp on
            else if (TimeWarp.CurrentRateIndex != 0)
            {
                //print("@@@Not processing - Timewarp");
                retval = false;
            }
            // switch off if not SUB_ORBITAL or FLYING (5 second grace period)
            else if (!(timeSinceLanding + 5.0 > vessel.missionTime || vessel.situation == Vessel.Situations.FLYING ||
                    vessel.situation == Vessel.Situations.SUB_ORBITAL))
            {
                //print("@@@Not processing - Not flying or suborbital");
                retval = false;
            }
            // because timeSinceLanding hasn't been set yet
            else if (vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                //print("@@@Not processing - prelaunch");
                retval = false;
            }
            // ignore if not within (ActivationHeight) of surface
            else if (altitude > ActivationHeight || altitude < 1)
            {
                //print("@@@Not processing - Not in alt range");
                retval = false;
            }
            // don't display / beep if game paused
            else if (FlightDriver.Pause || PauseMenu.isOpen)
            {
                //print("@@@Not processing - paused");
                retval = false;
            }
            // don't display if parachutes open
            else if (parachutesOpen && deactivateOnParachute)
            {
                //print("@@@Not processing - parachute");
                retval = false;
            }
            // don't display if rover
            else if (deactivateIfRover && vessel.vesselType == VesselType.Rover)
            {
                
                retval = false;
            }
            else if (timeSinceOnGround + 5.0 > vessel.missionTime && vessel.verticalSpeed >= 0)
            {
                //print("@@@Not processing - just launched");
                retval = false;
            }
            else if (!(timeSinceDescending + 5.0 > vessel.missionTime) && vessel.verticalSpeed >= 0)
            {
                retval = false;
            }

            prevConditionalShow = ConditionalShow;
            ConditionalShow = retval;
            return ConditionalShow || (UseToolbar && ToolbarShowSettings);
         }

        private void OnWindow(int windowID)
        {
            //print("@@@OnWindow");
            DoProximityContent();
            GUI.DragWindow();
        }

        private void DoProximityContent()
        {
            CheckLanded();

            //print("@@@DoProximityContent()");
            if (ConditionalShow)
            {
                CheckChutes();

                int count = GetBeepInterval();

                skip--;

                if (skip <= 0)
                {
                    skip = count;

                    if (isPowered && SystemOn)
                    {
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

                        warn = AddCrackle(warn);

                        DoSound();
                    }
                    else
                    {
                        warn = "---------------------unpowered----------------------";
                    }
                }
            }

            ShowGraphicalIndicator();

            ShowSettings();
        }

        private void ShowGraphicalIndicator()
        {
            if (SystemOn && ConditionalShow && visualIndex != 3)
            {
                GUIStyle style = new GUIStyle(GUI.skin.textArea);
                style.normal.textColor = style.focused.textColor = style.hover.textColor = style.active.textColor = GetColour(altitude);
                style.alignment = TextAnchor.MiddleCenter;
                style.wordWrap = false;
                style.stretchWidth = false;
                style.fixedWidth = fixedwidth;
                style.stretchHeight = false;
                style.fixedHeight = 25f;

                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth));
                GUILayout.Label(warn, style);
                GUILayout.EndHorizontal();
            }
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

            // allows us to switch off if ascending for > 5 seconds (ie launch)
            if (vessel.verticalSpeed < 0)
            {
                timeSinceDescending = vessel.missionTime;
            }

            if (vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.PRELAUNCH)
            {
                timeSinceOnGround = vessel.missionTime;
            }
        }

        // colour for visual display bar - cyan if distance mode, else by prox / speed ratio
        private Color GetColour(int alt)
        { 
            Color colour = Color.green;

            if (!isPowered)
            {
                colour = Color.grey;
            }
            else if (vessel.verticalSpeed >= 0)
            {
                // going up
                colour = Color.white;
            }
            else if (visualIndex == 0 || (visualIndex == 2 && altitude > DSThreshold))
            {
                // distance mode
                colour = Color.cyan;
            }
            else
            {
                // speed mode - colour for danger
                float danger = (float)vessel.verticalSpeed * -2.5f / (altitude + 4);
                if (danger < 0.0)
                {
                    danger = 0.0f;
                }
                else if (danger > 1.0)
                {
                    danger = 1.0f;
                }

                if (danger <= 0.5f)
                {
                    colour = Color.Lerp(Color.green, Color.yellow, danger * 2.0f);
                }
                else
                {
                    colour = Color.Lerp(Color.yellow, Color.red, (danger - 0.5f) * 2.0f);
                }
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

            if (!UseToolbar && ConditionalShow)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button((GUIShowSettings ? "Hide" : "Show") + " settings", styleBtn, GUILayout.ExpandWidth(true)))
                {
                    GUIShowSettings = !GUIShowSettings;
                    sizechange = true;
                }
                GUILayout.EndHorizontal();
            }

            if ((!UseToolbar && GUIShowSettings && ConditionalShow) || (UseToolbar && ToolbarShowSettings))
            {
                // Activation height
                GUILayout.BeginHorizontal();
                GUILayout.Label("Active below:");
                if (GUILayout.Button("-", styleBtn, GUILayout.ExpandWidth(true)))
                {
                    ActivationHeight -= 500;
                    if (ActivationHeight < 500) ActivationHeight = 500;
                }

                GUILayout.Label(ActivationHeight.ToString() + " m", styleValue);
                
                if (GUILayout.Button("+", styleBtn, GUILayout.ExpandWidth(true)))
                {
                    ActivationHeight += 500;
                    if (ActivationHeight > 10000) ActivationHeight = 10000;
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

#if ! PERSONAL_VERSION
                // Visual threshold subtype
                if (visualIndex == 2)
                {
                    ThresholdHeight(styleValue);
                }
#endif

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

                // beep volume 
                float oldvol = volume;
                GUILayout.BeginHorizontal();
                GUILayout.Label("Volume:", GUILayout.ExpandWidth(false));
                volume = GUILayout.HorizontalSlider(volume, 0.01f, 1f, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                if (volume != oldvol)
                { 
                    obj.audio.volume = volume;
                }

                // beep length
                GUILayout.BeginHorizontal();
                GUILayout.Label("Beep length: ");
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
                    if (beepLength > 10)
                    {
                        beepLength = 10;
                    }
                    MakeBeep();
                }

                GUILayout.EndHorizontal();

                // parachutes
                GUIStyle styleToggle = new GUIStyle(GUI.skin.toggle);
                styleToggle.fixedWidth = fixedwidth;

                GUILayout.BeginHorizontal();
                deactivateOnParachute = GUILayout.Toggle(deactivateOnParachute, "  Off if parachutes open", styleToggle, null);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                deactivateIfRover = GUILayout.Toggle(deactivateIfRover, "  Off if vessel is rover", styleToggle, null);
                GUILayout.EndHorizontal();

#if PERSONAL_VERSION
                // Configure ship for landing
                styleToggle = new GUIStyle(GUI.skin.toggle);
                styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.green;
                styleValue.alignment = TextAnchor.MiddleCenter;

                bool oldauto = autoExtendLandingLegs;
                GUILayout.BeginHorizontal();
                autoExtendLandingLegs = GUILayout.Toggle(autoExtendLandingLegs, "  Autodeploy landing legs", styleToggle, null);
                GUILayout.EndHorizontal();
                if (autoExtendLandingLegs != oldauto)
                {
                    sizechange = true;
                }

                oldauto = autoParachute;
                GUILayout.BeginHorizontal();
                autoParachute = GUILayout.Toggle(autoParachute, "  Autodeploy parachute", styleToggle, null);
                GUILayout.EndHorizontal();
                if (autoParachute != oldauto)
                {
                    sizechange = true;
                }

                // Visual threshold subtype
                if (visualIndex == 2 || autoExtendLandingLegs || autoParachute)
                {
                    ThresholdHeight(styleValue);
                }
#endif

                styleBtn.normal.textColor = styleBtn.focused.textColor = styleBtn.hover.textColor = styleBtn.active.textColor = SystemOn ? Color.red: Color.green;
                styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.white;

                GUILayout.BeginHorizontal();
                GUILayout.Label("Proximity ", styleValue);
                styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = SystemOn ? Color.green: Color.red;
                GUILayout.Label(SystemOn ? "ON ": "OFF ", styleValue);
                if (GUILayout.Button(SystemOn ? "Switch off": "Switch on", styleBtn, GUILayout.ExpandWidth(true)))
                {
                    SystemOn = !SystemOn;
                    sizechange = true;
                }
                GUILayout.EndHorizontal();
            }
        }

        private void ThresholdHeight(GUIStyle style)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Threshold: ");
            if (GUILayout.Button("-", styleBtn, GUILayout.ExpandWidth(true)))
            {
                DSThreshold -= 50;
                if (DSThreshold < 50)
                {
                    DSThreshold = 50;
                }
            }

            GUILayout.Label(DSThreshold.ToString() + " m", style);

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

        private void MakeBeep()
        { 
            beep = AudioClip.Create("beepx", beepLength * 512, 1, 44100, false, true, OnAudioRead, OnAudioSetPosition);
            obj.audio.clip = beep;
        }

        // make visual display string in Speed mode
        private string GetWarnStringSpeed()
        { 
            string warn = warnstring;
            warn = warn.Insert(warnstring.Length - warnPos, "O");
            warn = warn.Insert(warnPos, "O");

            if (vessel.verticalSpeed <= 0) // falling
            {
                if (++warnPos > warnstring.Length / 2)
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
            warnPos = warnstring.Length / 2 - ((warnstring.Length / 2) * altitude / ActivationHeight);

            string warn = warnstring;
            warn = warn.Insert(warnstring.Length - (warnPos + 1), "O");
            warn = warn.Insert(warnPos + 1, "O");            
            return warn;
        }

        // play beep
        private void DoSound()
        { 
            if (!ShouldBeep())
            {
                return;
            }

            audioskip--;
            if (audioskip <= 0 && beepIndex != 3)
            {
                audioskip = 8;

                obj.audio.Play();
            }
        }

        private bool ShouldBeep()
        { 
            if (vessel.verticalSpeed > 0)
            {
                return false;
            }

            if (FlightDriver.Pause || PauseMenu.isOpen)
            {
                return false;
            }

            return true;
        }

#if PERSONAL_VERSION
        private void PrepareToLand()
        {
            if (SystemOn && (vessel.situation == Vessel.Situations.SUB_ORBITAL || vessel.situation == Vessel.Situations.FLYING) &&
                isPowered && altitude > 0 && altitude < DSThreshold && vessel.verticalSpeed < -0.5)
            {
                if (autoExtendLandingLegs && !readyToLand)
                {
                    List<ModuleLandingLeg> lleg = vessel.FindPartModulesImplementing<ModuleLandingLeg>();
                    List<ModuleLandingLeg>.Enumerator en = lleg.GetEnumerator();
                    while (en.MoveNext())
                    {
                        en.Current.Invoke("LowerLeg", 0.1f);
                    }
                    en.Dispose();

                    List<ModuleLight> llight = vessel.FindPartModulesImplementing<ModuleLight>();
                    
                    List<ModuleLight>.Enumerator el = llight.GetEnumerator();
                    while (el.MoveNext())
                    {
                        el.Current.Invoke("LightsOn", 0.1f);
                    }
                    el.Dispose();

                    readyToLand = true;
                }

                if (autoParachute && !parachutesOpen && vessel.mainBody.atmosphere)
                { 
                    List<ModuleParachute> lpara = vessel.FindPartModulesImplementing<ModuleParachute>();
                    List<ModuleParachute>.Enumerator en = lpara.GetEnumerator();
                    while (en.MoveNext())
                    {
                        en.Current.Invoke("Deploy", 0.1f);
/*                     
                        List<BaseAction>.Enumerator act = en.Current.Actions.GetEnumerator();
                        while (act.MoveNext())
                        {
                            print("@@@" + act.Current.name + ", " + act.Current.guiName);
                        }
 */ 
                    }
                    en.Dispose();
                    parachutesOpen = true;
                }
            }
            else if (vessel.verticalSpeed > 1)
            {
                readyToLand = false;
            }
        }
#endif

        private void CheckChutes()
        { 
            if (!parachutesOpen)
            {
                List<ModuleParachute> lpara = vessel.FindPartModulesImplementing<ModuleParachute>();
                List<ModuleParachute>.Enumerator en = lpara.GetEnumerator();
                while (en.MoveNext())
                {
                    if (en.Current.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED ||
                        en.Current.deploymentState == ModuleParachute.deploymentStates.DEPLOYED)
                    {
                        parachutesOpen = true;
                        break;
                    }
                }
                en.Dispose();
            }
        }

        public void OnDestroy()
        {
            if (toolbarButton != null)
            {
                toolbarButton.Destroy();
            }
        }

        private string AddCrackle(string warn)
        {
            if (OldSpeed < 0 && vessel.verticalSpeed > OldSpeed + 2)
            {
                for (int i = (int)(1 - OldSpeed); i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, warn.Length - 2);
                    warn = warn.Remove(j, 1);
                    int k = UnityEngine.Random.Range(0, 8);
                    warn = warn.Insert(j, breakup[k]);
                    if (i > 20)
                    {
                        i = 20;
                        OldSpeed *= 0.5;
                    }
                }
                OldSpeed += 0.6;
            }
            else
            {
                OldSpeed = vessel.verticalSpeed;
            }

            return warn;
        }

        private bool IsPowered()
        { 
            double electricCharge = 0;
            foreach (Part p in vessel.parts)
            {
                foreach (PartResource pr in p.Resources)
                {
                    if (pr.resourceName.Equals("ElectricCharge"))
                    {
                        electricCharge += pr.amount;
                        break;
                    }
                }
            }

            return electricCharge > 0.04;
        }
    }
}
