using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using MelonLoader;
using UiPrototypeTemplateMod.Core;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(UiPrototypeTemplateMod.PrototypeTemplateMod), "UI Prototype Template Mod", "0.1.0", "user0")]
[assembly: MelonGame]
[assembly: HarmonyDontPatchAll]

namespace UiPrototypeTemplateMod
{
    public sealed class PrototypeTemplateMod : MelonMod
    {
        private const string ActivationMessage = "UI prototype takeover active - normal game UI is visually replaced.";
        private static readonly Color OverlayColor = new Color(0.02f, 0.02f, 0.025f, 0.98f);
        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _bodyStyle;
        private static GUIStyle _overlayStyle;
        private static Texture2D _overlayTexture;
        private static bool _loggedFirstGui;
        private static GameObject _runtimeOverlayObject;
        private static string _diagnosticsLine = "Input: initializing";
        private static object _activeSession;
        private static int _variantIndex;
        private object _session;
        private MajdataInputAdapter _majdataInput;
        private GameObject _overlayObject;
        private SynchronizationContext _mainThreadContext;
        private Timer _tickTimer;
        private int _tickPending;
        private bool _loggedTimerTick;

        static PrototypeTemplateMod()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveSupportAssembly;
        }

        public override void OnApplicationStart()
        {
            MelonLogger.Msg(ActivationMessage);
            PrototypeSession session = PrototypeSession.CreateDefault();
            _session = session;
            _activeSession = session;
            _majdataInput = MajdataInputAdapter.Create();
            _mainThreadContext = SynchronizationContext.Current;
            _diagnosticsLine = "Input: " + (_majdataInput.IsAvailable ? PrototypeInputSource.MajdataReflection.ToString() : PrototypeInputSource.KeyboardFallback.ToString());
            MelonLogger.Msg("Prototype core ready: phase=" + session.Phase + " song=" + session.SelectedSong.Title + " difficulty=" + session.SelectedDifficulty.Name);
            MelonLogger.Msg("Prototype input source: " + _diagnosticsLine);
            EnsureOverlayObject();
            StartTickTimer();
        }

        public override void OnUpdate()
        {
            EnsureOverlayObject();
            UpdateVariantFromKeyboard();
            UpdatePrototypeStateFromInput();
            Time.timeScale = 0f;
        }

        public override void OnGUI()
        {
            RenderOverlay();
        }

        public override void OnApplicationQuit()
        {
            if (_tickTimer != null)
            {
                _tickTimer.Dispose();
                _tickTimer = null;
            }

            Time.timeScale = 1f;
        }

        internal static void RenderOverlay()
        {
            EnsureGuiResources();

            if (!_loggedFirstGui)
            {
                MelonLogger.Msg("UI prototype placeholder rendering through IMGUI.");
                _loggedFirstGui = true;
            }

            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.Box(screen, GUIContent.none, _overlayStyle);

            float scale = Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
            if (scale < 0.55f)
            {
                scale = 0.55f;
            }

            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3((Screen.width - 1920f * scale) * 0.5f, (Screen.height - 1080f * scale) * 0.5f, 0f),
                Quaternion.identity,
                new Vector3(scale, scale, 1f));

            DrawVirtualCanvas();
            GUI.matrix = previousMatrix;
        }

        internal static string DiagnosticsLine
        {
            get { return _diagnosticsLine; }
        }

        internal static object ActiveSession
        {
            get { return _activeSession; }
        }

        internal static int VariantIndex
        {
            get { return _variantIndex; }
        }

        internal static string VariantLabel
        {
            get { return "VARIANT " + (_variantIndex + 1) + "/" + PrototypeVariantRouter.VariantCount; }
        }

        private void UpdatePrototypeStateFromInput()
        {
            PrototypeSession session = _session as PrototypeSession;
            if (session == null)
            {
                return;
            }

            RawPrototypeInput input;
            if (_majdataInput != null && _majdataInput.TryRead(out input))
            {
                _diagnosticsLine = "Input: " + input.Source;
            }
            else
            {
                input = ReadKeyboardFallback();
                _diagnosticsLine = "Input: " + input.Source;
            }

            PrototypeInputFrame frame = PrototypeInputMapper.Map(session.Phase, input);
            if (!frame.HasActions)
            {
                return;
            }

            for (int i = 0; i < frame.Actions.Count; i++)
            {
                session.Apply(frame.Actions[i]);
            }

            _diagnosticsLine = "Input: " + frame.Source + " Actions: " + JoinActions(frame);
            PrototypeOverlayBehaviour.RefreshAll();
        }

        private void StartTickTimer()
        {
            if (_tickTimer != null || _mainThreadContext == null)
            {
                return;
            }

            _tickTimer = new Timer(TimerTick, null, 100, 50);
            MelonLogger.Msg("Prototype input timer started.");
        }

        private void TimerTick(object state)
        {
            if (Interlocked.Exchange(ref _tickPending, 1) == 1)
            {
                return;
            }

            _mainThreadContext.Post(TickOnMainThread, null);
        }

        private void TickOnMainThread(object state)
        {
            try
            {
                EnsureOverlayObject();
                UpdateVariantFromKeyboard();
                UpdatePrototypeStateFromInput();
                Time.timeScale = 0f;
                if (!_loggedTimerTick)
                {
                    MelonLogger.Msg("Prototype input timer reached Unity main thread.");
                    _loggedTimerTick = true;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _tickPending, 0);
            }
        }

        private static RawPrototypeInput ReadKeyboardFallback()
        {
            return new RawPrototypeInput(
                PrototypeInputSource.KeyboardFallback,
                KeyboardInput.GetKeyDown(KeyCode.DownArrow) || KeyboardInput.GetKeyDown(KeyCode.D),
                KeyboardInput.GetKeyDown(KeyCode.Return) || KeyboardInput.GetKeyDown(KeyCode.Space),
                KeyboardInput.GetKeyDown(KeyCode.Escape) || KeyboardInput.GetKeyDown(KeyCode.Backspace),
                KeyboardInput.GetKeyDown(KeyCode.UpArrow) || KeyboardInput.GetKeyDown(KeyCode.A));
        }

        private static void UpdateVariantFromKeyboard()
        {
            int previous = _variantIndex;
            if (KeyboardInput.GetKeyDown(KeyCode.RightArrow))
            {
                _variantIndex = PrototypeVariantRouter.Next(_variantIndex);
            }
            else if (KeyboardInput.GetKeyDown(KeyCode.LeftArrow))
            {
                _variantIndex = PrototypeVariantRouter.Previous(_variantIndex);
            }

            if (previous != _variantIndex)
            {
                _diagnosticsLine = "Variant switched: " + VariantLabel;
                MelonLogger.Msg("Prototype variant switched to " + VariantLabel);
                PrototypeOverlayBehaviour.RefreshAll();
            }
        }

        private static string JoinActions(PrototypeInputFrame frame)
        {
            string result = string.Empty;
            for (int i = 0; i < frame.Actions.Count; i++)
            {
                if (i > 0)
                {
                    result += ",";
                }

                result += frame.Actions[i].ToString();
            }

            return result;
        }

        private void EnsureOverlayObject()
        {
            if (_overlayObject != null)
            {
                return;
            }

            _overlayObject = new GameObject("UI Prototype Template Overlay");
            UnityEngine.Object.DontDestroyOnLoad(_overlayObject);
            _overlayObject.AddComponent<PrototypeOverlayBehaviour>();
            MelonLogger.Msg("UI prototype overlay behaviour installed.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void InstallRuntimeOverlay()
        {
            if (_runtimeOverlayObject != null)
            {
                return;
            }

            _runtimeOverlayObject = new GameObject("UI Prototype Template Runtime Overlay");
            UnityEngine.Object.DontDestroyOnLoad(_runtimeOverlayObject);
            _runtimeOverlayObject.AddComponent<PrototypeOverlayBehaviour>();
            MelonLogger.Msg("UI prototype runtime overlay behaviour installed.");
        }

        private static void DrawVirtualCanvas()
        {
            GUI.Label(new Rect(96f, 82f, 1728f, 120f), new GUIContent("MAJDATA UI PROTOTYPE"), _titleStyle);
            GUI.Label(new Rect(104f, 214f, 1712f, 72f), new GUIContent("Template mod takeover is active"), _subtitleStyle);

            Rect panel = new Rect(180f, 350f, 1560f, 390f);
            GUI.Box(panel, string.Empty);
            GUI.Label(
                new Rect(panel.x + 64f, panel.y + 54f, panel.width - 128f, 120f),
                new GUIContent("This placeholder intentionally covers the normal game screen."),
                _bodyStyle);
            GUI.Label(
                new Rect(panel.x + 64f, panel.y + 178f, panel.width - 128f, 140f),
                new GUIContent("Next slices add a pure prototype state core, semantic input actions, and the song-first selection flow."),
                _bodyStyle);

            GUI.Label(new Rect(104f, 910f, 1712f, 80f), new GUIContent("Prototype-only MelonLoader mod - remove the DLL to restore normal MajdataPlay behavior."), _subtitleStyle);
            GUI.Label(new Rect(104f, 980f, 1712f, 48f), new GUIContent(DiagnosticsLine), _subtitleStyle);
        }

        private static void EnsureGuiResources()
        {
            if (_overlayTexture == null)
            {
                _overlayTexture = new Texture2D(1, 1);
                _overlayTexture.SetPixel(0, 0, OverlayColor);
                _overlayTexture.Apply();
            }

            if (_overlayStyle == null)
            {
                _overlayStyle = new GUIStyle();
                _overlayStyle.normal.background = _overlayTexture;
            }

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle()
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 72,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.95f, 0.98f, 1f, 1f) }
                };
            }

            if (_subtitleStyle == null)
            {
                _subtitleStyle = new GUIStyle()
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 34,
                    normal = { textColor = new Color(0.72f, 0.86f, 0.9f, 1f) }
                };
            }

            if (_bodyStyle == null)
            {
                _bodyStyle = new GUIStyle()
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 40,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
            }
        }

        private static Assembly ResolveSupportAssembly(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new AssemblyName(args.Name);
            if (name.Name != "UiPrototypeTemplateMod.Core")
            {
                return null;
            }

            string path = Path.Combine(Environment.CurrentDirectory, "Mods", "UiPrototypeTemplateModLib", "UiPrototypeTemplateMod.Core.dll");
            if (!File.Exists(path))
            {
                return null;
            }

            return Assembly.LoadFrom(path);
        }
    }

    internal static class KeyboardInput
    {
        private static readonly bool[] WasDown = new bool[256];

        public static bool GetKeyDown(KeyCode key)
        {
            int virtualKey = ToVirtualKey(key);
            if (virtualKey <= 0)
            {
                return false;
            }

            bool isDown = (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
            bool wasDown = WasDown[virtualKey];
            WasDown[virtualKey] = isDown;
            return isDown && !wasDown;
        }

        private static int ToVirtualKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Backspace:
                    return 0x08;
                case KeyCode.Return:
                    return 0x0D;
                case KeyCode.Escape:
                    return 0x1B;
                case KeyCode.Space:
                    return 0x20;
                case KeyCode.LeftArrow:
                    return 0x25;
                case KeyCode.UpArrow:
                    return 0x26;
                case KeyCode.RightArrow:
                    return 0x27;
                case KeyCode.DownArrow:
                    return 0x28;
                case KeyCode.A:
                    return 0x41;
                case KeyCode.D:
                    return 0x44;
                default:
                    return 0;
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }

    internal sealed class MajdataInputAdapter
    {
        private const BindingFlags StaticPublic = BindingFlags.Public | BindingFlags.Static;
        private readonly MethodInfo _getButtonDown;
        private readonly MethodInfo _getInputDown;
        private readonly object _a3Button;
        private readonly object _a4Button;
        private readonly object _a5Button;
        private readonly object _a6Button;
        private readonly object _a3Sensor;
        private readonly object _a4Sensor;
        private readonly object _a5Sensor;
        private readonly object _a6Sensor;

        private MajdataInputAdapter(
            MethodInfo getButtonDown,
            MethodInfo getInputDown,
            object a3Button,
            object a4Button,
            object a5Button,
            object a6Button,
            object a3Sensor,
            object a4Sensor,
            object a5Sensor,
            object a6Sensor)
        {
            _getButtonDown = getButtonDown;
            _getInputDown = getInputDown;
            _a3Button = a3Button;
            _a4Button = a4Button;
            _a5Button = a5Button;
            _a6Button = a6Button;
            _a3Sensor = a3Sensor;
            _a4Sensor = a4Sensor;
            _a5Sensor = a5Sensor;
            _a6Sensor = a6Sensor;
        }

        public bool IsAvailable
        {
            get { return _getButtonDown != null || _getInputDown != null; }
        }

        public static MajdataInputAdapter Create()
        {
            try
            {
                Type inputManagerType = Type.GetType("MajdataPlay.IO.InputManager, Assembly-CSharp", false);
                Type buttonZoneType = Type.GetType("MajdataPlay.IO.ButtonZone, Assembly-CSharp", false);
                Type sensorAreaType = Type.GetType("MajdataPlay.IO.SensorArea, Assembly-CSharp", false);
                if (inputManagerType == null || buttonZoneType == null)
                {
                    return new MajdataInputAdapter(null, null, null, null, null, null, null, null, null, null);
                }

                MethodInfo getButtonDown = inputManagerType.GetMethod("GetButtonDown", StaticPublic, null, new[] { typeof(int), buttonZoneType }, null);
                MethodInfo getInputDown = null;
                object a3Sensor = null;
                object a4Sensor = null;
                object a5Sensor = null;
                object a6Sensor = null;

                if (sensorAreaType != null)
                {
                    getInputDown = inputManagerType.GetMethod("GetInputDown", StaticPublic, null, new[] { typeof(int), buttonZoneType, sensorAreaType }, null);
                    a3Sensor = Enum.Parse(sensorAreaType, "A3");
                    a4Sensor = Enum.Parse(sensorAreaType, "A4");
                    a5Sensor = Enum.Parse(sensorAreaType, "A5");
                    a6Sensor = Enum.Parse(sensorAreaType, "A6");
                }

                return new MajdataInputAdapter(
                    getButtonDown,
                    getInputDown,
                    Enum.Parse(buttonZoneType, "A3"),
                    Enum.Parse(buttonZoneType, "A4"),
                    Enum.Parse(buttonZoneType, "A5"),
                    Enum.Parse(buttonZoneType, "A6"),
                    a3Sensor,
                    a4Sensor,
                    a5Sensor,
                    a6Sensor);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Majdata input reflection unavailable: " + ex.Message);
                return new MajdataInputAdapter(null, null, null, null, null, null, null, null, null, null);
            }
        }

        public bool TryRead(out RawPrototypeInput input)
        {
            input = null;
            if (!IsAvailable)
            {
                return false;
            }

            try
            {
                input = new RawPrototypeInput(
                    PrototypeInputSource.MajdataReflection,
                    ReadButton(_a3Button, _a3Sensor),
                    ReadButton(_a4Button, _a4Sensor),
                    ReadButton(_a5Button, _a5Sensor),
                    ReadButton(_a6Button, _a6Sensor));
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("Majdata input reflection read failed; using keyboard fallback. " + ex.Message);
                return false;
            }
        }

        private bool ReadButton(object button, object sensor)
        {
            if (_getInputDown != null && sensor != null)
            {
                return Convert.ToBoolean(_getInputDown.Invoke(null, new[] { (object)0, button, sensor }));
            }

            if (_getButtonDown != null)
            {
                return Convert.ToBoolean(_getButtonDown.Invoke(null, new[] { (object)0, button }));
            }

            return false;
        }
    }

    public sealed class PrototypeOverlayBehaviour : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<PrototypeOverlayBehaviour> Instances = new System.Collections.Generic.List<PrototypeOverlayBehaviour>();
        private GameObject _canvasObject;
        private GameObject _contentRoot;
        private bool _loggedUpdate;

        public void Awake()
        {
            Instances.Add(this);
            MelonLogger.Msg("UI prototype overlay behaviour awake.");
            InstallCanvasFallback();
        }

        public void OnDestroy()
        {
            Instances.Remove(this);
        }

        public void Start()
        {
            MelonLogger.Msg("UI prototype overlay behaviour started.");
        }

        public void Update()
        {
            Time.timeScale = 0f;
            if (!_loggedUpdate)
            {
                MelonLogger.Msg("UI prototype overlay behaviour is ticking.");
                _loggedUpdate = true;
            }
        }

        public static void RefreshAll()
        {
            for (int i = 0; i < Instances.Count; i++)
            {
                if (Instances[i] != null)
                {
                    Instances[i].RefreshCanvas();
                }
            }
        }

        public void OnGUI()
        {
            PrototypeTemplateMod.RenderOverlay();
        }

        private void InstallCanvasFallback()
        {
            if (_canvasObject != null)
            {
                return;
            }

            _canvasObject = new GameObject("UI Prototype Template Canvas Placeholder");
            UnityEngine.Object.DontDestroyOnLoad(_canvasObject);

            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;
            _canvasObject.AddComponent<CanvasScaler>();
            _canvasObject.AddComponent<GraphicRaycaster>();
            RefreshCanvas();
            MelonLogger.Msg("UI prototype song-first canvas installed.");
        }

        private void RefreshCanvas()
        {
            if (_canvasObject == null)
            {
                return;
            }

            if (_contentRoot != null)
            {
                UnityEngine.Object.Destroy(_contentRoot);
            }

            _contentRoot = new GameObject("Song First Prototype Content");
            _contentRoot.transform.SetParent(_canvasObject.transform, false);

            GameObject panelObject = CreateObject("Full Screen Prototype Blocker");
            Image panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0.02f, 0.02f, 0.025f, 0.98f);
            Stretch(panel.rectTransform);

            PrototypeSession session = PrototypeTemplateMod.ActiveSession as PrototypeSession;
            if (session == null)
            {
                AddText("Title", "MAJDATA UI PROTOTYPE", 72, FontStyle.Bold, new Vector2(0f, 140f), new Vector2(1700f, 120f));
                AddText("Body", "Prototype state is initializing.", 42, FontStyle.Normal, new Vector2(0f, -40f), new Vector2(1500f, 120f));
                AddText("Diagnostics", PrototypeTemplateMod.DiagnosticsLine, 30, FontStyle.Normal, new Vector2(0f, -220f), new Vector2(1700f, 70f));
                return;
            }

            DrawSongFirstFlow(session);
        }

        private void DrawSongFirstFlow(PrototypeSession session)
        {
            PrototypeSong song = session.SelectedSong;
            PrototypeDifficulty difficulty = session.SelectedDifficulty;
            string phase = session.Phase.ToString();

            AddText("Variant Label", PrototypeTemplateMod.VariantLabel, 76, FontStyle.Bold, new Vector2(0f, 454f), new Vector2(1780f, 86f));
            AddText("Header", "SONG-FIRST SELECTION PROTOTYPE", 34, FontStyle.Bold, new Vector2(0f, 396f), new Vector2(1780f, 50f));
            AddText("Phase", "Phase: " + phase + "    Song: " + song.Title + "    Difficulty: " + difficulty.Name + " " + difficulty.Level, 26, FontStyle.Normal, new Vector2(0f, 352f), new Vector2(1780f, 48f));
            AddText("Diagnostics", PrototypeTemplateMod.DiagnosticsLine, 24, FontStyle.Normal, new Vector2(0f, -476f), new Vector2(1780f, 44f));

            if (PrototypeTemplateMod.VariantIndex == 0)
            {
                DrawSinmaiLikeVariant(session);
            }
            else if (PrototypeTemplateMod.VariantIndex == 1)
            {
                DrawMajdataHybridVariant(session);
            }
            else
            {
                DrawCompactFastFlowVariant(session);
            }

            DrawPrompts(session.Phase);
        }

        private void DrawSinmaiLikeVariant(PrototypeSession session)
        {
            AddText("Variant Name", "Sinmai-like carousel", 24, FontStyle.Normal, new Vector2(0f, 314f), new Vector2(1780f, 34f));
            DrawCarousel(session);
            DrawSongDetails(session.SelectedSong);
            DrawDifficulties(session);
            DrawPhasePanel(session);
        }

        private void DrawMajdataHybridVariant(PrototypeSession session)
        {
            AddText("Variant Name", "Majdata-native hybrid list", 24, FontStyle.Normal, new Vector2(0f, 314f), new Vector2(1780f, 34f));
            AddPanel("Vertical List", new Vector2(-650f, -20f), new Vector2(500f, 650f), new Color(0.07f, 0.1f, 0.13f, 0.92f));
            for (int i = 0; i < session.Songs.Count; i++)
            {
                PrototypeSong row = session.Songs[i];
                bool selected = i == session.SelectedSongIndex;
                Vector2 rowPosition = new Vector2(-650f, 220f - i * 112f);
                AddPanel("Song Row " + i, rowPosition, new Vector2(440f, 82f), selected ? new Color(0.12f, 0.42f, 0.36f, 0.95f) : new Color(0.12f, 0.15f, 0.2f, 0.88f));
                AddText("Song Row Text " + i, row.Title + "\n" + row.Category, selected ? 25 : 21, selected ? FontStyle.Bold : FontStyle.Normal, rowPosition, new Vector2(410f, 72f));
            }

            AddPanel("Hybrid Detail", new Vector2(190f, 62f), new Vector2(1050f, 430f), new Color(0.1f, 0.12f, 0.18f, 0.92f));
            AddText("Hybrid Title", session.SelectedSong.Title, 54, FontStyle.Bold, new Vector2(190f, 184f), new Vector2(970f, 82f));
            AddText("Hybrid Meta", session.SelectedSong.Artist + "    BPM " + session.SelectedSong.Bpm + "    " + session.SelectedSong.Category + FlagText(session.SelectedSong), 28, FontStyle.Normal, new Vector2(190f, 104f), new Vector2(970f, 52f));
            DrawDifficulties(session);
            DrawPhasePanel(session);
        }

        private void DrawCompactFastFlowVariant(PrototypeSession session)
        {
            AddText("Variant Name", "Compact fast-flow hybrid", 24, FontStyle.Normal, new Vector2(0f, 314f), new Vector2(1780f, 34f));
            AddPanel("Compact Main", new Vector2(0f, 72f), new Vector2(1620f, 390f), new Color(0.1f, 0.11f, 0.12f, 0.94f));
            AddText("Compact Title", session.SelectedSong.Title, 60, FontStyle.Bold, new Vector2(-320f, 158f), new Vector2(880f, 92f));
            AddText("Compact Meta", session.SelectedSong.Artist + " / " + session.SelectedSong.Category + " / BPM " + session.SelectedSong.Bpm + FlagText(session.SelectedSong), 30, FontStyle.Normal, new Vector2(-320f, 82f), new Vector2(880f, 56f));
            AddText("Compact Phase", session.Phase == PrototypePhase.SongSelect ? "Pick song first" : session.Phase == PrototypePhase.DifficultySelect ? "Now choose difficulty" : "Confirmed", 40, FontStyle.Bold, new Vector2(520f, 118f), new Vector2(520f, 80f));
            AddText("Compact Difficulty", session.SelectedDifficulty.Name + " " + session.SelectedDifficulty.Level + "\n" + session.SelectedDifficulty.Rank + " " + session.SelectedDifficulty.DxScore, 34, FontStyle.Bold, new Vector2(520f, -8f), new Vector2(520f, 132f));
            DrawDifficulties(session);
        }

        private void DrawCarousel(PrototypeSession session)
        {
            int count = session.Songs.Count;
            int previous = session.SelectedSongIndex == 0 ? count - 1 : session.SelectedSongIndex - 1;
            int next = session.SelectedSongIndex == count - 1 ? 0 : session.SelectedSongIndex + 1;

            AddPanel("Previous Song Panel", new Vector2(-620f, 190f), new Vector2(420f, 150f), new Color(0.12f, 0.16f, 0.2f, 0.86f));
            AddPanel("Current Song Panel", new Vector2(0f, 190f), new Vector2(620f, 190f), new Color(0.1f, 0.34f, 0.42f, 0.94f));
            AddPanel("Next Song Panel", new Vector2(620f, 190f), new Vector2(420f, 150f), new Color(0.12f, 0.16f, 0.2f, 0.86f));

            AddText("Previous Song", session.Songs[previous].Title, 28, FontStyle.Normal, new Vector2(-620f, 190f), new Vector2(380f, 110f));
            AddText("Current Song", session.SelectedSong.Title, 44, FontStyle.Bold, new Vector2(0f, 204f), new Vector2(580f, 100f));
            AddText("Current Category", session.SelectedSong.Category, 24, FontStyle.Normal, new Vector2(0f, 138f), new Vector2(580f, 42f));
            AddText("Next Song", session.Songs[next].Title, 28, FontStyle.Normal, new Vector2(620f, 190f), new Vector2(380f, 110f));
        }

        private void DrawSongDetails(PrototypeSong song)
        {
            AddPanel("Song Details Panel", new Vector2(-480f, -54f), new Vector2(770f, 310f), new Color(0.08f, 0.09f, 0.12f, 0.9f));
            AddText("Song Title", song.Title, 42, FontStyle.Bold, new Vector2(-480f, 42f), new Vector2(700f, 62f));
            AddText("Song Artist", song.Artist + "    BPM " + song.Bpm, 28, FontStyle.Normal, new Vector2(-480f, -10f), new Vector2(700f, 44f));
            AddText("Song Meta", song.Category + "    " + song.Badge + FlagText(song), 24, FontStyle.Normal, new Vector2(-480f, -62f), new Vector2(700f, 44f));
            AddText("Song Score", "Best visible per difficulty: ranks and DX-score-like values stay available before choosing difficulty.", 23, FontStyle.Normal, new Vector2(-480f, -138f), new Vector2(680f, 80f));
        }

        private void DrawDifficulties(PrototypeSession session)
        {
            PrototypeSong song = session.SelectedSong;
            float startX = -760f;
            for (int i = 0; i < song.Difficulties.Count; i++)
            {
                PrototypeDifficulty diff = song.Difficulties[i];
                bool selected = i == session.SelectedDifficultyIndex;
                Color color = selected ? new Color(0.95f, 0.72f, 0.18f, 0.96f) : diff.CanSelect ? new Color(0.18f, 0.24f, 0.32f, 0.92f) : new Color(0.18f, 0.18f, 0.18f, 0.72f);
                Vector2 position = new Vector2(startX + i * 305f, -284f);
                AddPanel("Difficulty " + i, position, new Vector2(270f, 126f), color);
                AddText("Difficulty Name " + i, diff.Name + " " + diff.Level, 24, FontStyle.Bold, new Vector2(position.x, position.y + 30f), new Vector2(250f, 38f));
                AddText("Difficulty Score " + i, diff.CanSelect ? diff.Rank + "  " + diff.DxScore : diff.Locked ? "LOCKED" : "UNAVAILABLE", 22, FontStyle.Normal, new Vector2(position.x, position.y - 22f), new Vector2(250f, 38f));
            }
        }

        private void DrawPhasePanel(PrototypeSession session)
        {
            string title;
            string body;
            if (session.Phase == PrototypePhase.SongSelect)
            {
                title = "BROWSE SONGS";
                body = "A3/A6 move through songs. Difficulty is context only until OK.";
            }
            else if (session.Phase == PrototypePhase.DifficultySelect)
            {
                title = "DIFFICULTY SELECT";
                body = "Selected song remains fixed. A3/A6 now change difficulty.";
            }
            else
            {
                title = "CONFIRMED";
                body = "Prototype confirmation reached. Back returns to difficulty selection.";
            }

            AddPanel("Phase Panel", new Vector2(520f, -54f), new Vector2(690f, 310f), new Color(0.2f, 0.12f, 0.28f, 0.88f));
            AddText("Phase Panel Title", title, 44, FontStyle.Bold, new Vector2(520f, 30f), new Vector2(620f, 70f));
            AddText("Phase Panel Body", body, 30, FontStyle.Normal, new Vector2(520f, -76f), new Vector2(600f, 130f));
        }

        private void DrawPrompts(PrototypePhase phase)
        {
            string prompts = phase == PrototypePhase.SongSelect
                ? "A3 Next Song    A6 Previous Song    A4 OK    A5 Category/Back"
                : phase == PrototypePhase.DifficultySelect
                    ? "A3 Harder    A6 Easier    A4 Confirm    A5 Back to Songs"
                    : "A5 Back to Difficulty";

            AddPanel("Prompt Bar", new Vector2(0f, -408f), new Vector2(1780f, 72f), new Color(0.05f, 0.06f, 0.08f, 0.94f));
            AddText("Prompts", prompts, 28, FontStyle.Bold, new Vector2(0f, -408f), new Vector2(1720f, 52f));
        }

        private static string FlagText(PrototypeSong song)
        {
            string flags = string.Empty;
            if (song.IsLong)
            {
                flags += "    LONG";
            }

            if (song.IsSpecial)
            {
                flags += "    SPECIAL";
            }

            return flags;
        }

        private GameObject CreateObject(string name)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(_contentRoot.transform, false);
            return obj;
        }

        private void AddPanel(string name, Vector2 position, Vector2 dimensions, Color color)
        {
            GameObject obj = CreateObject(name);
            Image image = obj.AddComponent<Image>();
            image.color = color;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
        }

        private void AddText(string name, string value, int size, FontStyle style, Vector2 position, Vector2 dimensions)
        {
            GameObject textObject = CreateObject(name);
            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
