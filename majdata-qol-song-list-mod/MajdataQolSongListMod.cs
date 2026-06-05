using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MajdataPlay;
using MajdataPlay.Collections;
using MajdataPlay.Scenes.List;
using MajdataPlay.Scenes.Setting;
using MajdataPlay.Settings;
using MajdataQolSongListMod.Core;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(MajdataQolSongListMod.MajdataQolSongListMod), "Majdata QoL Song List Mod", "0.1.0", "user0")]
[assembly: MelonGame]
[assembly: HarmonyDontPatchAll]

namespace MajdataQolSongListMod
{
    public sealed class MajdataQolSongListMod : MelonMod
    {
        private QolRuntimeBridge _bridge;
        private SynchronizationContext _unityContext;
        private Timer _bridgeTimer;

        static MajdataQolSongListMod()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveSupportAssembly;
        }

        public override void OnApplicationStart()
        {
            QolSongListModLogic logic = new QolSongListModLogic();
            MelonLogger.Msg(logic.StartupMessage());
            _bridge = new QolRuntimeBridge(message => MelonLogger.Msg(message), error => MelonLogger.Error(error));
            _unityContext = SynchronizationContext.Current;
            if (_unityContext != null)
            {
                _bridgeTimer = new Timer(PostBridgeUpdate, null, 250, 250);
            }
            QolRuntimeBridgeDriver.Install(_bridge);
            MelonLogger.Msg("Majdata QoL Song List Mod active with default folder behavior preserved.");
        }

        public override void OnUpdate()
        {
            if (_bridge != null)
            {
                _bridge.Update();
            }
        }

        public override void OnApplicationQuit()
        {
            if (_bridgeTimer != null)
            {
                _bridgeTimer.Dispose();
                _bridgeTimer = null;
            }
        }

        private void PostBridgeUpdate(object state)
        {
            SynchronizationContext context = _unityContext;
            if (context == null || _bridge == null)
            {
                return;
            }

            context.Post(_ => _bridge.Update(), null);
        }

        private static Assembly ResolveSupportAssembly(object sender, ResolveEventArgs args)
        {
            AssemblyName name = new AssemblyName(args.Name);
            if (name.Name != "MajdataQolSongListMod.Core")
            {
                return null;
            }

            string path = Path.Combine(Environment.CurrentDirectory, "Mods", "MajdataQolSongListModLib", "MajdataQolSongListMod.Core.dll");
            if (!File.Exists(path))
            {
                return null;
            }

            return Assembly.LoadFrom(path);
        }
    }

    public sealed class QolRuntimeBridgeDriver : MonoBehaviour
    {
        private QolRuntimeBridge _bridge;

        public static void Install(QolRuntimeBridge bridge)
        {
            GameObject obj = new GameObject("MajdataQolSongListMod.RuntimeBridge");
            Object.DontDestroyOnLoad(obj);
            QolRuntimeBridgeDriver driver = obj.AddComponent<QolRuntimeBridgeDriver>();
            driver._bridge = bridge;
        }

        private void Update()
        {
            if (_bridge != null)
            {
                _bridge.Update();
            }
        }
    }

    public sealed class MapListRuntimeSettings
    {
        [OptionName("Difficulty Filter")]
        [Description("Difficulty Filter Description")]
        public DifficultyCountFilter DifficultyFilter { get; set; }

        [OptionName("Sorting")]
        [Description("Sorting Description")]
        public MapListSortMode Sorting { get; set; }

        [OptionName("Grouping")]
        [Description("Grouping Description")]
        public MapListGroupingMode Grouping { get; set; }

        [OptionName("Downloaded Songs Filter")]
        [Description("Downloaded Songs Filter Description")]
        public DownloadedSongsFilter DownloadedSongsFilter { get; set; }

        public MapListRuntimeSettings()
        {
            MapListSettings defaults = MapListSettings.Defaults();
            DifficultyFilter = defaults.DifficultyFilter;
            Sorting = defaults.Sorting;
            Grouping = defaults.Grouping;
            DownloadedSongsFilter = defaults.DownloadedSongsFilter;
        }

        public MapListSettings Snapshot()
        {
            return new MapListSettings(DifficultyFilter, Sorting, Grouping, DownloadedSongsFilter);
        }
    }

    public sealed class QolRuntimeBridge
    {
        private const string MapListMenuName = "Map List";
        private const string RandomRecommendedName = "Random Recommended";
        private const string RandomRecommendedTileText = "Random\nRecommended";

        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] DifficultyBracketOrder = { "Easy", "Basic", "Advance", "Expert", "Master", "ReMaster", "UTAGE", "Other" };

        private readonly Action<string> _log;
        private readonly Action<string> _error;
        private readonly MapListRuntimeSettings _runtimeSettings = new MapListRuntimeSettings();
        private readonly MapListSettingsGroup _settingsGroup = MapListSettingsBridge.BuildGroup();

        private SettingManager _patchedSettingManager;
        private SongCollection[] _baseCollections;
        private SongCollection[] _lastAppliedCollections;
        private MapListSettings _lastAppliedSettings;
        private int _lastAppliedDifficulty = -1;
        private string _lastSceneName = string.Empty;
        private bool _reportedReady;
        private bool _reportedRandom;
        private int _frame;

        public QolRuntimeBridge(Action<string> log, Action<string> error)
        {
            _log = log;
            _error = error;
            Active = this;
        }

        public static QolRuntimeBridge Active { get; private set; }

        public static string DiagnosticsSnapshot()
        {
            string[] names = SongStorage.Collections.Select(collection => collection.Name).ToArray();
            bool hasRandom = names.Any(name => string.Equals(name, RandomRecommendedName, StringComparison.Ordinal));
            return "collections=" + string.Join("|", names) + "; hasRandomRecommended=" + hasRandom;
        }

        public static bool SetGroupingModeForDiagnostics(string groupingMode)
        {
            if (Active == null)
            {
                return false;
            }

            MapListGroupingMode parsed;
            if (!Enum.TryParse(groupingMode, out parsed))
            {
                return false;
            }

            Active._runtimeSettings.Grouping = parsed;
            return true;
        }

        public void Update()
        {
            _frame++;
            if (_frame % 5 != 0)
            {
                PatchRandomRecommendedTiles();
                return;
            }

            try
            {
                string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
                if (!string.Equals(sceneName, _lastSceneName, StringComparison.Ordinal))
                {
                    _lastSceneName = sceneName;
                    _patchedSettingManager = null;
                }

                EnsureCollectionsApplied();
                PatchSettingScene();
                PatchRandomRecommendedTiles();

                if (!_reportedReady)
                {
                    _reportedReady = true;
                    _log("Map List settings bridge ready: Map List, Game");
                }
            }
            catch (Exception ex)
            {
                _error("Majdata QoL Song List runtime bridge failed: " + ex);
            }
        }

        private void PatchSettingScene()
        {
            SettingManager manager = Object.FindObjectOfType<SettingManager>();
            if (manager == null)
            {
                return;
            }

            Menu[] menus = GetMenus(manager);
            if (menus.Length == 0)
            {
                return;
            }

            if (!ReferenceEquals(_patchedSettingManager, manager) || !menus.Any(menu => menu != null && menu.Name == MapListMenuName))
            {
                InjectMapListMenu(manager, menus);
                _patchedSettingManager = manager;
            }

            PatchMapListOptionText(manager);
        }

        private void InjectMapListMenu(SettingManager manager, Menu[] menus)
        {
            if (menus.Any(menu => menu != null && menu.Name == MapListMenuName))
            {
                return;
            }

            GameObject menuObject = Object.Instantiate(manager.menuPrefab, manager.transform);
            menuObject.name = MapListMenuName;
            Menu menu = menuObject.GetComponent<Menu>();
            menu.Name = MapListMenuName;
            menu.SubOptionObject = _runtimeSettings;
            menu.Init();
            SetMenuTitle(menu, _settingsGroup.Title);

            List<Menu> ordered = new List<Menu>();
            bool inserted = false;
            foreach (Menu existing in menus)
            {
                if (existing == null)
                {
                    continue;
                }

                if (!inserted && existing.Name == "Game")
                {
                    ordered.Add(menu);
                    inserted = true;
                }

                ordered.Add(existing);
            }

            if (!inserted)
            {
                ordered.Insert(0, menu);
            }

            SetPrivateField(manager, "menus", ordered.ToArray());
            SetPrivateField(manager, "<Index>k__BackingField", 0);
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].gameObject.SetActive(i == 0);
            }

            PatchMapListOptionText(manager);
            _log("Map List settings group injected before Game.");
        }

        private void PatchMapListOptionText(SettingManager manager)
        {
            Menu[] menus = GetMenus(manager);
            Menu mapMenu = menus.FirstOrDefault(menu => menu != null && menu.Name == MapListMenuName);
            if (mapMenu == null)
            {
                return;
            }

            SetMenuTitle(mapMenu, _settingsGroup.Title);
            Option[] options = GetPrivateField<Option[]>(mapMenu, "_options") ?? new Option[0];
            Dictionary<string, MapListSettingCard> cards = _settingsGroup.Cards.ToDictionary(card => card.PropertyName, card => card);

            foreach (Option option in options)
            {
                if (option == null || option.PropertyInfo == null)
                {
                    continue;
                }

                MapListSettingCard card;
                if (!cards.TryGetValue(option.PropertyInfo.Name, out card))
                {
                    continue;
                }

                SetOptionText(option, "_nameText", card.Label);
                SetOptionText(option, "_descriptionText", card.Description);
                SetOptionText(option, "_valueText", CurrentLabel(option.PropertyInfo.Name));
            }
        }

        private void EnsureCollectionsApplied()
        {
            SongCollection[] current = SongStorage.Collections;
            if (current == null || current.Length == 0)
            {
                return;
            }

            CaptureBaseCollections(current);

            int selectedDifficulty = SelectedDifficultyIndex();
            MapListSettings settings = _runtimeSettings.Snapshot();
            bool needsApply =
                _lastAppliedCollections == null ||
                _lastAppliedSettings == null ||
                settings.DifficultyFilter != _lastAppliedSettings.DifficultyFilter ||
                settings.Grouping != _lastAppliedSettings.Grouping ||
                settings.DownloadedSongsFilter != _lastAppliedSettings.DownloadedSongsFilter ||
                selectedDifficulty != _lastAppliedDifficulty;

            if (!needsApply)
            {
                return;
            }

            SongCollection[] next = BuildCollections(settings, selectedDifficulty);
            SetSongStorageCollections(next);
            _lastAppliedCollections = next;
            _lastAppliedSettings = settings;
            _lastAppliedDifficulty = selectedDifficulty;

            if (!_reportedRandom && next.Any(collection => collection.Name == RandomRecommendedName))
            {
                _reportedRandom = true;
                _log("Random Recommended collection ensured.");
            }

            _log("QoL grouping mode=" + MapListOptionLabels.For(settings.Grouping));
        }

        private void CaptureBaseCollections(SongCollection[] current)
        {
            if (_baseCollections != null && _baseCollections.Length != 0)
            {
                return;
            }

            SongCollection[] withoutQol = current
                .Where(collection => collection != null && collection.Name != RandomRecommendedName)
                .ToArray();
            if (withoutQol.Length == 0)
            {
                return;
            }

            _baseCollections = withoutQol;
        }

        private SongCollection[] BuildCollections(MapListSettings settings, int selectedDifficulty)
        {
            SongCollection[] source = _baseCollections ?? SongStorage.Collections;
            if (settings.Grouping == MapListGroupingMode.Default)
            {
                return AppendRandomRecommended(source);
            }

            List<ISongDetail> songs = AllSongs(source);
            IEnumerable<SongCollection> grouped;
            switch (settings.Grouping)
            {
                case MapListGroupingMode.DifficultyBracket:
                    grouped = DifficultyBracketOrder
                        .Select(name => new SongCollection(name, songs.Where(song => DifficultyBuckets(song).Contains(name)).ToArray()))
                        .Where(collection => collection.Count > 0 || collection.Name == "Other");
                    break;
                case MapListGroupingMode.DifficultyLevel:
                    grouped = songs
                        .GroupBy(song => LevelBucket(song, selectedDifficulty))
                        .OrderBy(group => LevelSortKey(group.Key))
                        .Select(group => new SongCollection(group.Key, group.ToArray()));
                    break;
                case MapListGroupingMode.Title:
                    grouped = songs
                        .GroupBy(song => TitleBucket(song.Title))
                        .OrderBy(group => group.Key == "#" ? "0" : group.Key == "Other" ? "ZZZ" : group.Key)
                        .Select(group => new SongCollection(group.Key, group.ToArray()));
                    break;
                case MapListGroupingMode.Artist:
                    grouped = songs
                        .GroupBy(song => string.IsNullOrWhiteSpace(song.Artist) ? "Unknown Artist" : song.Artist.Trim())
                        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(group => new SongCollection(group.Key, group.ToArray()));
                    break;
                case MapListGroupingMode.Rank:
                    grouped = new[] { new SongCollection("No Play", songs.ToArray()) };
                    break;
                default:
                    grouped = source;
                    break;
            }

            return AppendRandomRecommended(grouped.ToArray());
        }

        private SongCollection[] AppendRandomRecommended(IEnumerable<SongCollection> collections)
        {
            List<SongCollection> result = new List<SongCollection>();
            foreach (SongCollection collection in collections ?? Enumerable.Empty<SongCollection>())
            {
                if (collection != null && collection.Name != RandomRecommendedName)
                {
                    result.Add(collection);
                }
            }

            int insertIndex = result.FindIndex(collection => collection.Name == "MyFavorites");
            if (insertIndex >= 0)
            {
                insertIndex++;
            }
            else
            {
                insertIndex = result.FindIndex(collection => collection.Name == "All");
                insertIndex = insertIndex >= 0 ? insertIndex + 1 : result.Count;
            }

            result.Insert(insertIndex, new SongCollection(RandomRecommendedName, new ISongDetail[0])
            {
                IsOnline = true,
                IsVirtual = true,
                Type = ChartStorageType.PlayList
            });
            return result.ToArray();
        }

        private static List<ISongDetail> AllSongs(IEnumerable<SongCollection> collections)
        {
            Dictionary<string, ISongDetail> byHash = new Dictionary<string, ISongDetail>(StringComparer.Ordinal);
            SongCollection all = collections.FirstOrDefault(collection => collection.Name == "All");
            IEnumerable<SongCollection> source = all == null ? collections : new[] { all };
            foreach (SongCollection collection in source)
            {
                foreach (ISongDetail song in collection.ToArray())
                {
                    if (!string.IsNullOrEmpty(song.Hash) && !byHash.ContainsKey(song.Hash))
                    {
                        byHash.Add(song.Hash, song);
                    }
                }
            }

            return byHash.Values.ToList();
        }

        private static HashSet<string> DifficultyBuckets(ISongDetail song)
        {
            HashSet<string> buckets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ReadOnlySpan<string> levels = song.Levels;
            for (int i = 0; i < levels.Length && i < DifficultyBracketOrder.Length - 1; i++)
            {
                if (!string.IsNullOrWhiteSpace(levels[i]))
                {
                    buckets.Add(DifficultyBracketOrder[i]);
                }
            }

            if (buckets.Count == 0)
            {
                buckets.Add("Other");
            }

            return buckets;
        }

        private static string LevelBucket(ISongDetail song, int selectedDifficulty)
        {
            ReadOnlySpan<string> levels = song.Levels;
            if (selectedDifficulty < 0 || selectedDifficulty >= levels.Length)
            {
                return "Other";
            }

            return LevelBucketizer.Bucketize(levels[selectedDifficulty]);
        }

        private static string TitleBucket(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "Other";
            }

            char c = char.ToUpperInvariant(title.Trim()[0]);
            if (c >= 'A' && c <= 'Z')
            {
                return c.ToString();
            }

            if (char.IsDigit(c))
            {
                return "#";
            }

            return "Other";
        }

        private static string LevelSortKey(string key)
        {
            if (key == "Other")
            {
                return "999";
            }

            string text = key.EndsWith("+", StringComparison.Ordinal) ? key.Substring(0, key.Length - 1) + ".7" : key;
            decimal value;
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
                ? value.ToString("000.0", CultureInfo.InvariantCulture)
                : key;
        }

        private static int SelectedDifficultyIndex()
        {
            try
            {
                CoverListDisplayer displayer = Object.FindObjectOfType<CoverListDisplayer>();
                if (displayer != null)
                {
                    return displayer.selectedDifficulty;
                }
            }
            catch
            {
            }

            return 0;
        }

        private void SetSongStorageCollections(SongCollection[] collections)
        {
            PropertyInfo property = typeof(SongStorage).GetProperty("Collections", StaticFlags);
            MethodInfo setter = property.GetSetMethod(true);
            setter.Invoke(null, new object[] { collections });
            if (SongStorage.CollectionIndex >= collections.Length)
            {
                SongStorage.CollectionIndex = 0;
            }
        }

        private void PatchRandomRecommendedTiles()
        {
            Type folderCoverType = typeof(CoverListDisplayer).Assembly.GetType("MajdataPlay.Scenes.List.FolderCoverSmallDisplayer");
            if (folderCoverType == null)
            {
                return;
            }

            Object[] displayers = Resources.FindObjectsOfTypeAll(folderCoverType);
            foreach (Object displayer in displayers)
            {
                if (displayer == null)
                {
                    continue;
                }

                SongCollection collection = GetPrivateField<SongCollection>(displayer, "_boundCollection");
                if (collection == null || collection.Name != RandomRecommendedName)
                {
                    continue;
                }

                TextMeshProUGUI folderText = GetPrivateField<TextMeshProUGUI>(displayer, "_folderText");
                if (folderText != null)
                {
                    folderText.text = RandomRecommendedTileText;
                    folderText.alignment = TextAlignmentOptions.Center;
                    folderText.enableAutoSizing = true;
                    folderText.fontSizeMin = 12f;
                    folderText.fontSizeMax = 18f;
                    folderText.overflowMode = TextOverflowModes.Overflow;
                }

                GameObject icon = GetPrivateField<GameObject>(displayer, "_icon");
                if (icon != null)
                {
                    icon.SetActive(true);
                }
            }
        }

        private string CurrentLabel(string propertyName)
        {
            switch (propertyName)
            {
                case "DifficultyFilter":
                    return MapListOptionLabels.For(_runtimeSettings.DifficultyFilter);
                case "Sorting":
                    return MapListOptionLabels.For(_runtimeSettings.Sorting);
                case "Grouping":
                    return MapListOptionLabels.For(_runtimeSettings.Grouping);
                case "DownloadedSongsFilter":
                    return MapListOptionLabels.For(_runtimeSettings.DownloadedSongsFilter);
                default:
                    return string.Empty;
            }
        }

        private static void SetMenuTitle(Menu menu, string text)
        {
            TextMeshPro title = GetPrivateField<TextMeshPro>(menu, "titleText");
            if (title != null)
            {
                title.text = text;
            }
        }

        private static void SetOptionText(Option option, string fieldName, string text)
        {
            TextMeshPro label = GetPrivateField<TextMeshPro>(option, fieldName);
            if (label != null)
            {
                label.text = text;
            }
        }

        private static Menu[] GetMenus(SettingManager manager)
        {
            return GetPrivateField<Menu[]>(manager, "menus") ?? new Menu[0];
        }

        private static T GetPrivateField<T>(object target, string fieldName)
            where T : class
        {
            if (target == null)
            {
                return null;
            }

            FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
            return field == null ? null : field.GetValue(target) as T;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceFlags);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
