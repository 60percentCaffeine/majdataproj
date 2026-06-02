using System;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
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
        private const float VirtualCanvasWidth = 1080f;
        private const float VirtualCanvasHeight = 1920f;
        private static readonly Color OverlayColor = new Color(0.02f, 0.02f, 0.025f, 0.98f);
        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _bodyStyle;
        private static GUIStyle _overlayStyle;
        private static GUIStyle _solidStyle;
        private static Texture2D _overlayTexture;
        private static Texture2D _solidTexture;
        private static Texture2D _circleFieldTexture;
        private static Texture2D _selectedCoverTexture;
        private static Texture2D _blueCoverTexture;
        private static Texture2D _pinkCoverTexture;
        private static Texture2D _darkCoverTexture;
        private static Texture2D _levelBadgeTexture;
        private static bool _loggedFirstGui;
        private static GameObject _runtimeOverlayObject;
        private static string _diagnosticsLine = "Input: initializing";
        private static object _activeSession;
        private static int _variantIndex;
        private object _session;
        private PrototypeInputAdapter _inputAdapter;
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
            _inputAdapter = new PrototypeInputAdapter();
            _mainThreadContext = SynchronizationContext.Current;
            _diagnosticsLine = "Input: PrototypeOwned";
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
            Time.timeScale = 1f;
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

            if (_inputAdapter != null)
            {
                _inputAdapter.Dispose();
                _inputAdapter = null;
            }

            Time.timeScale = 1f;
        }

        internal static void RenderOverlay()
        {
            EnsureGuiResources();

            if (!_loggedFirstGui)
            {
                MelonLogger.Msg(ActiveSession == null
                    ? "UI prototype initialization placeholder rendering through IMGUI."
                    : "UI prototype song-first screen rendering through IMGUI.");
                _loggedFirstGui = true;
            }

            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.Box(screen, GUIContent.none, _overlayStyle);
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;

            PrototypeSession activeSession = ActiveSession as PrototypeSession;
            if (activeSession != null)
            {
                float portraitScale = Mathf.Min(Screen.width / VirtualCanvasWidth, Screen.height / VirtualCanvasHeight);
                Matrix4x4 portraitPreviousMatrix = GUI.matrix;
                GUI.matrix = Matrix4x4.TRS(
                    new Vector3((Screen.width - VirtualCanvasWidth * portraitScale) * 0.5f, (Screen.height - VirtualCanvasHeight * portraitScale) * 0.5f, 0f),
                    Quaternion.identity,
                    new Vector3(portraitScale, portraitScale, 1f));

                DrawSongFirstVirtualCanvas(activeSession);
                GUI.matrix = portraitPreviousMatrix;
                return;
            }

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

            RawPrototypeInput input = _inputAdapter != null ? _inputAdapter.Read() : ReadKeyboardFallback();
            _diagnosticsLine = "Input: " + PrototypeInputAdapter.LastInputLabel;

            PrototypeInputFrame frame = PrototypeInputMapper.Map(session.Phase, input);
            if (!frame.HasActions)
            {
                return;
            }

            for (int i = 0; i < frame.Actions.Count; i++)
            {
                session.Apply(frame.Actions[i]);
            }

            _diagnosticsLine = "Input: " + PrototypeInputAdapter.LastInputLabel + " Actions: " + JoinActions(frame);
            MelonLogger.Msg("Prototype input actions: " + _diagnosticsLine);
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
                Time.timeScale = 1f;
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
                KeyboardInput.GetKeyDown(KeyCode.D),
                KeyboardInput.GetKeyDown(KeyCode.C) || KeyboardInput.GetKeyDown(KeyCode.Return) || KeyboardInput.GetKeyDown(KeyCode.Space),
                KeyboardInput.GetKeyDown(KeyCode.X) || KeyboardInput.GetKeyDown(KeyCode.Escape) || KeyboardInput.GetKeyDown(KeyCode.Backspace),
                KeyboardInput.GetKeyDown(KeyCode.Z));
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

        private static void DrawSongFirstVirtualCanvas(PrototypeSession session)
        {
            PrototypeSong song = session.SelectedSong;
            PrototypeDifficulty difficulty = session.SelectedDifficulty;

            DrawBaseGameShell();
            Color baseText = new Color(0.34f, 0.28f, 0.24f, 1f);
            DrawText(new Rect(34f, 114f, 1010f, 30f), "Press Select P1 to search and sort songs. Long press Button 4 to start practice mode. Press Alt+F4 to exit.", 19, true, baseText, TextAnchor.MiddleLeft);
            DrawText(new Rect(34f, 158f, 990f, 28f), VariantLabel + "    " + session.Phase + "    Song: " + song.Title + "    Difficulty: " + difficulty.Name + " " + difficulty.Level, 18, true, baseText, TextAnchor.MiddleLeft);
            DrawWaveformPanel(song);

            if (VariantIndex == 0)
            {
                DrawVariantOne(session);
            }
            else if (VariantIndex == 1)
            {
                DrawVariantTwo(session);
            }
            else
            {
                DrawVariantThree(session);
            }

            DrawPromptsImgui(session.Phase);
            DrawText(new Rect(24f, 830f, 1032f, 28f), DiagnosticsLine + "    Left/Right switches variants", 18, true, Color.white, TextAnchor.MiddleCenter);
        }

        private static void DrawBaseGameShell()
        {
            DrawPanel(new Rect(0f, 0f, VirtualCanvasWidth, 450f), new Color(1f, 0.985f, 0.95f, 1f));
            DrawPanel(new Rect(0f, 450f, VirtualCanvasWidth, 430f), Color.black);
            DrawPanel(new Rect(0f, 880f, VirtualCanvasWidth, 1040f), new Color(1f, 0.985f, 0.95f, 1f));
            DrawPlaid(new Rect(0f, 0f, VirtualCanvasWidth, 450f));
            DrawPlaid(new Rect(0f, 880f, VirtualCanvasWidth, 1040f));

            DrawPanel(new Rect(24f, 18f, 420f, 92f), new Color(0.34f, 0.28f, 0.24f, 0.96f));
            DrawCircle(new Rect(42f, 26f, 76f, 76f), _blueCoverTexture);
            DrawText(new Rect(132f, 38f, 300f, 48f), "Guest", 28, true, Color.white, TextAnchor.MiddleLeft);

            DrawCircle(new Rect(-6f, 836f, 1092f, 1092f), _circleFieldTexture);
            DrawTab(new Rect(266f, 872f, 206f, 58f), "Lv -");
            DrawTab(new Rect(652f, 872f, 206f, 58f), "Lv +");
            DrawTab(new Rect(16f, 1162f, 70f, 136f), "<");
            DrawTab(new Rect(994f, 1162f, 70f, 136f), ">");
            DrawTab(new Rect(266f, 1842f, 206f, 58f), "Back");
            DrawTab(new Rect(652f, 1842f, 206f, 58f), "OK");
        }

        private static void DrawPlaid(Rect area)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0.95f, 0.75f, 0.56f, 0.22f);
            for (float x = area.x - area.height; x < area.xMax + area.height; x += 86f)
            {
                DrawPanel(new Rect(x, area.y, 10f, area.height), GUI.color);
            }

            GUI.color = new Color(0.95f, 0.75f, 0.56f, 0.14f);
            for (float y = area.y; y < area.yMax; y += 86f)
            {
                DrawPanel(new Rect(area.x, y, area.width, 7f), GUI.color);
            }

            GUI.color = oldColor;
        }

        private static void DrawWaveformPanel(PrototypeSong song)
        {
            Rect panel = new Rect(24f, 202f, 1032f, 214f);
            DrawPanel(panel, new Color(0.34f, 0.28f, 0.24f, 0.96f));
            DrawText(new Rect(42f, 226f, 260f, 118f), "Peak Density = 15\nEsti. Lv " + song.Difficulties[sessionSafeDifficultyIndex(song)].Level + "\nLength = 2:38.516\nBPM = " + song.Bpm, 22, false, Color.white, TextAnchor.UpperLeft);

            Color[] colors = new[]
            {
                new Color(0.72f, 0.36f, 0.56f, 0.95f),
                new Color(0.42f, 0.58f, 0.72f, 0.9f),
                new Color(0.74f, 0.68f, 0.38f, 0.8f)
            };

            for (int i = 0; i < 70; i++)
            {
                float x = 258f + i * 10f;
                float h = 22f + Mathf.Abs(Mathf.Sin((i + song.Title.Length) * 0.71f)) * 138f;
                Color oldColor = GUI.color;
                GUI.color = colors[i % colors.Length];
                DrawPanel(new Rect(x, panel.yMax - 24f - h, 10f, h), GUI.color);
                GUI.color = oldColor;
            }
        }

        private static int sessionSafeDifficultyIndex(PrototypeSong song)
        {
            return song.Difficulties.Count > 2 ? 2 : 0;
        }

        private static void DrawScreenSpaceSongFirst(PrototypeSession session)
        {
            float width = Screen.width;
            float height = Screen.height;
            float margin = Mathf.Max(14f, width * 0.03f);
            float y = margin;
            PrototypeSong song = session.SelectedSong;
            PrototypeDifficulty difficulty = session.SelectedDifficulty;

            BoxText(new Rect(margin, y, width - margin * 2f, 82f), VariantLabel + "\nSONG-FIRST SELECTION PROTOTYPE", 28, true);
            y += 96f;
            BoxText(new Rect(margin, y, width - margin * 2f, 58f), "Phase: " + session.Phase + "  |  Song: " + song.Title + "  |  Difficulty: " + difficulty.Name + " " + difficulty.Level, 17, false);
            y += 72f;

            int count = session.Songs.Count;
            int previous = session.SelectedSongIndex == 0 ? count - 1 : session.SelectedSongIndex - 1;
            int next = session.SelectedSongIndex == count - 1 ? 0 : session.SelectedSongIndex + 1;
            float third = (width - margin * 2f - 16f) / 3f;
            BoxText(new Rect(margin, y, third, 88f), "Previous\n" + session.Songs[previous].Title, 15, false);
            BoxText(new Rect(margin + third + 8f, y, third, 88f), "SELECTED SONG\n" + song.Title + "\n" + song.Category, 17, true);
            BoxText(new Rect(margin + third * 2f + 16f, y, third, 88f), "Next\n" + session.Songs[next].Title, 15, false);
            y += 104f;

            BoxText(new Rect(margin, y, width - margin * 2f, 122f), "Song details\n" + song.Title + " / " + song.Artist + "\nBPM " + song.Bpm + "  " + song.Category + "  " + song.Badge + FlagText(song) + "\nRanks and DX-score-like values stay visible before choosing difficulty.", 16, false);
            y += 138f;

            string phaseTitle;
            string phaseBody;
            if (session.Phase == PrototypePhase.SongSelect)
            {
                phaseTitle = "BROWSE SONGS";
                phaseBody = "A3/A6 browse songs. A4 locks the song and moves to difficulty selection. Difficulty is secondary context here.";
            }
            else if (session.Phase == PrototypePhase.DifficultySelect)
            {
                phaseTitle = "DIFFICULTY SELECT";
                phaseBody = "Selected song remains visible. A3/A6 now change difficulty. A5 returns to the same song.";
            }
            else
            {
                phaseTitle = "CONFIRMED";
                phaseBody = "A5 returns to difficulty selection.";
            }

            BoxText(new Rect(margin, y, width - margin * 2f, 116f), phaseTitle + "\n" + phaseBody, 17, true);
            y += 132f;

            for (int i = 0; i < song.Difficulties.Count; i++)
            {
                PrototypeDifficulty diff = song.Difficulties[i];
                bool selected = i == session.SelectedDifficultyIndex;
                string status = diff.CanSelect ? diff.Rank + "  " + diff.DxScore : diff.Locked ? "LOCKED" : "UNAVAILABLE";
                BoxText(new Rect(margin, y, width - margin * 2f, 58f), (selected ? "> " : "  ") + diff.Name + " " + diff.Level + "    " + status, selected ? 17 : 15, selected);
                y += 66f;
            }

            string prompts = session.Phase == PrototypePhase.SongSelect
                ? "A3 Next Song    A6 Previous Song    A4 OK    A5 Category/Back"
                : session.Phase == PrototypePhase.DifficultySelect
                    ? "A3 Harder    A6 Easier    A4 Confirm    A5 Back to Songs"
                    : "A5 Back to Difficulty";

            BoxText(new Rect(margin, height - 116f, width - margin * 2f, 52f), prompts, 15, true);
            BoxText(new Rect(margin, height - 58f, width - margin * 2f, 40f), DiagnosticsLine + "    Left/Right switches variants", 13, false);
        }

        private static void BoxText(Rect rect, string text, int fontSize, bool bold)
        {
            GUIStyle style = GUI.skin.box;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = fontSize;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            style.wordWrap = true;
            style.normal.textColor = Color.white;
            GUI.Box(rect, new GUIContent(text), style);
        }

        private static void DrawVariantOne(PrototypeSession session)
        {
            DrawText(new Rect(48f, 936f, 984f, 32f), "Sinmai-like song-first carousel", 22, true, new Color(0.36f, 0.31f, 0.28f, 1f), TextAnchor.MiddleCenter);
            DrawCarouselImgui(session);
            DrawSongDetailsImgui(session.SelectedSong, new Rect(688f, 1078f, 314f, 474f));
            DrawDifficultiesImgui(session, 210f, 1558f);
        }

        private static void DrawVariantTwo(PrototypeSession session)
        {
            DrawText(new Rect(48f, 936f, 984f, 32f), "Majdata-native hybrid list", 22, true, new Color(0.36f, 0.31f, 0.28f, 1f), TextAnchor.MiddleCenter);
            DrawPanel(new Rect(76f, 1054f, 280f, 486f), new Color(0.34f, 0.28f, 0.24f, 0.92f));
            for (int i = 0; i < session.Songs.Count; i++)
            {
                PrototypeSong row = session.Songs[i];
                bool selected = i == session.SelectedSongIndex;
                Rect rowRect = new Rect(102f, 1084f + i * 74f, 228f, 56f);
                DrawPanel(rowRect, selected ? new Color(1f, 0.39f, 0.30f, 0.98f) : new Color(0.45f, 0.39f, 0.35f, 0.92f));
                DrawText(rowRect, row.Title + "\n" + row.Category, selected ? 17 : 15, selected, Color.white, TextAnchor.MiddleCenter);
            }

            DrawSelectedCover(session, new Rect(374f, 1168f, 356f, 356f), true);
            DrawSongDetailsImgui(session.SelectedSong, new Rect(720f, 1078f, 282f, 474f));
            DrawPhasePanelImgui(session, new Rect(374f, 1558f, 628f, 160f));
            DrawDifficultiesImgui(session, 178f, 1742f);
        }

        private static void DrawVariantThree(PrototypeSession session)
        {
            DrawText(new Rect(48f, 936f, 984f, 32f), "Compact fast-flow hybrid", 22, true, new Color(0.36f, 0.31f, 0.28f, 1f), TextAnchor.MiddleCenter);
            DrawPanel(new Rect(98f, 1096f, 884f, 360f), new Color(0.34f, 0.28f, 0.24f, 0.96f));
            DrawSelectedCover(session, new Rect(128f, 1130f, 312f, 312f), true);
            DrawText(new Rect(472f, 1132f, 444f, 72f), session.SelectedSong.Title, 34, true, Color.white, TextAnchor.MiddleLeft);
            DrawText(new Rect(472f, 1208f, 444f, 74f), session.SelectedSong.Artist + "\n" + session.SelectedSong.Category + " / BPM " + session.SelectedSong.Bpm + FlagText(session.SelectedSong), 22, false, Color.white, TextAnchor.MiddleLeft);
            string phase = session.Phase == PrototypePhase.SongSelect ? "Pick song first" : session.Phase == PrototypePhase.DifficultySelect ? "Now choose difficulty" : "Confirmed";
            DrawText(new Rect(472f, 1300f, 444f, 54f), phase, 28, true, new Color(1f, 0.78f, 0.36f, 1f), TextAnchor.MiddleLeft);
            DrawText(new Rect(472f, 1356f, 444f, 66f), session.SelectedDifficulty.Name + " " + session.SelectedDifficulty.Level + "    " + session.SelectedDifficulty.Rank + " " + session.SelectedDifficulty.DxScore, 24, true, Color.white, TextAnchor.MiddleLeft);
            DrawDifficultiesImgui(session, 178f, 1508f);
        }

        private static void DrawCarouselImgui(PrototypeSession session)
        {
            int count = session.Songs.Count;
            int previous = session.SelectedSongIndex == 0 ? count - 1 : session.SelectedSongIndex - 1;
            int next = session.SelectedSongIndex == count - 1 ? 0 : session.SelectedSongIndex + 1;

            DrawCover(new Rect(184f, 1118f, 148f, 148f), session.Songs[previous].Title, session.Songs[previous].Difficulties[0].Level, _blueCoverTexture);
            DrawCover(new Rect(358f, 1016f, 144f, 144f), session.Songs[previous].Title, session.Songs[previous].Difficulties[1].Level, _darkCoverTexture);
            DrawCover(new Rect(544f, 1018f, 144f, 144f), session.Songs[next].Title, session.Songs[next].Difficulties[1].Level, _pinkCoverTexture);
            DrawCover(new Rect(706f, 1138f, 140f, 140f), session.Songs[next].Title, "?", _blueCoverTexture);
            DrawCover(new Rect(212f, 1506f, 142f, 142f), session.Songs[previous].Title, session.Songs[previous].Difficulties[2].Level, _pinkCoverTexture);
            DrawCover(new Rect(392f, 1648f, 142f, 142f), session.Songs[next].Title, "10", _darkCoverTexture);
            DrawCover(new Rect(586f, 1644f, 142f, 142f), "Random", "9+", _blueCoverTexture);
            DrawSelectedCover(session, new Rect(332f, 1218f, 350f, 350f), true);
        }

        private static void DrawSongDetailsImgui(PrototypeSong song, Rect rect)
        {
            DrawPanel(rect, new Color(0.34f, 0.28f, 0.24f, 0.97f));
            DrawText(new Rect(rect.x + 28f, rect.y + 84f, rect.width - 42f, 66f), song.Title, 25, true, Color.white, TextAnchor.MiddleLeft);
            DrawText(new Rect(rect.x + 28f, rect.y + 150f, rect.width - 42f, 84f), song.Artist + "\n" + song.Category + "    BPM " + song.Bpm + FlagText(song), 20, true, Color.white, TextAnchor.MiddleLeft);
            DrawPanel(new Rect(rect.x + 28f, rect.y + 274f, rect.width - 56f, 3f), Color.white);
            DrawText(new Rect(rect.x + 28f, rect.y + 302f, rect.width - 56f, 92f), "Score facts stay visible while songs are browsed first.", 18, false, Color.white, TextAnchor.MiddleLeft);
        }

        private static void DrawDifficultiesImgui(PrototypeSession session, float startX, float y)
        {
            PrototypeSong song = session.SelectedSong;
            for (int i = 0; i < song.Difficulties.Count; i++)
            {
                PrototypeDifficulty diff = song.Difficulties[i];
                bool selected = i == session.SelectedDifficultyIndex;
                Rect rect = new Rect(startX + i * 138f, y, 118f, 86f);
                DrawPanel(rect, selected ? new Color(1f, 0.39f, 0.30f, 0.98f) : diff.CanSelect ? new Color(0.34f, 0.28f, 0.24f, 0.95f) : new Color(0.25f, 0.25f, 0.25f, 0.72f));
                DrawText(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 32f), diff.Name + " " + diff.Level, 16, true, Color.white, TextAnchor.MiddleCenter);
                DrawText(new Rect(rect.x + 8f, rect.y + 42f, rect.width - 16f, 32f), diff.CanSelect ? diff.Rank : diff.Locked ? "LOCKED" : "N/A", 15, true, Color.white, TextAnchor.MiddleCenter);
            }
        }

        private static void DrawPhasePanelImgui(PrototypeSession session, Rect rect)
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
                body = "Back returns to difficulty selection.";
            }

            DrawPanel(rect, new Color(0.34f, 0.28f, 0.24f, 0.96f));
            DrawText(new Rect(rect.x + 24f, rect.y + 20f, rect.width - 48f, 46f), title, 24, true, new Color(1f, 0.78f, 0.36f, 1f), TextAnchor.MiddleCenter);
            DrawText(new Rect(rect.x + 28f, rect.y + 74f, rect.width - 56f, rect.height - 88f), body, 18, false, Color.white, TextAnchor.MiddleCenter);
        }

        private static void DrawPromptsImgui(PrototypePhase phase)
        {
            string prompts = phase == PrototypePhase.SongSelect
                ? "A3 Next Song    A6 Previous Song    A4 OK    A5 Category/Back"
                : phase == PrototypePhase.DifficultySelect
                    ? "A3 Harder    A6 Easier    A4 Confirm    A5 Back to Songs"
                    : "A5 Back to Difficulty";

            DrawPanel(new Rect(86f, 1764f, 892f, 56f), new Color(0.34f, 0.28f, 0.24f, 0.96f));
            DrawText(new Rect(106f, 1772f, 852f, 40f), prompts, 20, true, Color.white, TextAnchor.MiddleCenter);
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

        private static void DrawSelectedCover(PrototypeSession session, Rect rect, bool showBadge)
        {
            DrawCircle(new Rect(rect.x - 16f, rect.y - 16f, rect.width + 32f, rect.height + 32f), _levelBadgeTexture);
            DrawCircle(rect, _selectedCoverTexture);
            DrawText(new Rect(rect.x + 28f, rect.y + rect.height * 0.42f, rect.width - 56f, 58f), session.SelectedSong.Title, 28, true, new Color(0.34f, 0.28f, 0.24f, 1f), TextAnchor.MiddleCenter);

            if (showBadge)
            {
                Rect badge = new Rect(rect.x - 20f, rect.y + rect.height * 0.66f, 96f, 96f);
                DrawCircle(badge, _levelBadgeTexture);
                DrawText(badge, session.SelectedDifficulty.Level, 28, true, Color.white, TextAnchor.MiddleCenter);
            }
        }

        private static void DrawCover(Rect rect, string title, string level, Texture2D texture)
        {
            DrawCircle(rect, texture);
            DrawText(new Rect(rect.x + 10f, rect.y + 36f, rect.width - 20f, 42f), title, 15, true, Color.white, TextAnchor.MiddleCenter);
            DrawPanel(new Rect(rect.x + 22f, rect.yMax - 28f, rect.width - 44f, 24f), new Color(0f, 0f, 0f, 0.86f));
            DrawText(new Rect(rect.x + 22f, rect.yMax - 29f, rect.width - 44f, 24f), level, 16, true, Color.white, TextAnchor.MiddleCenter);
        }

        private static void DrawTab(Rect rect, string text)
        {
            DrawPanel(rect, new Color(1f, 1f, 1f, 0.96f));
            DrawText(rect, text, 24, true, new Color(0.34f, 0.28f, 0.24f, 1f), TextAnchor.MiddleCenter);
        }

        private static void DrawCircle(Rect rect, Texture2D texture)
        {
            GUIStyle style = new GUIStyle();
            style.normal.background = texture;
            GUI.Box(rect, GUIContent.none, style);
        }

        private static void DrawPanel(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, _solidStyle);
            GUI.color = oldColor;
        }

        private static void DrawText(Rect rect, string text, int fontSize, bool bold, Color color, TextAnchor alignment)
        {
            GUIStyle style = bold ? _titleStyle : _bodyStyle;
            style.alignment = alignment;
            style.fontSize = fontSize;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            style.wordWrap = true;
            style.normal.textColor = color;
            GUI.Label(rect, new GUIContent(text), style);
        }

        private static void EnsureGuiResources()
        {
            if (_overlayTexture == null)
            {
                _overlayTexture = new Texture2D(1, 1);
                _overlayTexture.SetPixel(0, 0, OverlayColor);
                _overlayTexture.Apply();
            }

            if (_solidTexture == null)
            {
                _solidTexture = new Texture2D(1, 1);
                _solidTexture.SetPixel(0, 0, Color.white);
                _solidTexture.Apply();
            }

            if (_circleFieldTexture == null)
            {
                _circleFieldTexture = CreateCircleTexture(1024, new Color(1f, 0.985f, 0.95f, 0.98f), new Color(0.34f, 0.28f, 0.24f, 1f), 40);
                _selectedCoverTexture = CreateCircleTexture(512, new Color(0.74f, 0.93f, 0.96f, 1f), new Color(1f, 0.39f, 0.30f, 1f), 32);
                _blueCoverTexture = CreateCircleTexture(256, new Color(0.42f, 0.7f, 0.86f, 1f), new Color(0.28f, 0.24f, 0.22f, 1f), 10);
                _pinkCoverTexture = CreateCircleTexture(256, new Color(0.88f, 0.52f, 0.66f, 1f), new Color(0.28f, 0.24f, 0.22f, 1f), 10);
                _darkCoverTexture = CreateCircleTexture(256, new Color(0.12f, 0.12f, 0.14f, 1f), new Color(0.28f, 0.24f, 0.22f, 1f), 10);
                _levelBadgeTexture = CreateCircleTexture(128, new Color(1f, 0.39f, 0.30f, 1f), new Color(1f, 0.39f, 0.30f, 1f), 4);
            }

            if (_overlayStyle == null)
            {
                _overlayStyle = new GUIStyle();
                _overlayStyle.normal.background = _overlayTexture;
            }

            if (_solidStyle == null)
            {
                _solidStyle = new GUIStyle();
                _solidStyle.normal.background = _solidTexture;
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

        private static Texture2D CreateCircleTexture(int size, Color fill, Color border, int borderWidth)
        {
            Texture2D texture = new Texture2D(size, size);
            float center = (size - 1) * 0.5f;
            float outerRadius = center;
            float innerRadius = outerRadius - borderWidth;
            Color clear = new Color(0f, 0f, 0f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance > outerRadius)
                    {
                        texture.SetPixel(x, y, clear);
                    }
                    else if (distance >= innerRadius)
                    {
                        texture.SetPixel(x, y, border);
                    }
                    else
                    {
                        texture.SetPixel(x, y, fill);
                    }
                }
            }

            texture.Apply();
            return texture;
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
                case KeyCode.C:
                    return 0x43;
                case KeyCode.D:
                    return 0x44;
                case KeyCode.X:
                    return 0x58;
                case KeyCode.Z:
                    return 0x5A;
                default:
                    return 0;
            }
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int virtualKey);
    }

    internal sealed class PrototypeInputAdapter : IDisposable
    {
        private const int SensorA3 = 2;
        private const int SensorA4 = 3;
        private const int SensorA5 = 4;
        private const int SensorA6 = 5;
        private readonly object _lock = new object();
        private readonly bool[] _sensorStates = new bool[35];
        private readonly bool[] _latchedEdges = new bool[4];
        private readonly string _portName;
        private readonly int _baudRate;
        private Thread _thread;
        private volatile bool _running;
        private string _status;

        public static string LastInputLabel = "COM initializing";

        public PrototypeInputAdapter()
        {
            _portName = ResolveTouchPanelPortName();
            _baudRate = ResolveTouchPanelBaudRate();
            _status = "COM " + _portName + " opening";
            LastInputLabel = _status;
            _running = true;
            _thread = new Thread(SerialThreadMain);
            _thread.IsBackground = true;
            _thread.Name = "UI Prototype COM Input";
            _thread.Start();
        }

        public RawPrototypeInput Read()
        {
            bool serialA3;
            bool serialA4;
            bool serialA5;
            bool serialA6;
            lock (_lock)
            {
                serialA3 = _latchedEdges[0];
                serialA4 = _latchedEdges[1];
                serialA5 = _latchedEdges[2];
                serialA6 = _latchedEdges[3];
                for (int i = 0; i < _latchedEdges.Length; i++)
                {
                    _latchedEdges[i] = false;
                }
            }

            RawPrototypeInput keyboard = new RawPrototypeInput(
                PrototypeInputSource.KeyboardFallback,
                KeyboardInput.GetKeyDown(KeyCode.D),
                KeyboardInput.GetKeyDown(KeyCode.C) || KeyboardInput.GetKeyDown(KeyCode.Return) || KeyboardInput.GetKeyDown(KeyCode.Space),
                KeyboardInput.GetKeyDown(KeyCode.X) || KeyboardInput.GetKeyDown(KeyCode.Escape) || KeyboardInput.GetKeyDown(KeyCode.Backspace),
                KeyboardInput.GetKeyDown(KeyCode.Z));

            bool hasSerial = serialA3 || serialA4 || serialA5 || serialA6;
            bool hasKeyboard = keyboard.A3 || keyboard.A4 || keyboard.A5 || keyboard.A6;
            LastInputLabel = hasSerial
                ? _status + " edge"
                : hasKeyboard
                    ? "Keyboard fallback"
                    : _status;

            return new RawPrototypeInput(
                hasSerial ? PrototypeInputSource.MajdataReflection : PrototypeInputSource.KeyboardFallback,
                serialA3 || keyboard.A3,
                serialA4 || keyboard.A4,
                serialA5 || keyboard.A5,
                serialA6 || keyboard.A6);
        }

        public void Dispose()
        {
            _running = false;
        }

        private void SerialThreadMain()
        {
            while (_running)
            {
                try
                {
                    using (SerialPort serial = new SerialPort(_portName, _baudRate))
                    {
                        serial.ReadTimeout = 500;
                        serial.WriteTimeout = 500;
                        serial.Open();
                        InitializeTouchPanel(serial);
                        _status = "COM " + _portName + " connected";
                        MelonLogger.Msg("Prototype COM input connected to touch panel on " + _portName + " @ " + _baudRate + ".");

                        byte[] buffer = new byte[256];
                        while (_running)
                        {
                            int bytesToRead = serial.BytesToRead;
                            if (bytesToRead <= 0)
                            {
                                Thread.Sleep(1);
                                continue;
                            }

                            if (bytesToRead > buffer.Length)
                            {
                                bytesToRead = buffer.Length;
                            }

                            int read = serial.Read(buffer, 0, bytesToRead);
                            ParseSerialPacket(buffer, read);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _status = "COM " + _portName + " unavailable: " + ex.GetType().Name;
                    MelonLogger.Warning("Prototype COM input could not read " + _portName + ": " + ex.Message);
                    Thread.Sleep(1000);
                }
            }
        }

        private static void InitializeTouchPanel(SerialPort serial)
        {
            WriteAscii(serial, "{RSET}");
            WriteAscii(serial, "{HALT}");
            for (byte sensor = 0x41; sensor <= 0x62; sensor++)
            {
                WriteAscii(serial, "{LA" + (char)sensor + "r2}");
            }

            WriteAscii(serial, "{STAT}");
            serial.DiscardInBuffer();
        }

        private static void WriteAscii(SerialPort serial, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            serial.Write(bytes, 0, bytes.Length);
        }

        private void ParseSerialPacket(byte[] packet, int length)
        {
            for (int start = 0; start < length; start++)
            {
                if (packet[start] != (byte)'(')
                {
                    continue;
                }

                int end = -1;
                for (int i = start + 1; i < length; i++)
                {
                    if (packet[i] == (byte)')')
                    {
                        end = i;
                        break;
                    }
                }

                if (end < 0 || end - start - 1 != 7)
                {
                    continue;
                }

                bool[] parsed = new bool[35];
                int k = 0;
                for (int i = start + 1; i < end; i++)
                {
                    for (int bit = 0; bit < 5; bit++)
                    {
                        parsed[k] = (packet[i] & (1 << bit)) != 0;
                        k++;
                    }
                }

                LatchSensorEdges(parsed);
            }
        }

        private void LatchSensorEdges(bool[] parsed)
        {
            lock (_lock)
            {
                LatchOne(parsed, SensorA3, 0);
                LatchOne(parsed, SensorA4, 1);
                LatchOne(parsed, SensorA5, 2);
                LatchOne(parsed, SensorA6, 3);
                for (int i = 0; i < _sensorStates.Length; i++)
                {
                    _sensorStates[i] = parsed[i];
                }
            }
        }

        private void LatchOne(bool[] parsed, int sensorIndex, int latchIndex)
        {
            if (!_sensorStates[sensorIndex] && parsed[sensorIndex])
            {
                _latchedEdges[latchIndex] = true;
                _status = "COM " + _portName + " A" + (sensorIndex + 1);
                MelonLogger.Msg("Prototype COM input edge A" + (sensorIndex + 1));
            }
        }

        private static string ResolveTouchPanelPortName()
        {
            string settings = ReadSettings();
            string configured = ExtractJsonScalar(settings, "\"TouchPanel\"", "\"SerialPortOptions\"", "\"Port\"");
            if (!string.IsNullOrEmpty(configured) && configured != "null")
            {
                if (configured.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                {
                    return configured;
                }

                return "COM" + configured;
            }

            return "COM3";
        }

        private static int ResolveTouchPanelBaudRate()
        {
            string settings = ReadSettings();
            string configured = ExtractJsonScalar(settings, "\"TouchPanel\"", "\"SerialPortOptions\"", "\"BaudRate\"");
            int parsed;
            if (!string.IsNullOrEmpty(configured) && int.TryParse(configured, out parsed))
            {
                return parsed;
            }

            return 9600;
        }

        private static string ReadSettings()
        {
            try
            {
                string path = Path.Combine(Environment.CurrentDirectory, "settings.json");
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ExtractJsonScalar(string json, string section, string subsection, string key)
        {
            int sectionIndex = json.IndexOf(section, StringComparison.Ordinal);
            if (sectionIndex < 0)
            {
                return null;
            }

            int subsectionIndex = json.IndexOf(subsection, sectionIndex, StringComparison.Ordinal);
            if (subsectionIndex < 0)
            {
                return null;
            }

            int keyIndex = json.IndexOf(key, subsectionIndex, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                return null;
            }

            int colon = json.IndexOf(':', keyIndex);
            if (colon < 0)
            {
                return null;
            }

            int valueStart = colon + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
            {
                valueStart++;
            }

            if (valueStart >= json.Length)
            {
                return null;
            }

            if (json[valueStart] == '"')
            {
                int valueEnd = json.IndexOf('"', valueStart + 1);
                return valueEnd > valueStart ? json.Substring(valueStart + 1, valueEnd - valueStart - 1) : null;
            }

            int end = valueStart;
            while (end < json.Length && json[end] != ',' && json[end] != '}' && !char.IsWhiteSpace(json[end]))
            {
                end++;
            }

            return json.Substring(valueStart, end - valueStart);
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
            Time.timeScale = 1f;
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
            MelonLogger.Msg("UI prototype canvas fallback disabled; IMGUI is the authoritative prototype renderer.");
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
