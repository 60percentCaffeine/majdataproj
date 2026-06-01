using MelonLoader;
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
        private GameObject _overlayObject;

        public override void OnApplicationStart()
        {
            MelonLogger.Msg(ActivationMessage);
            EnsureOverlayObject();
        }

        public override void OnUpdate()
        {
            EnsureOverlayObject();
            Time.timeScale = 0f;
        }

        public override void OnGUI()
        {
            RenderOverlay();
        }

        public override void OnApplicationQuit()
        {
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

        private void EnsureOverlayObject()
        {
            if (_overlayObject != null)
            {
                return;
            }

            _overlayObject = new GameObject("UI Prototype Template Overlay");
            Object.DontDestroyOnLoad(_overlayObject);
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
            Object.DontDestroyOnLoad(_runtimeOverlayObject);
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
    }

    public sealed class PrototypeOverlayBehaviour : MonoBehaviour
    {
        private GameObject _canvasObject;
        private bool _loggedUpdate;

        public void Awake()
        {
            MelonLogger.Msg("UI prototype overlay behaviour awake.");
            InstallCanvasFallback();
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
            Object.DontDestroyOnLoad(_canvasObject);

            Canvas canvas = _canvasObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32767;
            _canvasObject.AddComponent<CanvasScaler>();
            _canvasObject.AddComponent<GraphicRaycaster>();

            GameObject panelObject = new GameObject("Full Screen Prototype Blocker");
            panelObject.transform.SetParent(_canvasObject.transform, false);
            Image panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0.02f, 0.02f, 0.025f, 0.98f);
            Stretch(panel.rectTransform);

            AddText("Title", "MAJDATA UI PROTOTYPE", 72, FontStyle.Bold, new Vector2(0f, 170f), new Vector2(1700f, 120f));
            AddText("Subtitle", "Template mod takeover is active", 34, FontStyle.Normal, new Vector2(0f, 80f), new Vector2(1700f, 80f));
            AddText("Body", "This placeholder intentionally covers the normal game screen.", 42, FontStyle.Normal, new Vector2(0f, -70f), new Vector2(1500f, 120f));
            AddText("Footer", "Prototype-only MelonLoader mod - remove the DLL to restore normal MajdataPlay behavior.", 28, FontStyle.Normal, new Vector2(0f, -410f), new Vector2(1700f, 80f));
            MelonLogger.Msg("UI prototype canvas placeholder installed.");
        }

        private void AddText(string name, string value, int size, FontStyle style, Vector2 position, Vector2 dimensions)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(_canvasObject.transform, false);
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
