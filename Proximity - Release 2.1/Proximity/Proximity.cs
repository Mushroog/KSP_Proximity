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
        private static string[] beepType = { "Square", "Saw", "Sine", "None" };
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
        private static bool toolbarShowSettings = false;
        public static bool ToolbarShowSettings
        {
            get { return toolbarShowSettings; }
            set 
            { 
                toolbarShowSettings = value;
                sizechange = true;
            }
        }

        private static bool UseToolbar = false;

        private static int ActivationHeight = 3000; // for proximity as a whole
        private static int DSThreshold = 300; // threshold height for both (a) switching between the two visual modes,
            // and (b) for activating landing lights and legs.

        private static ToolbarButtonWrapper toolbarButton = null;

        private int altitude = 0;

        private bool newInstance = true;

        // resize window? - prevents blinking when buttons clicked
        private static bool sizechange = true;

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

        private static bool prevConditionalShow = false;
        private static bool ConditionalShow = false;

        private static bool isPowered = true;

        // used in beep generation optimizing 
        private static float[] Sines;

        private static int oldBeepIndex = -1;
        private static float oldActualFrequency = -1f;

        private static bool useStockToolBar = true;
        public static bool UseStockToolBar
        {
            get { return useStockToolBar; }
            set { useStockToolBar = value; }
        }
        
        private static bool systemOn = true;
        public static bool SystemOn
        {
            get { return systemOn; }
            set { systemOn = value; }
        }

        private const float fixedwidth = 255f;
        private const float margin = 20f;
        
        private GUIStyle styleTextArea = null;
        private GUIStyle styleButton = null;
        private GUIStyle styleValue = null;
        private GUIStyle styleToggle = null;

        private double gracePeriod = 3.0;

        Vessel ActiveVessel = null;

        bool lostToStaging = false;

        private int numParts = -1;
        private int stage = -1;

        public override void OnStart(StartState state)
        {
            if (!useStockToolBar) // blizzy
            {
                try
                {
                    //print("@@@Toolbar init");
                    toolbarButton = new ToolbarButtonWrapper("Proximity", "toolbarButton");
                    RefreshBlizzyButton();
                    toolbarButton.ToolTip = "Proximity settings";
                    toolbarButton.Visible = true;
                    toolbarButton.AddButtonClickHandler((e) =>
                    {
                        //print("@@@ prox toolbar button clicked");
                        toolbarShowSettings = !toolbarShowSettings;
                        sizechange = true;
                        RefreshBlizzyButton();
                        //windowPos.yMax = windowPos.yMin + 20;
                    });
                }
                catch (Exception ex)
                {
                    //print("@@@Exception on blizzy toolbar init, msg = " + ex.Message);

                }
                UseToolbar = true;
            }
            else // stock
            {
                UseToolbar = true;
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

                timeSinceLanding = FlightGlobals.ActiveVessel.missionTime;

                if (Sines == null)
                {
                    Sines = new float[10000];
                    for (int i = 0; i < 10000; i++)
                    {
                        Sines[i] = Mathf.Sin((float)i);
                    }
                }

                sizechange = true;
            }
        }

        private bool RefreshBlizzyButton()
        {
            bool relevant = IsRelevant();
            toolbarButton.Visible = relevant;

            if (relevant)
            {
                string path = "Proximity/ToolbarIcons/ProxS";

                path += toolbarShowSettings ? "G" : "C";

                if (!SystemOn)
                {
                    path += "X";
                }

                toolbarButton.TexturePath = path;
            }
            else
            {
                lostToStaging = true;
            }

            return relevant;
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
                    double absspeed = FlightGlobals.ActiveVessel.verticalSpeed;
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
            config.SetValue("Show settings", UseToolbar ? toolbarShowSettings: GUIShowSettings);
            config.SetValue("Pitch type", pitchIndex);
            config.SetValue("Off if parachute", deactivateOnParachute);
            config.SetValue("Off if rover", deactivateIfRover);
            config.SetValue("Volume", (int)(volume * 100));
            config.SetValue("Toolbar", useStockToolBar ? "stock": "blizzy");

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

            try
            {
                windowPos = config.GetValue<Rect>("Window Position");
                beepIndex = config.GetValue<int>("Beep type");
                beepLength = config.GetValue<int>("Beep length");
                visualIndex = config.GetValue<int>("Visual type");
                ActivationHeight = config.GetValue<int>("Activation height");
                DSThreshold = config.GetValue<int>("Distance Speed threshold");
                GUIShowSettings = config.GetValue<bool>("Show settings");
                toolbarShowSettings = GUIShowSettings;
                pitchIndex = config.GetValue<int>("Pitch type");
                deactivateOnParachute = config.GetValue<bool>("Off if parachute");
                deactivateIfRover = config.GetValue<bool>("Off if rover");
                int vol = config.GetValue<int>("Volume");
                volume = ((float)vol) / 100f;
                string s = config.GetValue<string>("Toolbar");
                s = s.ToLower();
                useStockToolBar = !(s.Contains("blizzy"));
            }
            catch (Exception ex)
            { 
                // likely a line is missing. 
            }

#if PERSONAL_VERSION
            autoParachute = config.GetValue<bool>("Autodeploy parachute");
            autoExtendLandingLegs = config.GetValue<bool>("Autodeploy landing legs");
#endif

            windowPos.width = fixedwidth;

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
            if (FlightGlobals.ActiveVessel != null)
            {
                ActiveVessel = FlightGlobals.ActiveVessel;

                altitude = GetAltitude();

                isPowered = IsPowered();
                
                // this takes account of vessels splitting (when undocking), Kerbals going on EVA, etc.
                if (newInstance || (useStockToolBar && (ActiveVessel.parts.Count != numParts || ActiveVessel.currentStage != stage)))
                {
                    numParts = ActiveVessel.parts.Count;
                    stage = ActiveVessel.currentStage;
                    //print("@@@num parts = " + numParts.ToString() + ", stage = " + stage.ToString());
                    
                    newInstance = false;
                    lostToStaging = false;
                    if (useStockToolBar)
                    {
                        if (!RefreshStockButton())
                        {
                            return;
                        }
                    }
                    else
                    {
                        if (!RefreshBlizzyButton())
                        {
                            return;
                        }
                    }
                }

#if PERSONAL_VERSION
        PrepareToLand();
#endif
                if (RightConditionsToDraw())
                {
                    //print("@@@Right conditions met");
                    if (ConditionalShow != prevConditionalShow)
                    {
                        sizechange = true;
                        skip = 0;
                    }

                    // no window if no visuals && no settings
                    if (visualIndex == 3 && !((!UseToolbar && GUIShowSettings && ConditionalShow) || (UseToolbar && toolbarShowSettings)))
                    {
                        return;
                    }
                    
                    styleTextArea = new GUIStyle(GUI.skin.textArea);
                    styleTextArea.normal.textColor = styleTextArea.focused.textColor = styleTextArea.hover.textColor = styleTextArea.active.textColor = Color.green;
                    styleTextArea.alignment = TextAnchor.MiddleCenter;
                    styleTextArea.stretchHeight = false;
                    styleTextArea.stretchWidth = false;
                    styleTextArea.fixedWidth = fixedwidth - margin;

                    styleButton = new GUIStyle(GUI.skin.button);
                    styleButton.normal.textColor = styleButton.focused.textColor = styleButton.hover.textColor = styleButton.active.textColor = Color.white;
                    styleButton.padding = new RectOffset(0, 0, 0, 0);

                    styleToggle = new GUIStyle(GUI.skin.toggle);

                    styleValue = new GUIStyle(GUI.skin.label);
                    styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.green;
                    styleValue.alignment = TextAnchor.MiddleCenter;

                    if (sizechange)
                    {
                        windowPos.yMax = windowPos.yMin + 20;
                        sizechange = false;
                    }

                    //print("@@@event type:" + Event.current.type.ToString());

                    GUILayoutOption[] opts = { GUILayout.Width(fixedwidth), GUILayout.ExpandHeight(true) };
                    windowPos = GUILayout.Window(this.ClassID, windowPos, OnWindow, ConditionalShow ? "Proximity" : "Proximity settings", opts);
                    windowPos.width = fixedwidth;

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
            if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.FLYING)
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
                distance = Mathf.Max(FlightGlobals.ActiveVessel.GetHeightFromTerrain(), FlightGlobals.ActiveVessel.heightFromTerrain);
            }
            return Convert.ToInt32(distance);
        }

        private bool RightConditionsToDraw()
        {
            //print("@@@RightConditionsToDraw");
            bool retval = true;
          
            if(!part.IsPrimary(FlightGlobals.ActiveVessel.parts, ClassID))
            {
                //print("@@@Not processing - multiple part, clsID = " + this.ClassID);
                return false; // this is such a hack
            }

            if (lostToStaging)
            {
                //print("@@@Not processing - lost to staging");
                prevConditionalShow = ConditionalShow = false;
                return false;
            }

            if (timeSinceDescending + gracePeriod < FlightGlobals.ActiveVessel.missionTime && FlightGlobals.ActiveVessel.verticalSpeed >= 0.1)
            {
                //print("@@@Not processing - not descended recently");
                retval = false;
            }
            else  if (timeSinceLanding + gracePeriod < FlightGlobals.ActiveVessel.missionTime && (Mathf.Abs((float)FlightGlobals.ActiveVessel.verticalSpeed) < 0.1))
            {
                //print("@@@Not processing - Landed");
                retval = false;
            }
            else if (TimeWarp.CurrentRateIndex != 0)
            {
                //print("@@@Not processing - Timewarp");
                retval = false;
            }
            else if (!(timeSinceLanding + gracePeriod > FlightGlobals.ActiveVessel.missionTime || FlightGlobals.ActiveVessel.situation == Vessel.Situations.FLYING ||
                    FlightGlobals.ActiveVessel.situation == Vessel.Situations.SUB_ORBITAL))
            {
                //print("@@@Not processing - Not flying or suborbital");
                retval = false;
            }
            else if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH)
            {
                //print("@@@Not processing - prelaunch");
                retval = false;
            }
            else if (altitude > ActivationHeight || altitude < 1)
            {
                //print("@@@Not processing - Not in alt range");
                retval = false;
            }
            else if (FlightDriver.Pause || PauseMenu.isOpen)
            {
                //print("@@@Not processing - paused");
                retval = false;
            }
            else if (parachutesOpen && deactivateOnParachute)
            {
                //print("@@@Not processing - parachute");
                retval = false;
            }
            else if (deactivateIfRover && FlightGlobals.ActiveVessel.vesselType == VesselType.Rover)
            {
                //print("@@@Not processing - rover");
                retval = false;
            }
            else if (timeSinceOnGround + gracePeriod > FlightGlobals.ActiveVessel.missionTime && FlightGlobals.ActiveVessel.verticalSpeed >= 0.1)
            {
                //print("@@@Not processing - just launched");
                retval = false;
            }
            else if (!SystemOn)
            { 
                //print("@@@Not processing - manually deactivated");
                retval = false;
            }

            prevConditionalShow = ConditionalShow;
            ConditionalShow = retval;
            return ConditionalShow || (UseToolbar && toolbarShowSettings);
         }

        private void OnWindow(int windowID)
        {
            //print("@@@OnWindow");
            try
            {
                DoProximityContent();
            }
            catch (Exception ex)
            {
                //print("@@@DoProximityContent exeption - " + ex.Message);
            }
            GUI.DragWindow();
        }

        private void DoProximityContent()
        {
            CheckLanded();

            if (Event.current.type == EventType.repaint && ConditionalShow)
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
                styleTextArea.normal.textColor = styleTextArea.focused.textColor = styleTextArea.hover.textColor = styleTextArea.active.textColor = GetColour(altitude);

                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                GUILayout.Label(warn, styleTextArea);
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
            if (!FlightGlobals.ActiveVessel.LandedOrSplashed)
            {
                timeSinceLanding = FlightGlobals.ActiveVessel.missionTime;
            }

            // allows us to switch off if ascending for > 5 seconds (ie launch)
            if (FlightGlobals.ActiveVessel.verticalSpeed < 0)
            {
                timeSinceDescending = FlightGlobals.ActiveVessel.missionTime;
            }

            if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.LANDED || FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH)
            {
                timeSinceOnGround = FlightGlobals.ActiveVessel.missionTime;
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
            else if (FlightGlobals.ActiveVessel.verticalSpeed >= 0)
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
                float danger = (float)FlightGlobals.ActiveVessel.verticalSpeed * -2.5f / (altitude + 4);
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

        private void ShowSettings()
        {
            //print("@@@ShowSettings");
            if (UseToolbar && toolbarShowSettings)
            {
                //print("@@@ShowSettings - showing");
                // Activation height
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                GUILayout.Label("Active below:");
                if (GUILayout.Button("-", styleButton, GUILayout.ExpandWidth(true)))
                {
                    ActivationHeight -= 500;
                    if (ActivationHeight < 500) ActivationHeight = 500;
                }

                GUILayout.Label(ActivationHeight.ToString() + " m", styleValue);
                
                if (GUILayout.Button("+", styleButton, GUILayout.ExpandWidth(true)))
                {
                    ActivationHeight += 500;
                    if (ActivationHeight > 10000) ActivationHeight = 10000;
                }

                GUILayout.EndHorizontal();

                // Visual type
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                string cap = "Visual: " + visualType[visualIndex];
                cap = cap.Replace("%", DSThreshold.ToString());
                if (GUILayout.Button(cap, styleButton, GUILayout.ExpandWidth(true)))
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
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                if (GUILayout.Button("Sound: " + beepType[beepIndex], styleButton, GUILayout.ExpandWidth(true)))
                {
                    beepIndex++;
                    if (beepIndex == beepType.Length)
                    {
                        beepIndex = 0;
                    }
                }
                GUILayout.EndHorizontal();

                // beep pitch
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                if (GUILayout.Button("Pitch: " + pitchType[pitchIndex], styleButton, GUILayout.ExpandWidth(true)))
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
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                GUILayout.Label("Volume:", GUILayout.ExpandWidth(false));
                volume = GUILayout.HorizontalSlider(volume, 0.01f, 1f, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();
                if (volume != oldvol)
                { 
                    obj.audio.volume = volume;
                }

                // beep length
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                GUILayout.Label("Beep length: ");
                if (GUILayout.Button("-", styleButton, GUILayout.ExpandWidth(true)))
                {
                    beepLength--;
                    if (beepLength < 1)
                    {
                        beepLength = 1;
                    }
                    MakeBeep();
                }

                GUILayout.Label(beepLength.ToString(), styleValue);
                
                if (GUILayout.Button("+", styleButton, GUILayout.ExpandWidth(true)))
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
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                deactivateOnParachute = GUILayout.Toggle(deactivateOnParachute, " Off if parachuting", styleToggle, null);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                deactivateIfRover = GUILayout.Toggle(deactivateIfRover, " Off if vessel is rover", styleToggle, null);
                GUILayout.EndHorizontal();

#if PERSONAL_VERSION
                // Configure ship for landing
                styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.green;
                styleValue.alignment = TextAnchor.MiddleCenter;

                bool oldauto = autoExtendLandingLegs;
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                autoExtendLandingLegs = GUILayout.Toggle(autoExtendLandingLegs, " Autodeploy landing legs", styleToggle, null);
                GUILayout.EndHorizontal();
                if (autoExtendLandingLegs != oldauto)
                {
                    sizechange = true;
                }

                oldauto = autoParachute;
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                autoParachute = GUILayout.Toggle(autoParachute, " Autodeploy parachute", styleToggle, null);
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

                styleButton.normal.textColor = styleButton.focused.textColor = styleButton.hover.textColor = styleButton.active.textColor = SystemOn ? Color.red: Color.green;
                styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.white;

                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                GUILayout.Label("Proximity ", styleValue);
                styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = SystemOn ? Color.green: Color.red;
                GUILayout.Label(SystemOn ? "ON ": "OFF ", styleValue);
                if (GUILayout.Button(SystemOn ? "Switch off": "Switch on", styleButton, GUILayout.ExpandWidth(true)))
                {
                    SystemOn = !SystemOn;
                    sizechange = true;

                    if (!useStockToolBar)
                    {
                        RefreshBlizzyButton();
                    }
                    else 
                    {
                        // here be twiddles
                        //RefreshStockButton();
                        //print("@@@ShowSettings, toggling on off");
                        StockToolbar stb = (StockToolbar)StockToolbar.FindObjectOfType(typeof(StockToolbar));
                        if (stb != null)
                        {
                            stb.RefreshButtonTexture();
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private void ThresholdHeight(GUIStyle style)
        {
            GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
            GUILayout.Label("Threshold: ");
            if (GUILayout.Button("-", styleButton, GUILayout.ExpandWidth(true)))
            {
                DSThreshold -= 50;
                if (DSThreshold < 50)
                {
                    DSThreshold = 50;
                }
            }

            GUILayout.Label(DSThreshold.ToString() + " m", style);

            if (GUILayout.Button("+", styleButton, GUILayout.ExpandWidth(true)))
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

        private string InitialiseWarnString()
        {
            return warnstring;
        }

        // make visual display string in Speed mode
        private string GetWarnStringSpeed()
        { 
            string warn = InitialiseWarnString();
            warn = warn.Insert(warnstring.Length - warnPos, "O");
            warn = warn.Insert(warnPos, "O");

            if (FlightGlobals.ActiveVessel.verticalSpeed <= 0) // falling
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

            string warn = InitialiseWarnString();
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
            if (FlightGlobals.ActiveVessel.verticalSpeed > 0)
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
            if (SystemOn && (FlightGlobals.ActiveVessel.situation == Vessel.Situations.SUB_ORBITAL || FlightGlobals.ActiveVessel.situation == Vessel.Situations.FLYING) &&
                isPowered && altitude > 0 && altitude < DSThreshold && FlightGlobals.ActiveVessel.verticalSpeed < -0.5)
            {
                if (autoExtendLandingLegs && !readyToLand)
                {
                    List<ModuleLandingLeg> lleg = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleLandingLeg>();
                    List<ModuleLandingLeg>.Enumerator en = lleg.GetEnumerator();
                    while (en.MoveNext())
                    {
                        en.Current.Invoke("LowerLeg", 0.1f);
                    }
                    en.Dispose();

                    List<ModuleLight> llight = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleLight>();
                    
                    List<ModuleLight>.Enumerator el = llight.GetEnumerator();
                    while (el.MoveNext())
                    {
                        el.Current.Invoke("LightsOn", 0.1f);
                    }
                    el.Dispose();

                    readyToLand = true;
                }

                if (autoParachute && !parachutesOpen && FlightGlobals.ActiveVessel.mainBody.atmosphere)
                { 
                    List<ModuleParachute> lpara = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleParachute>();
                    List<ModuleParachute>.Enumerator en = lpara.GetEnumerator();
                    while (en.MoveNext())
                    {
                        en.Current.Invoke("Deploy", 0.1f);
/*                     
                        List<BaseAction>.Enumerator act = en.Current.Actions.GetEnumerator();
                        while (act.MoveNext())
                        {
                            //print("@@@" + act.Current.name + ", " + act.Current.guiName);
                        }
 */ 
                    }
                    en.Dispose();
                    parachutesOpen = true;
                }
            }
            else if (FlightGlobals.ActiveVessel.verticalSpeed > 1)
            {
                readyToLand = false;
            }
        }
#endif

        private void CheckChutes()
        { 
            if (!parachutesOpen)
            {
                List<ModuleParachute> lpara = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleParachute>();
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
            if (OldSpeed < 0 && FlightGlobals.ActiveVessel.verticalSpeed > OldSpeed + 2)
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
                OldSpeed = FlightGlobals.ActiveVessel.verticalSpeed;
            }

            return warn;
        }

        private bool IsPowered()
        {
            double electricCharge = 0;

            foreach (Part p in ActiveVessel.parts)
            {
                foreach (PartResource pr in p.Resources)
                {
                    if (pr.resourceName.Equals("ElectricCharge") && pr.flowState)
                    {
                        electricCharge += pr.amount;
                        break;
                    }
                }
            }

            return electricCharge > 0.04;
        }

        private bool RefreshStockButton()
        {
            bool result = false;

            //print("@@@RefreshStockButton");

            if (useStockToolBar)
            {
                //print("@@@RefreshStockButton, using stock tb");
                StockToolbar stb = (StockToolbar)StockToolbar.FindObjectOfType(typeof(StockToolbar));
                if (stb != null)
                {
                    //print("@@@RefreshStockButton, got stock tb");
                    result = true; 
                    stb.ButtonNeeded = true; 
                    stb.CreateButton();
                    //print("@@@RefreshStockButton: stb.CreateButton() called, result " + stb.ButtonNeeded.ToString());
                    if (!stb.ButtonNeeded)
                    {
                        result = false;
                        windowPos.height = 20;
                        lostToStaging = true;
                        //print("@@@RefreshStockButton, set lostToStaging = true");
                    }
                }
            }

            return result;
        }

        public static bool IsRelevant()
        { 
            bool relevant = false;
            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                if (FlightGlobals.ActiveVessel != null)
                {
                    List<Proximity> bio = FlightGlobals.ActiveVessel.FindPartModulesImplementing<Proximity>();

                    if (bio != null && bio.Count > 0)
                    {
                        relevant = true;
                    }
                }
            }
            return relevant;
        }
    }
}
