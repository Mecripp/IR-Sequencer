﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using KSP.IO;
using IRSequencer.API;
using IRSequencer.Utility;
using IRSequencer.Core;
using IRSequencer.Module;

namespace IRSequencer.Gui
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class SequencerFlight : SequencerGUI
    {
        public override string AddonName { get { return this.name; } }
    }

    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class SequencerEditor : SequencerGUI
    {
        public override string AddonName { get { return this.name; } }
    }

    /// <summary>
    /// Class for creating and editing command queue for vessel's Infernal Robotic servos
    /// 
    /// So far relies on IR to parse all servos to ServoGroups
    /// </summary>
    public class SequencerGUI : MonoBehaviour
    {
        public virtual String AddonName { get; set; }

        public bool guiHidden = false;

        public bool GUIEnabled = false;
        public bool guiSequenceEditor = false;
        public bool guiCommandEditor = false;
        public bool guiStateSelector = false;
        private bool isReady = false;
        private bool firstUpdate = true;

        public bool isEnabled = true;
        internal static bool GUISetupDone = false;

        private ApplicationLauncherButton appLauncherButton;
        private string tooltipText = "";
        private string lastTooltipText = "";
        private float tooltipTime;
        private const float TOOLTIP_MAX_TIME = 8f;
        private const float TOOLTIP_DELAY = 1.5f;
        private static GUIStyle tooltipStyle;
        private static GUIStyle buttonStyle;
        private static GUIStyle nameStyle;
        private static GUIStyle dotStyle;
        private static GUIStyle playheadStyle;
        private static GUIStyle textFieldStyle;
        private static GUIStyle insertToggleStyle;
        private static GUIStyle hoverStyle;

        private static Color solidColor;
        private static Color opaqueColor;

        //Sequence Editor UI related
        private float currentDelay = 1.0f;
        private int currentMode = 0;
        private string currentGotoIndexString = "1";
        private int currentGotoIndex = 0;
        private int currentGotoCounter = -1;
        //index wher to insert new commands
        private int insertCommandIndex = -1;

        private string lastFocusedControlName = "";
        private string lastFocusedTextFieldValue = "";

        protected static Rect SequencerWindowPos;
        protected static Rect SequencerEditorWindowPos;
        protected static Rect SequencerCommandEditorWindowPos;
        protected static Rect SequencerStateSelectorWindowPos;
        protected static int SequencerWindowID;
        protected static int SequencerEditorWindowID;
        protected static int SequencerCommandEditorWindowID;
        protected static int SequencerStateSelectorWindowID;

        protected static Vector2 servoListScroll;
        protected static Vector2 actionListScroll;
        protected static Vector2 commandListScroll;

        protected static SequencerGUI SequencerInstance;

        public bool SequencerReady {get { return isReady;}}

        public static SequencerGUI Instance
        {
            get { return SequencerInstance; }
        }

        internal List<bool> openGroupsList;

        internal List<ModuleSequencer> sequencers;

        internal Sequence openSequence;
        internal BasicCommand selectedBasicCommand;
        internal int selectedBasicCommandIndex;
        internal SequencerState selectedState = null;
        internal Sequence selectedSequence = null;
        private List<BasicCommand> availableServoCommands;

        static SequencerGUI()
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            SequencerWindowID = UnityEngine.Random.Range(1000, 2000000) + assemblyName.GetHashCode();
            SequencerEditorWindowID = SequencerWindowID + 1;
            SequencerCommandEditorWindowID = UnityEngine.Random.Range(1000, 2000000) + assemblyName.GetHashCode();
            SequencerStateSelectorWindowID = SequencerCommandEditorWindowID + 1;
        }

        /// <summary>
        ///     Load the textures from files to memory
        ///     Initialise Styles
        /// </summary>
        private static void InitGUI()
        {
            if (!GUISetupDone)
            {
                TextureLoader.InitTextures();

                tooltipStyle = new GUIStyle
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal =
                    {
                        textColor = new Color32(207, 207, 207, 255),
                        background = TextureLoader.BgIcon
                    },
                    stretchHeight = true,
                    border = new RectOffset(3, 3, 3, 3),
                    padding = new RectOffset(4, 4, 6, 4),
                    alignment = TextAnchor.MiddleLeft
                };

                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.padding =  new RectOffset(2, 2, 2, 2);
                buttonStyle.alignment = TextAnchor.MiddleCenter;

                nameStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true,
                    clipping = TextClipping.Overflow
                };

                playheadStyle = new GUIStyle()
                {
                    padding = new RectOffset(0, 0, 0, 0),
                    border = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, -3, -3),
                    normal =
                    {
                        background = TextureLoader.PlayheadBG
                    },
                };

                hoverStyle = new GUIStyle (GUI.skin.label) 
                {
                    hover = 
                    {
                        textColor = Color.white,
                        background = TextureLoader.ToggleBGHover
                    },
                    padding = new RectOffset(1, 1, 1, 1),
                    border = new RectOffset (1, 1, 1, 1)
                };

                insertToggleStyle = new GUIStyle (GUI.skin.label) 
                {
                    onNormal = 
                    {
                        textColor = Color.white,
                        background = TextureLoader.ToggleBG
                    },
                    onActive = 
                    {
                        textColor = Color.white,
                        background = TextureLoader.ToggleBG
                    },
                    onHover = 
                    {
                        textColor = Color.white,
                        background = TextureLoader.ToggleBGHover
                    },
                    hover = 
                    {
                        textColor = Color.white,
                        background = TextureLoader.ToggleBGHover
                    },
                    active = 
                    {
                        textColor = Color.white,
                        background = TextureLoader.ToggleBG
                    },
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(1, 1, 1, 1),
                    border = new RectOffset (1, 1, 1, 1)
                };

                dotStyle = new GUIStyle(GUI.skin.label)
                {
                    richText = true,
                    alignment = TextAnchor.MiddleCenter
                };

                textFieldStyle = new GUIStyle(GUI.skin.textField);

                solidColor = new Color (1, 1, 1, 1);
                opaqueColor = new Color (1, 1, 1, 0.7f);

                GUISetupDone = true;
            }
        }

        private void AddAppLauncherButton()
        {
            if (appLauncherButton == null && ApplicationLauncher.Ready && ApplicationLauncher.Instance != null)
            {
                try
                {
                    var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                    TextureLoader.LoadImageFromFile(texture, "icon_seq_button.png");
                    Logger.Log(string.Format("[GUI Icon Loaded]"), Logger.Level.Debug);
                    if (ApplicationLauncher.Instance == null)
                    {
                        Logger.Log(string.Format("[GUI AddAppLauncher.Instance is null, PANIC!"), Logger.Level.Fatal);
                        return;
                    }
                    appLauncherButton = ApplicationLauncher.Instance.AddModApplication(delegate { GUIEnabled = true; },
                        delegate { GUIEnabled = false; }, null, null, null, null,
                        ApplicationLauncher.AppScenes.FLIGHT, texture);

                }
                catch (Exception ex)
                {
                    Logger.Log(string.Format("[GUI AddAppLauncherButton Exception, {0}", ex.Message), Logger.Level.Fatal);
                    DestroyAppLauncherButton ();
                    appLauncherButton = null;
                }
            }
        }

        public void Update()
        {
            if(firstUpdate)
            {
                try
                {
                    IRWrapper.InitWrapper();
                }
                catch (Exception e)
                {
                    Logger.Log("[Sequencer] Exception while initialising API " + e.Message, Logger.Level.Debug);
                }
                firstUpdate = false;
            }
        }


        private void OnVesselChange(Vessel v)
        {
            //if the scene was loaded on non-IR Vessel and then IR vessel became focused we might need to re-init the API
            if (!IRWrapper.APIReady)
                try
                {
                    IRWrapper.InitWrapper();
                }
                catch (Exception e)
                {
                    Logger.Log("[Sequencer] Exception while initialising API " + e.Message, Logger.Level.Debug);
                }

            sequencers.Clear();
            openGroupsList = null;
            guiSequenceEditor = false;
            availableServoCommands = null;
            openSequence = null;

            //find module SequencerStorage and force loading of sequences
            var modules = v.FindPartModulesImplementing<ModuleSequencer>();
            if (modules == null)
            {
                Logger.Log("Could not find any ModuleSequencer module", Logger.Level.Debug);
                return;
            }
            else
            {
                try
                {
                    if  (v == FlightGlobals.ActiveVessel && modules.Count > 0)
                    {
                        sequencers = modules;

                    }
                    else
                    {
                        Logger.Log("Could not find SequencerStorage module to load sequences from", Logger.Level.Debug);
                        return;
                    }
                        
                }
                catch (Exception e)
                {
                    Logger.Log("[IRSequencer] Exception in OnVesselChange: " + e.Message);
                }
            }

            Logger.Log("[IRSequencer] OnVesselChange finished, sequencers count=" + sequencers.Count);
        }

        private void OnVesselWasModified(Vessel v)
        {
            if (v == FlightGlobals.ActiveVessel)
            {
                OnVesselChange(v);
            }
        }

        private void OnEditorShipModified(ShipConstruct ship)
        {
            if(!IRWrapper.APIReady)
            {
                IRWrapper.InitWrapper();
            }
            guiSequenceEditor = false;
            availableServoCommands = null;
            openGroupsList = null;
            openSequence = null;
            sequencers.Clear ();

            var sequencerParts = ship.Parts.FindAll(p => p.FindModuleImplementing<ModuleSequencer>() != null);

            for (int i=0; i < sequencerParts.Count; i++)
            {
                var seqModule = sequencerParts[i].FindModuleImplementing<ModuleSequencer>();
                sequencers.Add(seqModule);
                seqModule.LoadData ();
            }

        }

        private void Awake()
        {
            LoadConfigXml();

            GameEvents.onShowUI.Add(OnShowUI);
            GameEvents.onHideUI.Add(OnHideUI);

            SequencerInstance = this;
            isReady = true;

            sequencers = new List<ModuleSequencer> ();
            sequencers.Clear ();

            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselWasModified.Add(OnVesselWasModified);
            GameEvents.onEditorShipModified.Add(OnEditorShipModified);
            GameEvents.onEditorRestart.Add(OnEditorRestart);

            GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequestedForAppLauncher);

            if (ApplicationLauncher.Ready && appLauncherButton == null && ApplicationLauncher.Instance != null)
            {
                AddAppLauncherButton();
            }

            Logger.Log("[Sequencer] Awake successful, Addon: " + AddonName, Logger.Level.Debug);
        }

        private void OnEditorRestart()
        {
            GUIEnabled = false;
            guiSequenceEditor = false;
            availableServoCommands = null;
            openGroupsList = null;
            openSequence = null;
            sequencers.Clear ();
        }

        void OnGameSceneLoadRequestedForAppLauncher(GameScenes SceneToLoad)
        {
            DestroyAppLauncherButton();
        }

        public void Start()
        {
            try
            {
                IRWrapper.InitWrapper();
            }
            catch (Exception e)
            {
                Logger.Log("[Sequencer] Exception while initialising API " + e.Message, Logger.Level.Debug);
            }

            Logger.Log("[Sequencer] OnStart successful", Logger.Level.Debug);
        }

        private void OnShowUI()
        {
            guiHidden = false;
        }

        private void OnHideUI()
        {
            guiHidden = true;
        }

        private void DestroyAppLauncherButton()
        {
            try
            {
                if (appLauncherButton != null && ApplicationLauncher.Instance != null)
                {
                    ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                    appLauncherButton = null;
                }
            }
            catch (Exception e)
            {
                Logger.Log("[Sequencer] Failed unregistering AppLauncher handlers," + e.Message);
            }
        }

        private void OnDestroy()
        {
            GameEvents.onShowUI.Remove(OnShowUI);
            GameEvents.onHideUI.Remove(OnHideUI);

            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
            GameEvents.onEditorShipModified.Remove(OnEditorShipModified);
            GameEvents.onEditorRestart.Remove(OnEditorRestart);

            SequencerGUI.Instance.isReady = false;
            SaveConfigXml();

            GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequestedForAppLauncher);
            DestroyAppLauncherButton();

            //consider unloading textures too in TextureLoader

            Logger.Log("[Sequencer] Destroy successful", Logger.Level.Debug);
        }

        /// <summary>
        /// Has to be called after any GUI element with tooltips.
        /// </summary>
        private void SetTooltipText()
        {
            if (Event.current.type == EventType.Repaint)
            {
                tooltipText = GUI.tooltip;
            }
        }

        /// <summary>
        /// Called in the end of OnGUI(), draws tooltip saved in tooltipText
        /// </summary>
        private void DrawTooltip()
        {
            Vector2 pos = Event.current.mousePosition;
            if (tooltipText != "" && tooltipTime < TOOLTIP_MAX_TIME)
            {
                
                var tooltip = new GUIContent(tooltipText);
                Vector2 size = tooltipStyle.CalcSize(tooltip);

                var tooltipPos = new Rect(pos.x - (size.x / 4), pos.y + 17, size.x, size.y);

                if (tooltipText != lastTooltipText)
                {
                    //reset timer
                    tooltipTime = 0f;
                }

                if (tooltipTime > TOOLTIP_DELAY)
                {
                    GUI.Label(tooltipPos, tooltip, tooltipStyle);
                    GUI.depth = 0;
                }

                tooltipTime += Time.deltaTime;
            }

            if (tooltipText != lastTooltipText) tooltipTime = 0f;
            lastTooltipText = tooltipText;
        }

        private void DrawSequencerStateSequences(ModuleSequencer module, SequencerState startState)
        {
            var allSequences = module.sequences.FindAll(s => s.startState == startState);
            if (allSequences == null || allSequences.Count == 0)
                return;

            GUILayout.BeginVertical();
            for (int i = 0; i < allSequences.Count; i++)
            {
                //list through all sequences
                var sq = allSequences[i];
                GUILayout.BeginHorizontal();

                string sequenceStatus = (sq.isActive) ? "<color=lime>■</color>" : sq.isFinished ? "<color=green>■</color>" : "<color=silver>■</color>";
                if (sq.IsPaused)
                    sequenceStatus = "<color=yellow>■</color>";

                if (sq.isLocked)
                    sequenceStatus = "<color=red>■</color>";

                GUI.color = solidColor;

                GUILayout.Label(sequenceStatus, dotStyle, GUILayout.Width(20), GUILayout.Height(22));

                sq.name = GUILayout.TextField(sq.name, textFieldStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22));

                if (sq.endState != null)
                {
                    //GUILayout.Label(" » " + sq.endState.stateName, nameStyle, GUILayout.Width(75), GUILayout.Height(22));
                    if(GUILayout.Button(" » " + sq.endState.stateName, buttonStyle, GUILayout.Width(75), GUILayout.Height(22)))
                    {
                        //open the state selector window. In new UI will be just a dropdown box
                        guiStateSelector = true; 
                        selectedState = sq.endState;
                        selectedSequence = sq;
                    }
                }

                sq.keyShortcut = GUILayout.TextField(sq.keyShortcut, textFieldStyle, GUILayout.Width(25), GUILayout.Height(22));

                bool playToggle = GUILayout.Toggle(sq.isActive, 
                    sq.isActive ? new GUIContent(TextureLoader.PauseIcon, "Pause") : new GUIContent(TextureLoader.PlayIcon, "Play"), 
                    buttonStyle, GUILayout.Width(22), GUILayout.Height(22));
                SetTooltipText ();

                if(playToggle && !sq.isLocked)
                {
                    if (playToggle != sq.isActive)
                    {
                        sq.Start(module.currentState);
                    }
                }
                else if (!sq.isLocked)
                {
                    if (playToggle != sq.isActive && !sq.isFinished)
                    {
                        sq.Pause();
                    }
                }

                if (GUILayout.Button(new GUIContent(TextureLoader.StopIcon, "Stop"), buttonStyle, GUILayout.Width(22), GUILayout.Height(22)))
                {
                    if (!sq.isLocked)
                        sq.Reset();
                }
                SetTooltipText ();

                sq.isLooped = GUILayout.Toggle(sq.isLooped, 
                    new GUIContent(sq.isLooped ? TextureLoader.LoopingIcon : TextureLoader.LoopIcon, "Loop"), 
                    buttonStyle, GUILayout.Width(22), GUILayout.Height(22));

                sq.autoStart = GUILayout.Toggle(sq.autoStart, new GUIContent(TextureLoader.LoopIcon, "Auto-Start"), 
                    buttonStyle, GUILayout.Width(22), GUILayout.Height(22));

                GUILayout.Space(4);

                bool sequenceEditToggle = (openSequence == sq) && guiSequenceEditor;

                bool toggleVal = GUILayout.Toggle(sequenceEditToggle, new GUIContent(TextureLoader.EditIcon, "Edit"), buttonStyle, GUILayout.Width(22), GUILayout.Height(22));
                SetTooltipText();

                if (sequenceEditToggle != toggleVal)
                {
                    if (guiSequenceEditor && Equals(openSequence, sq))
                        guiSequenceEditor = !guiSequenceEditor;
                    else
                    {
                        openSequence = sq;
                        if (!guiSequenceEditor)
                            guiSequenceEditor = true;
                    }
                }

                if (GUILayout.Button(new GUIContent(TextureLoader.CloneIcon, "Clone"), buttonStyle, GUILayout.Width(22), GUILayout.Height(22)))
                {
                    allSequences.Add(new Sequence(sq));
                }
                SetTooltipText ();

                GUILayout.Space(4);

                if (GUILayout.Button(new GUIContent(TextureLoader.TrashIcon, "Delete"), buttonStyle, GUILayout.Width(22), GUILayout.Height(22)))
                {
                    sq.Pause();
                    sq.Reset();
                    if (openSequence == sq)
                    {
                        guiSequenceEditor = false;
                        openSequence = null;
                    }
                    allSequences.RemoveAt(i);
                    module.sequences.Remove(sq);
                }
                SetTooltipText ();
                GUI.color = opaqueColor;
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void SequencerControlWindow(int windowID)
        {
            if (sequencers == null)
                return;

            GUI.color = opaqueColor;

            GUILayout.BeginVertical();

            for (int x=0; x < sequencers.Count; x++)
            {
                GUILayout.BeginHorizontal();
                GUI.color = solidColor;
                GUILayout.Label(sequencers[x].sequencerName, GUILayout.ExpandWidth(true), GUILayout.Height(22));

                if(GUILayout.Button("Add State", buttonStyle, GUILayout.Height(22)))
                {
                    var newState = new SequencerState ();
                    newState.stateName = "New State";
                    sequencers[x].states.Add(newState);
                }
                GUI.color = opaqueColor;
                GUILayout.EndHorizontal();

                GUILayout.BeginVertical();
                for (int s = 0; s < sequencers[x].states.Count; s++)
                {
                    var st = sequencers [x].states [s];

                    GUILayout.BeginHorizontal();
                    GUI.color = solidColor;

                    if(sequencers[x].currentState == st)
                    {
                        GUILayout.Label ("<color=lime>»</color>", dotStyle, GUILayout.Width (20), GUILayout.Height (22));
                    }
                    else
                    {
                        GUILayout.Label ("<color=silver>-</color>", dotStyle, GUILayout.Width (20), GUILayout.Height (22));
                    }

                    if (selectedState == st)
                    {
                        st.stateName = GUILayout.TextField(st.stateName, textFieldStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22));
                    }
                    else
                    {
                        GUILayout.Label(st.stateName, GUILayout.ExpandWidth(true), GUILayout.Height(22));
                    }

                    bool stateEditToggle = (selectedState == st);

                    bool toggleVal = GUILayout.Toggle(stateEditToggle, new GUIContent(TextureLoader.EditIcon, "Edit State"), buttonStyle, GUILayout.Width(22), GUILayout.Height(22));
                    SetTooltipText();

                    if (stateEditToggle != toggleVal)
                    {
                        selectedState = Equals (selectedState, st) ? null : st;
                    }

                    if(GUILayout.Button("Add Sequence", buttonStyle, GUILayout.Height(22)))
                    {
                        var newSeq = new Sequence ();
                        newSeq.startState = st;
                        newSeq.endState = st;
                        sequencers[x].sequences.Add(newSeq);
                    }

                    if (sequencers [x].states.Count > 1) 
                    {
                        if (GUILayout.Button(new GUIContent(TextureLoader.TrashIcon, "Delete State. \n Will delete all Sequences in it."), buttonStyle, GUILayout.Width(22), GUILayout.Height(22))) 
                        {
                            //remove all affected sequences
                            sequencers [x].sequences.RemoveAll (seq => (seq.startState == st || seq.endState == st));
                            sequencers [x].states.Remove (st);
                            if(sequencers[x].currentState == st)
                            {
                                //reset to the first state
                                sequencers [x].currentState = sequencers [x].states [0];
                            }
                        }
                    }
                    GUI.color = opaqueColor;
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space (30);
                    DrawSequencerStateSequences (sequencers [x], st);
                    GUILayout.EndHorizontal();

                }
                GUILayout.EndVertical();

            }

            GUILayout.EndVertical();

            GUI.color = opaqueColor;

            GUI.DragWindow();
        }

        /// <summary>
        /// Draws the text field and returns its value
        /// Uses global variables lastFocusedControlName and lastFocusedTextFieldValue
        /// </summary>
        /// <returns>Entered value</returns>
        /// <param name="controlName">Control name.</param>
        /// <param name="value">Value.</param>
        /// <param name="format">Format.</param>
        /// <param name="style">Style.</param>
        /// <param name="width">Width.</param>
        /// <param name="height">Height.</param>
        private string DrawTextField(string controlName, float value, string format, GUIStyle style, GUILayoutOption width, GUILayoutOption height)
        {
            string focusedControlName = GUI.GetNameOfFocusedControl ();

            if (controlName == focusedControlName 
                && lastFocusedTextFieldValue == "")
            {
                lastFocusedTextFieldValue = string.Format (format, value);
            }

            string tmp = (controlName == focusedControlName) 
                ? lastFocusedTextFieldValue 
                : string.Format (format, value);

            GUI.SetNextControlName(controlName);
            tmp = GUILayout.TextField(tmp, style, width, height);

            if (controlName == focusedControlName 
                && focusedControlName == lastFocusedControlName)
                lastFocusedTextFieldValue = tmp;

            return tmp;
        }


        private void SequencerEditorWindow(int windowID)
        {
            if (openSequence == null)
                return;

            if (IRWrapper.IRController.ServoGroups == null)
                return;

            GUI.color = opaqueColor;

            string tmpString;
            float tmpValue;

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayoutOption maxHeight = GUILayout.MaxHeight(Screen.height * 0.5f);

            GUILayout.BeginVertical(GUILayout.Width(270));

            //draw buttons here
            GUILayout.BeginHorizontal();
            var modes = new String[] {"Servos", "ActionGroups"};
            GUI.color = solidColor;
            currentMode = GUILayout.Toolbar (currentMode, modes, buttonStyle,  GUILayout.Height(22));
            GUI.color = opaqueColor;
            GUILayout.EndHorizontal();

            var allServos = new List<IRWrapper.IServo>();

            if (currentMode == 0) 
            {
                foreach (IRWrapper.IControlGroup g in IRWrapper.IRController.ServoGroups) 
                {
                    allServos.AddRange (g.Servos);
                }

                if (availableServoCommands == null) 
                {
                    availableServoCommands = new List<BasicCommand> ();    
                }

                if (allServos.Count != availableServoCommands.Count) 
                {
                    availableServoCommands.Clear ();
                    //rebuild the list of available commands
                    foreach (IRWrapper.IServo s in allServos) 
                    {
                        var bc = new BasicCommand (s, s.Position, 1f);
                        availableServoCommands.Add (bc);
                    }
                }

                if (openGroupsList == null)
                {
                    openGroupsList = new List<bool> ();
                }
                if(openGroupsList.Count != IRWrapper.IRController.ServoGroups.Count)
                {
                    openGroupsList.Clear ();
                    for (int i=0; i<IRWrapper.IRController.ServoGroups.Count; i++)
                    {
                        openGroupsList.Add (IRWrapper.IRController.ServoGroups [i].Expanded);
                    }
                }

                servoListScroll = GUILayout.BeginScrollView (servoListScroll, false, false, maxHeight);
                for (int i = 0; i < IRWrapper.IRController.ServoGroups.Count; i++) 
                {
                    IRWrapper.IControlGroup g = IRWrapper.IRController.ServoGroups [i];

                    //if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ActiveVessel != g.Vessel)
                    //    continue;
                    
                    if (g.Servos.Any ()) 
                    {
                        GUILayout.BeginHorizontal ();

                        GUI.color = solidColor;
                        if (openGroupsList [i]) 
                        {
                            openGroupsList [i] = !GUILayout.Button (TextureLoader.CollapseIcon, buttonStyle, GUILayout.Width (20), GUILayout.Height (22));
                        } 
                        else 
                        {
                            openGroupsList [i] = GUILayout.Button (TextureLoader.ExpandIcon, buttonStyle, GUILayout.Width (20), GUILayout.Height (22));
                        }
                        
                        nameStyle.fontStyle = FontStyle.Bold;
                        GUILayout.Label (g.Name, nameStyle, GUILayout.ExpandWidth (true), GUILayout.Height (22));
                        nameStyle.fontStyle = FontStyle.Normal;
                        GUI.color = opaqueColor;
                        GUILayout.EndHorizontal ();

                        if (openGroupsList [i]) 
                        {
                            GUILayout.BeginHorizontal (GUILayout.Height (5));
                            GUILayout.EndHorizontal ();

                            foreach (IRWrapper.IServo servo in g.Servos) 
                            {
                                GUILayout.BeginHorizontal (hoverStyle);
                                GUI.color = solidColor;

                                var avCommand = availableServoCommands.FirstOrDefault(t => t.servo.Equals(servo));

                                if (avCommand == null) 
                                {
                                    Logger.Log ("[Sequencer] Cannot find matching command for servo " + servo.Name, Logger.Level.Debug);
                                    return;
                                }
                               
                                if (GUILayout.Button ("Add", buttonStyle, GUILayout.Width (30), GUILayout.Height (22))) 
                                {
                                    openSequence.Pause ();
                                    openSequence.Reset ();
                                    if (insertCommandIndex + 1 == openSequence.commands.Count)
                                    {
                                        openSequence.commands.Add (new BasicCommand (avCommand));
                                        insertCommandIndex++;
                                    }
                                    else
                                    {
                                        openSequence.commands.Insert (insertCommandIndex + 1, new BasicCommand (avCommand));
                                    }
                                }
                                
                                GUILayout.Label (servo.Name, nameStyle, GUILayout.ExpandWidth (true), GUILayout.Height (22));

                                Rect last = GUILayoutUtility.GetLastRect();
                                Vector2 pos = Event.current.mousePosition;
                                bool highlight = last.Contains(pos);
                                servo.Highlight = highlight;

                                var e = Event.current;
                                if (e.isMouse && e.clickCount == 2 && last.Contains(e.mousePosition))
                                {
                                    avCommand.position = servo.Position;
                                }

                                string focusedControlName = GUI.GetNameOfFocusedControl ();
                                string thisControlName = "SequencerPosition " + servo.UID;

                                tmpString = DrawTextField (thisControlName, avCommand.position, "{0:#0.0#}", 
                                    textFieldStyle, GUILayout.Width (40), GUILayout.Height (22));

                                var valueChanged = (thisControlName == focusedControlName && 
                                    (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter));

                                if (float.TryParse (tmpString, out tmpValue) && valueChanged) 
                                {
                                    avCommand.position = Mathf.Clamp(tmpValue, avCommand.servo.MinPosition, avCommand.servo.MaxPosition);
                                    lastFocusedTextFieldValue = "";
                                }

                                GUILayout.Label ("@", nameStyle, GUILayout.Height (22));

                                thisControlName = "SequencerSpeed " + servo.UID;

                                tmpString = DrawTextField (thisControlName, avCommand.speedMultiplier, "{0:#0.0#}", 
                                    textFieldStyle, GUILayout.Width (30), GUILayout.Height (22));

                                valueChanged = (thisControlName == focusedControlName && 
                                    (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter));
                                
                                if (float.TryParse (tmpString, out tmpValue) && valueChanged)
                                {
                                    avCommand.speedMultiplier = Mathf.Clamp (tmpValue, 0.05f, 1000f);
                                    lastFocusedTextFieldValue = "";
                                }
                                GUI.color = opaqueColor;
                                GUILayout.EndHorizontal ();
                            }

                            GUILayout.BeginHorizontal (GUILayout.Height (5));
                            GUILayout.EndHorizontal ();
                        }
                    
                    }
                }
                GUILayout.EndScrollView ();
            }
            else 
            {
                //here goes actiongroup stuff
                actionListScroll = GUILayout.BeginScrollView (actionListScroll, false, false, maxHeight);

                //first list all the stock AGs
                foreach (KSPActionGroup a in Enum.GetValues(typeof(KSPActionGroup)).Cast<KSPActionGroup>())
                {
                    if (a == KSPActionGroup.None)
                        continue;
                    
                    GUILayout.BeginHorizontal ();
                    GUI.color = solidColor;
                    if (GUILayout.Button ("Add Toggle", buttonStyle, GUILayout.Width (80), GUILayout.Height (22))) 
                    {
                        openSequence.Pause ();
                        openSequence.Reset ();
                        
                        var newCommand = new BasicCommand (a);

                        if (insertCommandIndex + 1 == openSequence.commands.Count)
                        {
                            openSequence.commands.Add (newCommand);
                            insertCommandIndex++;
                        }
                        else
                        {
                            openSequence.commands.Insert (insertCommandIndex + 1, newCommand);
                        }
                    }
                    if (GUILayout.Button("Wait For", buttonStyle, GUILayout.Width(60), GUILayout.Height(22)))
                    {
                        openSequence.Pause();
                        openSequence.Reset();

                        var newCommand = new BasicCommand(a);
                        newCommand.wait = true;

                        if (insertCommandIndex + 1 == openSequence.commands.Count)
                        {
                            openSequence.commands.Add(newCommand);
                            insertCommandIndex++;
                        }
                        else
                        {
                            openSequence.commands.Insert(insertCommandIndex + 1, newCommand);
                        }
                    }
                    GUILayout.Label (a.ToString(), GUILayout.ExpandWidth (true), GUILayout.Height (22));
                    GUI.color = opaqueColor;
                    GUILayout.EndHorizontal ();
                }

                //now if AGX is installed, list all the groups
                if(ActionGroupsExtendedAPI.Instance != null && ActionGroupsExtendedAPI.Instance.Installed())
                {
                    Dictionary<int, string> extendedGroups;

                    if(HighLogic.LoadedSceneIsFlight)
                    {
                        extendedGroups = ActionGroupsExtendedAPI.Instance.GetAssignedGroups(FlightGlobals.ActiveVessel);
                    }
                    else
                    {
                        extendedGroups = ActionGroupsExtendedAPI.Instance.GetAssignedGroups();
                    }
                    foreach (var pair in extendedGroups)
                    {
                        GUILayout.BeginHorizontal();
                        GUI.color = solidColor;
                        if (GUILayout.Button("Add Toggle", buttonStyle, GUILayout.Width(80), GUILayout.Height(22)))
                        {
                            openSequence.Pause();
                            openSequence.Reset();

                            var newCommand = new BasicCommand(pair.Key);

                            if (insertCommandIndex + 1 == openSequence.commands.Count)
                            {
                                openSequence.commands.Add(newCommand);
                                insertCommandIndex++;
                            }
                            else
                            {
                                openSequence.commands.Insert(insertCommandIndex + 1, newCommand);
                            }
                        }
                        if (GUILayout.Button("Wait For", buttonStyle, GUILayout.Width(60), GUILayout.Height(22)))
                        {
                            openSequence.Pause();
                            openSequence.Reset();

                            var newCommand = new BasicCommand(pair.Key);
                            newCommand.wait = true;

                            if (insertCommandIndex + 1 == openSequence.commands.Count)
                            {
                                openSequence.commands.Add(newCommand);
                                insertCommandIndex++;
                            }
                            else
                            {
                                openSequence.commands.Insert(insertCommandIndex + 1, newCommand);
                            }
                        }
                        GUILayout.Label(pair.Value, GUILayout.ExpandWidth(true), GUILayout.Height(22));
                        GUI.color = opaqueColor;
                        GUILayout.EndHorizontal();
                    }

                }
                GUILayout.EndScrollView ();
            }

            GUILayout.BeginHorizontal(GUILayout.Height(5));
            GUILayout.Label("", GUILayout.ExpandWidth(true), GUILayout.Height(5));
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GUI.skin.scrollView);
            GUILayout.BeginHorizontal();
            GUI.color = solidColor;
            if (GUILayout.Button("Add", buttonStyle, GUILayout.Width(30), GUILayout.Height(22)))
            {
                openSequence.Pause ();
                openSequence.Reset ();
                
                var newCommand = new BasicCommand(true, currentDelay);
                if (insertCommandIndex + 1 == openSequence.commands.Count)
                {
                    openSequence.commands.Add (newCommand);
                    insertCommandIndex++;
                }
                else
                {
                    openSequence.commands.Insert (insertCommandIndex + 1, newCommand);
                }
            }

            GUILayout.Label("Delay for ", nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22));
            tmpString = GUILayout.TextField(string.Format("{0:#0.0#}", currentDelay), textFieldStyle, GUILayout.Width(40), GUILayout.Height(22));
            if (float.TryParse(tmpString, out tmpValue))
            {
                currentDelay = Mathf.Clamp(tmpValue, 0f, 600f);
            }
            GUILayout.Label("s ", nameStyle, GUILayout.Width(18), GUILayout.Height(22));
            
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Add", buttonStyle, GUILayout.Width(30), GUILayout.Height(22)))
            {
                openSequence.Pause ();
                openSequence.Reset ();
                
                var newCommand = new BasicCommand(true);
                if (insertCommandIndex + 1 == openSequence.commands.Count)
                {
                    openSequence.commands.Add (newCommand);
                    insertCommandIndex++;
                }
                else
                {
                    openSequence.commands.Insert (insertCommandIndex + 1, newCommand);
                }
            }
            GUILayout.Label("Wait for Moves", nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Add", buttonStyle, GUILayout.Width(30), GUILayout.Height(22)))
            {
                openSequence.Pause ();
                openSequence.Reset ();
                
                var newCommand = new BasicCommand(currentGotoIndex, currentGotoCounter);
                if (insertCommandIndex + 1 == openSequence.commands.Count)
                {
                    openSequence.commands.Add (newCommand);
                    insertCommandIndex++;
                }
                else
                {
                    openSequence.commands.Insert (insertCommandIndex + 1, newCommand);
                }
            }

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Go To Command #", nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22));
            if (GUILayout.Button ("-", buttonStyle, GUILayout.Width (18), GUILayout.Height (22))) 
            {
                currentGotoIndex = Math.Max (currentGotoIndex - 1, 0);
                currentGotoIndexString = (currentGotoIndex+1).ToString ();
            }
            currentGotoIndexString = GUILayout.TextField(string.Format("{0:#0}", currentGotoIndexString), textFieldStyle, GUILayout.Width(25), GUILayout.Height(22));

            if (float.TryParse(currentGotoIndexString, out tmpValue))
            {
                currentGotoIndex = (int)Mathf.Clamp(tmpValue-1, 0f, openSequence.commands.Count-1);
            }

            if (GUILayout.Button ("+", buttonStyle, GUILayout.Width (18), GUILayout.Height (22))) 
            {
                currentGotoIndex = Math.Max (Math.Min (currentGotoIndex + 1, openSequence.commands.Count-1), 0);
                currentGotoIndexString = (currentGotoIndex+1).ToString ();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Repeat (-1 for loop)", nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22));
            if (GUILayout.Button ("-", buttonStyle, GUILayout.Width (18), GUILayout.Height (22))) 
            {
                currentGotoCounter = Math.Max (currentGotoCounter - 1, -1);
            }

            tmpString = GUILayout.TextField(string.Format("{0:#0}", currentGotoCounter), textFieldStyle, GUILayout.Width(25), GUILayout.Height(22));
            if (float.TryParse(tmpString, out tmpValue))
            {
                currentGotoCounter = (int)Math.Max(tmpValue, -1);
            }
            if (GUILayout.Button ("+", buttonStyle, GUILayout.Width (18), GUILayout.Height (22))) 
            {
                currentGotoCounter = Math.Max (currentGotoCounter + 1, -1);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            
            GUILayout.EndHorizontal();
            GUI.color = opaqueColor;
            GUILayout.EndVertical();

            GUILayout.EndVertical ();
            GUILayout.BeginVertical();

            commandListScroll = GUILayout.BeginScrollView(commandListScroll, false, false, maxHeight);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUI.color = solidColor;
            GUILayout.Label("Commands:", nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22));
            GUI.color = opaqueColor;
            GUILayout.EndHorizontal();

            //set pointer to last command
            if (insertCommandIndex < -1 || insertCommandIndex >= openSequence.commands.Count)
                insertCommandIndex = openSequence.commands.Count-1;

            if (insertCommandIndex == -1) 
            {
                playheadStyle.normal.background = TextureLoader.ToggleBGHover;
                GUILayout.BeginHorizontal (GUILayout.Height(1));
                GUILayout.Label ("", playheadStyle, GUILayout.Height(1));
                GUILayout.EndHorizontal ();
            } 
            else 
            {
                playheadStyle.normal.background = null;
            }

            //now begin listing commands in sequence
            for (int i = 0; i < openSequence.commands.Count; i++ )
            {
                BasicCommand bc = openSequence.commands[i];

                if (openSequence.lastCommandIndex == i)
                {
                    playheadStyle.normal.background = openSequence.commands[i].isActive ? TextureLoader.PlayheadBG : TextureLoader.PlayheadBGPaused;
                    GUILayout.BeginHorizontal(playheadStyle);
                }
                else
                {
                    playheadStyle.normal.background = null;
                    GUILayout.BeginHorizontal(playheadStyle);
                }

                GUI.color = solidColor;
                string commandStatus = (bc.isActive || openSequence.lastCommandIndex == i) ? "<color=lime>■</color>" : bc.isFinished ? "<color=green>■</color>" : "<color=silver>■</color>";
                if (openSequence.lastCommandIndex == i && !bc.isActive)
                    commandStatus = "<color=yellow>■</color>";
                GUILayout.Label(commandStatus, dotStyle, GUILayout.Width(20), GUILayout.Height(22));

                //GUILayout.Label((i+1).ToString() + ":", dotStyle, GUILayout.Width(25), GUILayout.Height(22));

                if(GUILayout.Toggle((i == insertCommandIndex), new GUIContent((i+1).ToString() + ":", "Insert After"), insertToggleStyle, GUILayout.Width(25), GUILayout.Height(22)))
                {
                    insertCommandIndex = i;
                }
                else if (insertCommandIndex==i)
                {
                    insertCommandIndex = -1;
                }

                var labelText = "";
                if (bc.wait)
                {
                    if (bc.waitTime > 0f)
                    {
                        if (bc.isActive)
                            labelText = "Delaying for " + Math.Round(bc.timeStarted + bc.waitTime - UnityEngine.Time.time, 2) + "s";
                        else
                            labelText = "Delay for " + Math.Round(bc.waitTime, 2) + "s";
                    }
                    else if (bc.gotoIndex != -1)
                    {
                        labelText = "Go To Command # " + (bc.gotoIndex + 1).ToString();
                        if (bc.gotoCounter != -1)
                        {
                            labelText += ", " + bc.gotoCounter + " more times.";
                        }
                    }
                    else if (bc.ag != KSPActionGroup.None || bc.agX > -1)
                    {
                        labelText = (bc.isActive ? "Waiting" : "Wait") + " for AG: ";
                        if (ActionGroupsExtendedAPI.Instance != null && ActionGroupsExtendedAPI.Instance.Installed() && bc.agX > -1)
                            labelText += ActionGroupsExtendedAPI.Instance.GetGroupName(bc.agX);
                        else
                            labelText += bc.ag.ToString();
                    }
                    else
                        labelText = (bc.isActive ? "Waiting" : "Wait") + " for Moves";
                }
                else if (bc.servo != null) 
                    labelText = bc.servo.Name + " to " + Math.Round(bc.position, 2).ToString() + " at " + Math.Round(bc.speedMultiplier, 2).ToString() + "x";
                else if (bc.ag != KSPActionGroup.None)
                {
                    labelText = "Toggle ActionGroup: " + bc.ag.ToString ();
                }
                else if (ActionGroupsExtendedAPI.Instance != null && ActionGroupsExtendedAPI.Instance.Installed() && bc.agX > -1)
                {
                    labelText = "Toggle ActionGroup: " + ActionGroupsExtendedAPI.Instance.GetGroupName(bc.agX);
                }

                GUILayout.Label(labelText, nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22));
                if (i > 0)
                {
                    if (GUILayout.Button(TextureLoader.UpIcon, buttonStyle, GUILayout.Width(20), GUILayout.Height(22)))
                    {
                        openSequence.Pause();
                        var tmp = openSequence.commands[i - 1];
                        openSequence.commands[i - 1] = bc;
                        openSequence.commands[i] = tmp;
                        openSequence.Reset();
                    }
                }
                else
                    GUILayout.Space(24);

                if (i < openSequence.commands.Count - 1)
                {
                    if (GUILayout.Button(TextureLoader.DownIcon, buttonStyle, GUILayout.Width(20), GUILayout.Height(22)))
                    {
                        openSequence.Pause();
                        var tmp = openSequence.commands[i + 1];
                        openSequence.commands[i + 1] = bc;
                        openSequence.commands[i] = tmp;
                        openSequence.Reset();
                    }
                }
                else
                    GUILayout.Space(24);

                //insert edit button/toggle here

                if (GUILayout.Button(new GUIContent(TextureLoader.EditIcon, "Edit"), buttonStyle, GUILayout.Width(20), GUILayout.Height(22)))
                {
                    openSequence.Pause();
                    //code for editing command
                    //open separate window with selected basic command
                    selectedBasicCommandIndex = i;
                    selectedBasicCommand = bc;
                    guiCommandEditor = true;
                    SequencerCommandEditorWindowPos = new Rect(Input.mousePosition.x - 100, Screen.height - Input.mousePosition.y + 17, 10, 10);
                    openSequence.Reset();
                }
                if (GUILayout.Button(TextureLoader.TrashIcon, buttonStyle, GUILayout.Width(20), GUILayout.Height(22)))
                {
                    openSequence.Pause();
                    openSequence.commands.RemoveAt(i);
                    openSequence.Reset();
                }
                GUI.color = opaqueColor;
                GUILayout.EndHorizontal();

                if (i == insertCommandIndex) 
                {
                    playheadStyle.normal.background = TextureLoader.ToggleBGHover;
                    GUILayout.BeginHorizontal (GUILayout.Height(1));
                    GUILayout.Label ("", playheadStyle, GUILayout.Height(1));
                    GUILayout.EndHorizontal ();
                } 
                else 
                {
                    playheadStyle.normal.background = null;
                }
                    
            }
            
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();

            GUI.color = solidColor;

            bool playToggle = GUILayout.Toggle(openSequence.isActive,
                   openSequence.isActive ? "Pause" : "Play",
                   buttonStyle, GUILayout.Width(100), GUILayout.Height(22));
            SetTooltipText();

            if (playToggle && !openSequence.isLocked)
            {
                if (playToggle != openSequence.isActive)
                {
                    var currentModule = sequencers.Find (s => s.sequences.Contains (openSequence));
                    if (currentModule != null)
                    {
                        openSequence.Start(currentModule.currentState);
                    }
                    else
                    {
                        Logger.Log ("Could not find sequencer for openSequence, panic!", Logger.Level.Warning);
                    }
                }
            }
            else if (!openSequence.isLocked)
            {
                if (playToggle != openSequence.isActive && !openSequence.isFinished)
                {
                    openSequence.Pause();
                }
            }

            if (GUILayout.Button("Step", buttonStyle, GUILayout.Width(100), GUILayout.Height(22)))
            {
                if (!openSequence.isLocked)
                {
                    openSequence.Step();
                }
            }
            
            if (GUILayout.Button("Stop", buttonStyle, GUILayout.Width(100), GUILayout.Height(22)))
            {
                if (!openSequence.isLocked)
                {
                    openSequence.Pause();
                    openSequence.Reset();
                }
            }

            openSequence.isLooped = GUILayout.Toggle(openSequence.isLooped, "Looping", buttonStyle, GUILayout.Height(22));
            
            GUI.color = opaqueColor;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void SequencerCommandEditorWindow(int windowID)
        {

            if (openSequence == null || selectedBasicCommand == null)
                return;

            var bc = selectedBasicCommand;

            GUI.color = solidColor;
            string tmpString;
            float tmpValue;

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();

            if (bc.waitTime > 0f) 
            {
                var tempDelay = bc.waitTime;
                GUILayout.Label ("Delay for ", nameStyle, GUILayout.ExpandWidth (true), GUILayout.Height (22));
                tmpString = GUILayout.TextField (string.Format ("{0:#0.0#}", tempDelay), textFieldStyle, GUILayout.Width (40), GUILayout.Height (22));
                if (float.TryParse (tmpString, out tmpValue)) 
                {
                    tempDelay = Mathf.Clamp (tmpValue, 0.001f, 600f);
                    bc.waitTime = tempDelay;
                }
                GUILayout.Label ("s ", nameStyle, GUILayout.Width (18), GUILayout.Height (22));

            }

            if(bc.gotoIndex != -1)
            {
                var tempGotoCounter = bc.gotoCounter;
                var tempGotoIndex = bc.gotoIndex;
                var tempGotoIndexString = (tempGotoIndex+1).ToString ();

                GUILayout.BeginVertical();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Go To Command #", nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22));
                if (GUILayout.Button ("-", buttonStyle, GUILayout.Width (18), GUILayout.Height (22))) 
                {
                    tempGotoIndex = Math.Max (tempGotoIndex - 1, 0);
                    bc.gotoIndex = tempGotoIndex;
                    tempGotoIndexString = (tempGotoIndex+1).ToString ();
                }
                tempGotoIndexString = GUILayout.TextField(string.Format("{0:#0}", tempGotoIndexString), textFieldStyle, GUILayout.Width(25), GUILayout.Height(22));

                if (float.TryParse(tempGotoIndexString, out tmpValue))
                {
                    tempGotoIndex = (int)Mathf.Clamp(tmpValue-1, 0f, openSequence.commands.Count-1);
                    bc.gotoIndex = tempGotoIndex;
                }

                if (GUILayout.Button ("+", buttonStyle, GUILayout.Width (18), GUILayout.Height (22))) 
                {
                    tempGotoIndex = Math.Max (Math.Min (tempGotoIndex + 1, openSequence.commands.Count-1), 0);
                    bc.gotoIndex = tempGotoIndex;
                    tempGotoIndexString = (tempGotoIndex+1).ToString ();
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Repeat (-1 for loop)", nameStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22));
                if (GUILayout.Button ("-", buttonStyle, GUILayout.Width (18), GUILayout.Height (22))) 
                {
                    tempGotoCounter = Math.Max (tempGotoCounter - 1, -1);
                    bc.gotoCounter = tempGotoCounter;
                    bc.gotoCommandCounter = tempGotoCounter;
                }

                tmpString = GUILayout.TextField(string.Format("{0:#0}", tempGotoCounter), textFieldStyle, GUILayout.Width(25), GUILayout.Height(22));
                if (float.TryParse(tmpString, out tmpValue))
                {
                    tempGotoCounter = (int)Math.Max(tmpValue, -1);
                    bc.gotoCounter = tempGotoCounter;
                    bc.gotoCommandCounter = tempGotoCounter;
                }
                if (GUILayout.Button ("+", buttonStyle, GUILayout.Width (18), GUILayout.Height (22))) 
                {
                    tempGotoCounter = Math.Max (tempGotoCounter + 1, -1);
                    bc.gotoCounter = tempGotoCounter;
                    bc.gotoCommandCounter = tempGotoCounter;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            if(bc.servo != null)
            {

                GUILayout.Label (bc.servo.Name, nameStyle, GUILayout.ExpandWidth (true), GUILayout.Height (22));

                Rect last = GUILayoutUtility.GetLastRect();
                Vector2 pos = Event.current.mousePosition;
                bool highlight = last.Contains(pos);
                bc.servo.Highlight = highlight;

                var e = Event.current;
                if (e.isMouse && e.clickCount == 2 && last.Contains(e.mousePosition))
                {
                    bc.position = bc.servo.Position;
                }

                string focusedControlName = GUI.GetNameOfFocusedControl ();
                string thisControlName = "SequencerPositionCommand " + bc.servo.UID;

                tmpString = DrawTextField (thisControlName, bc.position, "{0:#0.0#}", 
                    textFieldStyle, GUILayout.Width (40), GUILayout.Height (22));

                var valueChanged = (thisControlName == focusedControlName && 
                    (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter));

                if (float.TryParse (tmpString, out tmpValue) && valueChanged) 
                {
                    bc.position = Mathf.Clamp(tmpValue, bc.servo.MinPosition, bc.servo.MaxPosition);
                    lastFocusedTextFieldValue = "";
                }

                GUILayout.Label ("@", nameStyle, GUILayout.Height (22));

                thisControlName = "SequencerSpeedCommand " + bc.servo.UID;

                tmpString = DrawTextField (thisControlName, bc.speedMultiplier, "{0:#0.0#}", 
                    textFieldStyle, GUILayout.Width (30), GUILayout.Height (22));

                valueChanged = (thisControlName == focusedControlName && 
                    (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter));

                if (float.TryParse (tmpString, out tmpValue) && valueChanged)
                {
                    bc.speedMultiplier = Mathf.Clamp (tmpValue, 0.05f, 1000f);
                    lastFocusedTextFieldValue = "";
                }

            }

            if (GUILayout.Button("Done", buttonStyle, GUILayout.Width(40), GUILayout.Height(22)))
            {
                openSequence.commands [selectedBasicCommandIndex]= bc;
                guiCommandEditor = false;
            }

            GUI.color = opaqueColor;
            GUILayout.EndHorizontal ();
            GUILayout.EndVertical();

            GUI.DragWindow();

        }

        private void SequencerStateSelectWindow(int windowID)
        {

            if (selectedState == null || selectedSequence == null || sequencers == null)
                return;
            
            var module = sequencers.Find (s => s.states.Contains (selectedState));
            if (module == null)
            {
                Logger.Log ("[GUI] Cannot find module for State " + selectedState.stateName, Logger.Level.Debug);
                return;
            }
                

            GUI.color = solidColor;
            GUILayout.BeginVertical();
            for (int i=0; i<module.states.Count; i++)
            {
                if(GUILayout.Button("Select " + module.states[i].stateName, buttonStyle, GUILayout.ExpandWidth(true), GUILayout.Height(22)))
                {
                    selectedSequence.endState = module.states [i];
                    selectedState = null;
                    selectedSequence = null;

                    guiStateSelector = false;
                }
            }


            GUI.color = opaqueColor;
            GUILayout.EndVertical();

            GUI.DragWindow();

        }

        public void LoadConfigXml()
        {
            PluginConfiguration config = PluginConfiguration.CreateForType<SequencerGUI>();
            config.load();
            SequencerWindowPos = config.GetValue<Rect>("SequencerWindowPos");
            SequencerEditorWindowPos = config.GetValue<Rect>("SequencerEditorWindowPos");
        }

        public void SaveConfigXml()
        {
            PluginConfiguration config = PluginConfiguration.CreateForType<SequencerGUI>();
            config.SetValue("SequencerWindowPos", SequencerWindowPos);
            config.SetValue("SequencerEditorWindowPos", SequencerEditorWindowPos);
            config.save();
        }

        private void ProcessFocusChange()
        {
            var temp = lastFocusedControlName.Split (' ');
            Logger.Log ("[GUI] Focus change, lastName = " + lastFocusedControlName 
                + ", lastValue = " + lastFocusedTextFieldValue 
                + ", temp.Length = " + temp.Length, Logger.Level.Debug);

            var servoFields = new string[4] {"SequencerPosition", "SequencerSpeed", "SequencerPositionCommand", "SequencerSpeedCommand"};

            var pos = Array.IndexOf (servoFields, temp [0]);

            Logger.Log ("availableServoCommands found: " + (availableServoCommands != null), Logger.Level.Debug);
            Logger.Log ("pos: " + pos, Logger.Level.Debug);

            if (temp.Length == 2 && pos >= 0 && pos < 4)
            {
                uint servoUID = 0;
                if(uint.TryParse(temp[1], out servoUID) && availableServoCommands != null)
                {
                    float tmpValue;
                    BasicCommand command;
                    if (guiCommandEditor && selectedBasicCommand != null)
                    {
                        command = selectedBasicCommand;
                    }
                    else
                    {
                        command = availableServoCommands.Find (p => p.servo.UID == servoUID);
                    }
                    
                    Logger.Log ("Command found: " + (command != null), Logger.Level.Debug);

                    if (float.TryParse (lastFocusedTextFieldValue, out tmpValue)) 
                    {
                        if ((pos == 0 || pos == 2) && command != null && command.servo != null)
                        {
                            command.position = Mathf.Clamp(tmpValue, command.servo.MinPosition, command.servo.MaxPosition);
                        }
                        else if ((pos == 1 || pos == 3) && command != null && command.servo != null)
                        {
                            command.speedMultiplier = Mathf.Clamp(tmpValue, 0.05f, 1000f);
                        }
                    }

                }
            }

            lastFocusedControlName = GUI.GetNameOfFocusedControl();
            lastFocusedTextFieldValue = "";
        }

        private void OnGUI()
        {
            //requires ServoGroups to be parsed
            if (!IRWrapper.APIReady)
            {
                if (appLauncherButton != null)
                {
                    appLauncherButton.VisibleInScenes = ApplicationLauncher.AppScenes.NEVER;
                }
                return;
            }

            if (appLauncherButton != null)
            {
                appLauncherButton.VisibleInScenes = ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB;
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (FlightGlobals.ActiveVessel == null)
                    return;
                
                var storage = FlightGlobals.ActiveVessel.FindPartModulesImplementing<ModuleSequencer>();
                if (GUIEnabled && (storage == null || storage.Count == 0))
                {
                    ScreenMessages.PostScreenMessage("Sequencer module is required (add probe core).", 3, ScreenMessageStyle.UPPER_CENTER);
                    GUIEnabled = false;
                    return;
                }
            }
            else if (HighLogic.LoadedSceneIsEditor)
            {
                if (EditorLogic.fetch != null) 
                {
                    var s = EditorLogic.fetch.ship;
                    if (s != null)
                    {
                        var storagePart = s.Parts.Find (p => p.FindModuleImplementing<ModuleSequencer> () != null);
                        if (GUIEnabled && storagePart == null) 
                        {
                            ScreenMessages.PostScreenMessage("Sequencer module is required (add probe core).", 3, ScreenMessageStyle.UPPER_CENTER);
                            GUIEnabled = false;
                            return;
                        }
                    }
                }
            }

            if (SequencerWindowPos.x == 0 && SequencerWindowPos.y == 0)
            {
                SequencerWindowPos = new Rect(Screen.width - 510, 70, 10, 10);
            }

            if (SequencerEditorWindowPos.x == 0 && SequencerEditorWindowPos.y == 0)
            {
                SequencerEditorWindowPos = new Rect(Screen.width - 510, 300, 10, 10);
            }

            if (SequencerCommandEditorWindowPos.x == 0 && SequencerCommandEditorWindowPos.y == 0)
            {
                SequencerCommandEditorWindowPos = new Rect(Input.mousePosition.x - 100, Screen.height - Input.mousePosition.y + 17, 10, 10);
            }

            if (SequencerStateSelectorWindowPos.x == 0 && SequencerStateSelectorWindowPos.y == 0)
            {
                SequencerStateSelectorWindowPos = new Rect(Input.mousePosition.x - 100, Screen.height - Input.mousePosition.y + 17, 10, 10);
            }

            GUI.skin = DefaultSkinProvider.DefaultSkin;
            GUI.color = opaqueColor;

            if (!GUISetupDone)
                InitGUI();
            
            if (GUIEnabled && !guiHidden)
            {
                if (lastFocusedControlName != GUI.GetNameOfFocusedControl())
                {
                    ProcessFocusChange ();
                }

                //this code defocuses the TexFields if you click mouse elsewhere
                if (GUIUtility.hotControl > 0 && GUIUtility.hotControl != GUIUtility.keyboardControl)
                {
                    GUIUtility.keyboardControl = 0;
                }

                SequencerWindowPos = GUILayout.Window(SequencerWindowID, SequencerWindowPos,
                SequencerControlWindow,
                "Servo Sequencer",
                GUILayout.Width(300),
                GUILayout.Height(80));

                if (guiSequenceEditor)
                {
                    float height = Screen.height / 2f;
                    string windowTitle = "Sequence Editor: " + openSequence.name;

                    if (openSequence.isLocked)
                        windowTitle += " (locked)";

                    if(openSequence != null)
                        SequencerEditorWindowPos = GUILayout.Window(SequencerEditorWindowID, SequencerEditorWindowPos,
                        SequencerEditorWindow,
                        windowTitle,
                        GUILayout.Width(640),
                        GUILayout.Height(height));
                }
                if(guiCommandEditor && selectedBasicCommand != null)
                {
                    string windowTitle = "Command Editor: " + openSequence.name + " [" + selectedBasicCommandIndex.ToString() + "]";

                    if(selectedBasicCommand != null)
                        SequencerCommandEditorWindowPos = GUILayout.Window(SequencerCommandEditorWindowID, SequencerCommandEditorWindowPos,
                            SequencerCommandEditorWindow,
                            windowTitle,
                            GUILayout.Width(250),
                            GUILayout.Height(50));
                }

                if(guiStateSelector && selectedState != null && selectedSequence != null)
                {
                    string windowTitle = "Select End State for " + selectedSequence.name;

                    SequencerStateSelectorWindowPos = GUILayout.Window(SequencerStateSelectorWindowID, SequencerStateSelectorWindowPos,
                            SequencerStateSelectWindow,
                            windowTitle,
                            GUILayout.Width(100),
                            GUILayout.Height(50));
                }
            }
            GUI.color = solidColor;
            DrawTooltip();

            if(HighLogic.LoadedSceneIsEditor)
            {
                var mousePos = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                bool lockEditor = GUIEnabled && (SequencerWindowPos.Contains(mousePos) || 
                                                (guiSequenceEditor && SequencerEditorWindowPos.Contains(mousePos)) ||
                                                (guiCommandEditor && SequencerCommandEditorWindowPos.Contains(mousePos))
                );

                EditorLock(lockEditor);
            }
        }

        /// <summary>
        ///     Applies or removes the lock
        /// </summary>
        /// <param name="apply">Which way are we going</param>
        internal void EditorLock(Boolean apply)
        {
            //only do this lock in the editor - no point elsewhere
            if (HighLogic.LoadedSceneIsEditor && apply)
            {
                //only add a new lock if there isnt already one there
                if (InputLockManager.GetControlLock("IRSGUILockOfEditor") != ControlTypes.EDITOR_LOCK)
                {
                    Logger.Log(String.Format("[GUI] AddingLock-{0}", "IRSGUILockOfEditor"), Logger.Level.Debug);

                    InputLockManager.SetControlLock(ControlTypes.EDITOR_LOCK, "IRSGUILockOfEditor");
                }
            }
            //Otherwise make sure the lock is removed
            else
            {
                //Only try and remove it if there was one there in the first place
                if (InputLockManager.GetControlLock("IRSGUILockOfEditor") == ControlTypes.EDITOR_LOCK)
                {
                    Logger.Log(String.Format("[GUI] Removing-{0}", "IRSGUILockOfEditor"), Logger.Level.Debug);
                    InputLockManager.RemoveControlLock("IRSGUILockOfEditor");
                }
            }
        }
    }

    
}
