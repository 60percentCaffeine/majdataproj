using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
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
using UnityEngine.UI;
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

    internal sealed class SelectedSongRuntimeMetadata
    {
        public SelectedSongRuntimeMetadata(string length, BpmFacet bpm, bool shouldHydrate)
        {
            Length = string.IsNullOrWhiteSpace(length) ? "--:--" : length.Trim();
            Bpm = bpm ?? BpmFacet.Pending();
            ShouldHydrate = shouldHydrate;
        }

        public string Length { get; private set; }

        public BpmFacet Bpm { get; private set; }

        public bool ShouldHydrate { get; private set; }
    }

    internal sealed class WebsiteRuntimeCollection
    {
        public WebsiteRuntimeCollection(string name, WebsiteCollectionKind kind, IEnumerable<string> hashes, int totalCount, IEnumerable<ISongDetail> preferredSongs = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Website Collection" : name.Trim();
            Kind = kind;
            Hashes = (hashes ?? Enumerable.Empty<string>()).Where(hash => !string.IsNullOrWhiteSpace(hash)).Select(hash => hash.Trim()).ToArray();
            TotalCount = Math.Max(totalCount, Hashes.Count);
            PreferredSongs = (preferredSongs ?? Enumerable.Empty<ISongDetail>()).Where(song => song != null && !string.IsNullOrWhiteSpace(song.Hash)).ToArray();
        }

        public string Name { get; private set; }

        public WebsiteCollectionKind Kind { get; private set; }

        public IReadOnlyList<string> Hashes { get; private set; }

        public IReadOnlyList<ISongDetail> PreferredSongs { get; private set; }

        public int TotalCount { get; private set; }
    }

    public sealed class QolRuntimeBridge
    {
        private const string MapListMenuName = "Map List";
        private const string RandomRecommendedName = "Random Recommended";
        private const string RandomRecommendedTileText = "Random\nRecommended";
        private const string MetadataLineName = "QoLSelectedSongMetadataLine";

        private static readonly BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] DifficultyBracketOrder = { "Easy", "Basic", "Advance", "Expert", "Master", "ReMaster", "UTAGE", "Other" };

        private readonly Action<string> _log;
        private readonly Action<string> _error;
        private readonly MapListRuntimeSettings _runtimeSettings = new MapListRuntimeSettings();
        private readonly MapListSettingsGroup _settingsGroup = MapListSettingsBridge.BuildGroup();
        private readonly HydrationScheduler _hydrationScheduler = new HydrationScheduler();
        private readonly Dictionary<string, SelectedSongRuntimeMetadata> _selectedSongMetadata = new Dictionary<string, SelectedSongRuntimeMetadata>(StringComparer.Ordinal);
        private readonly Dictionary<string, ScoreFacet> _scoreOverrides = new Dictionary<string, ScoreFacet>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _onlinePlayCountOverrides = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<WebsiteRuntimeCollection> _websiteCollections = new List<WebsiteRuntimeCollection>();
        private readonly object _selectedSongMetadataLock = new object();
        private readonly object _scoreOverrideLock = new object();
        private readonly object _visibleOnlinePlayCountCacheLock = new object();
        private readonly object _randomRecommendedLock = new object();
        private readonly object _websiteCollectionLock = new object();

        private SettingManager _patchedSettingManager;
        private SongCollection[] _baseCollections;
        private SongCollection[] _lastAppliedCollections;
        private MapListSettings _lastAppliedSettings;
        private int _lastAppliedDifficulty = -1;
        private string _lastSceneName = string.Empty;
        private bool _reportedReady;
        private bool _reportedRandom;
        private int _frame;
        private string _lastMetadataLine = string.Empty;
        private QolStatusOverlay _statusOverlay;
        private Dictionary<string, int> _visibleOnlinePlayCountByHashCache;
        private ISongDetail[] _randomRecommendedSongs = new ISongDetail[0];
        private int _randomRecommendedSeed = 17;
        private string _lastRandomRecommendedStatus = "Random Recommended empty";
        private string _lastWebsiteCollectionStatus = "Website collections not loaded";
        private string _lastKnownCollectionName = string.Empty;
        private string _lastKnownSongHash = string.Empty;
        private bool _restoreSelectionOnNextListScene;
        private bool _capturedStorageSelectionForScene;

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

        public static bool SetSortingModeForDiagnostics(string sortingMode)
        {
            if (Active == null)
            {
                return false;
            }

            MapListSortMode parsed;
            if (!Enum.TryParse(sortingMode, out parsed))
            {
                return false;
            }

            Active._runtimeSettings.Sorting = parsed;
            return true;
        }

        public static bool SetDifficultyFilterForDiagnostics(string difficultyFilter)
        {
            if (Active == null)
            {
                return false;
            }

            DifficultyCountFilter parsed;
            if (!Enum.TryParse(difficultyFilter, out parsed))
            {
                return false;
            }

            Active._runtimeSettings.DifficultyFilter = parsed;
            return true;
        }

        public static bool SetDownloadedSongsFilterForDiagnostics(string downloadedSongsFilter)
        {
            if (Active == null)
            {
                return false;
            }

            DownloadedSongsFilter parsed;
            if (!Enum.TryParse(downloadedSongsFilter, out parsed))
            {
                return false;
            }

            Active._runtimeSettings.DownloadedSongsFilter = parsed;
            return true;
        }

        public static bool ApplySettingsForDiagnostics()
        {
            if (Active == null)
            {
                return false;
            }

            Active.EnsureCollectionsApplied();
            return true;
        }

        public static bool ShowStatusForDiagnostics(string message)
        {
            if (Active == null)
            {
                return false;
            }

            Active.ShowStatus(message, 3.0f);
            return true;
        }

        public static bool SetSelectedSongMetadataForDiagnostics(string length, string bpm)
        {
            if (Active == null)
            {
                return false;
            }

            CoverListDisplayer list = ActiveCoverListDisplayer();
            ISongDetail song = list == null ? null : list.SelectedSong;
            if (song == null || string.IsNullOrWhiteSpace(song.Hash))
            {
                return false;
            }

            Active.SetSelectedSongMetadata(song.Hash, SelectedDifficultyIndex(), new SelectedSongRuntimeMetadata(
                string.IsNullOrWhiteSpace(length) ? "--:--" : length.Trim(),
                ParseDiagnosticBpm(bpm),
                false));
            return true;
        }

        public static bool ClearSelectedSongMetadataForDiagnostics()
        {
            if (Active == null)
            {
                return false;
            }

            lock (Active._selectedSongMetadataLock)
            {
                Active._selectedSongMetadata.Clear();
            }

            return true;
        }

        public static bool SetSelectedSongScoreForDiagnostics(double dxAccuracy, int playCount, string comboState, long dxScore)
        {
            if (Active == null)
            {
                return false;
            }

            CoverListDisplayer list = Object.FindObjectOfType<CoverListDisplayer>();
            ISongDetail song = list == null ? null : list.SelectedSong;
            if (song == null || string.IsNullOrWhiteSpace(song.Hash))
            {
                return false;
            }

            return SetSongScoreForDiagnostics(song.Hash, dxAccuracy, playCount, comboState, dxScore);
        }

        public static bool SetSongScoreForDiagnostics(string hash, double dxAccuracy, int playCount, string comboState, long dxScore)
        {
            if (Active == null || string.IsNullOrWhiteSpace(hash))
            {
                return false;
            }

            Active.SetScoreOverride(hash, RuntimeScoreFacetAdapter.FromRuntimeScore(dxAccuracy, playCount, comboState, dxScore));
            return true;
        }

        public static bool SetSongOnlinePlayCountForDiagnostics(string hash, int playCount)
        {
            if (Active == null || string.IsNullOrWhiteSpace(hash) || playCount < 0)
            {
                return false;
            }

            lock (Active._scoreOverrideLock)
            {
                Active._onlinePlayCountOverrides[hash] = playCount;
                Active._scoreOverrides[hash] = new ScoreFacet(null, playCount, false, false, null);
            }
            Active.ClearVisibleOnlinePlayCountCache();
            return true;
        }

        public static string RefreshRandomRecommendedForDiagnostics(bool forceFailure)
        {
            if (Active == null)
            {
                return "inactive";
            }

            Active.ShowStatus("Refreshing Random Recommended...", 0f);
            Active.RefreshRandomRecommended(forceFailure);
            Active.EnsureCollectionsApplied(force: true);
            Active.ShowStatus(Active._lastRandomRecommendedStatus, 3.0f);
            return Active._lastRandomRecommendedStatus;
        }

        public static string RandomRecommendedDiagnosticsSnapshot()
        {
            if (Active == null)
            {
                return "randomCount=0; status=inactive; hashes=";
            }

            ISongDetail[] rows;
            lock (Active._randomRecommendedLock)
            {
                rows = Active._randomRecommendedSongs.ToArray();
            }

            return "randomCount=" + rows.Length.ToString(CultureInfo.InvariantCulture) +
                "; status=" + Active._lastRandomRecommendedStatus +
                "; hashes=" + string.Join("|", rows.Select(song => song == null ? string.Empty : song.Hash).Where(hash => !string.IsNullOrWhiteSpace(hash)).ToArray());
        }

        public static string InstallWebsiteCollectionForDiagnostics(string name, string pipeDelimitedHashes, int totalCount)
        {
            if (Active == null || string.IsNullOrWhiteSpace(name))
            {
                return "inactive";
            }

            string[] hashes = (pipeDelimitedHashes ?? string.Empty)
                .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(hash => hash.Trim())
                .Where(hash => !string.IsNullOrWhiteSpace(hash))
                .ToArray();

            ISongDetail[] preferredSongs = Active.ResolvePreferredSongsForHashes(hashes);
            lock (Active._websiteCollectionLock)
            {
                Active._websiteCollections.RemoveAll(collection => string.Equals(collection.Name, name, StringComparison.OrdinalIgnoreCase));
                Active._websiteCollections.Add(new WebsiteRuntimeCollection(name.Trim(), WebsiteCollectionKind.Subscribed, hashes, totalCount, preferredSongs));
                Active._lastWebsiteCollectionStatus = "Website collections refreshed: " + name.Trim();
            }

            Active.EnsureCollectionsApplied(force: true);
            Active.ShowStatus(Active._lastWebsiteCollectionStatus, 3.0f);
            return Active._lastWebsiteCollectionStatus;
        }

        public static string SimulateWebsiteCollectionFailureForDiagnostics()
        {
            if (Active == null)
            {
                return "inactive";
            }

            lock (Active._websiteCollectionLock)
            {
                Active._lastWebsiteCollectionStatus = "Website collection refresh failed; retained cached collections";
            }

            Active.EnsureCollectionsApplied(force: true);
            Active.ShowStatus(Active._lastWebsiteCollectionStatus, 3.0f);
            return Active._lastWebsiteCollectionStatus;
        }

        public static string WebsiteCollectionDiagnosticsSnapshot()
        {
            if (Active == null)
            {
                return "websiteCount=0; status=inactive";
            }

            WebsiteRuntimeCollection[] collections;
            lock (Active._websiteCollectionLock)
            {
                collections = Active._websiteCollections.ToArray();
            }

            return "websiteCount=" + collections.Length.ToString(CultureInfo.InvariantCulture) +
                "; status=" + Active._lastWebsiteCollectionStatus +
                "; names=" + string.Join("|", collections.Select(collection => collection.Name).ToArray());
        }

        public static string UiDiagnosticsSnapshot()
        {
            string metadataLine = string.Empty;
            bool statusVisible = false;
            string statusText = string.Empty;
            if (Active != null)
            {
                metadataLine = Active._lastMetadataLine ?? string.Empty;
                if (Active._statusOverlay != null)
                {
                    statusVisible = Active._statusOverlay.IsVisible;
                    statusText = Active._statusOverlay.CurrentText;
                }
            }

            return "metadataLine=" + metadataLine + "; statusVisible=" + statusVisible + "; statusText=" + statusText;
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
                    _capturedStorageSelectionForScene = false;
                    if (string.Equals(sceneName, "List", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(_lastKnownSongHash))
                    {
                        _restoreSelectionOnNextListScene = true;
                    }
                }

                EnsureCollectionsApplied();
                PatchSettingScene();
                PatchSelectedSongMetadataLine();
                PatchStatusOverlay();
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

        private void EnsureCollectionsApplied(bool force = false)
        {
            SongCollection[] current = SongStorage.Collections;
            if (current == null || current.Length == 0)
            {
                return;
            }

            CaptureBaseCollections(current);
            if (!_restoreSelectionOnNextListScene)
            {
                if (string.Equals(SceneManager.GetActiveScene().name, "List", StringComparison.Ordinal))
                {
                    CaptureActiveSelectionIfApplied(current);
                }
                else if (string.Equals(SceneManager.GetActiveScene().name, "Game", StringComparison.Ordinal))
                {
                    CaptureStorageSelectionIfApplied(current);
                }
            }

            int selectedDifficulty = SelectedDifficultyIndex();
            MapListSettings settings = _runtimeSettings.Snapshot();
            bool storageWasReset = _lastAppliedCollections != null && !ReferenceEquals(current, _lastAppliedCollections) && string.Equals(SceneManager.GetActiveScene().name, "List", StringComparison.Ordinal);
            bool needsApply =
                force ||
                _lastAppliedCollections == null ||
                _lastAppliedSettings == null ||
                settings.DifficultyFilter != _lastAppliedSettings.DifficultyFilter ||
                settings.Sorting != _lastAppliedSettings.Sorting ||
                settings.Grouping != _lastAppliedSettings.Grouping ||
                settings.DownloadedSongsFilter != _lastAppliedSettings.DownloadedSongsFilter ||
                selectedDifficulty != _lastAppliedDifficulty ||
                storageWasReset;

            if (!needsApply)
            {
                if (_restoreSelectionOnNextListScene && string.Equals(SceneManager.GetActiveScene().name, "List", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(_lastKnownSongHash))
                {
                    RestoreLastKnownSelection(current);
                    SyncActiveCoverListCollections(current, _lastKnownSongHash);
                    _restoreSelectionOnNextListScene = !ActiveSelectionMatches(_lastKnownSongHash);
                }
                return;
            }

            ClearVisibleOnlinePlayCountCache();
            SongCollection[] next = BuildCollections(settings, selectedDifficulty);
            if (storageWasReset)
            {
                RestoreLastKnownSelection(next);
            }
            SetSongStorageCollections(next, storageWasReset ? _lastKnownSongHash : null);
            if (storageWasReset)
            {
                _restoreSelectionOnNextListScene = !ActiveSelectionMatches(_lastKnownSongHash);
            }
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

        private void CaptureActiveSelectionIfApplied(SongCollection[] current)
        {
            if (_lastAppliedCollections == null || !ReferenceEquals(current, _lastAppliedCollections))
            {
                return;
            }

            CoverListDisplayer list = ActiveCoverListDisplayer();
            if (list == null || !list.IsChartList)
            {
                return;
            }

            SongCollection collection = list.SelectedCollection;
            ISongDetail song = list.SelectedSong;
            if (collection == null || song == null || string.IsNullOrWhiteSpace(song.Hash))
            {
                return;
            }

            _lastKnownCollectionName = collection.Name ?? string.Empty;
            _lastKnownSongHash = song.Hash;
        }

        private void CaptureStorageSelectionIfApplied(SongCollection[] current)
        {
            if (_capturedStorageSelectionForScene || _lastAppliedCollections == null || !ReferenceEquals(current, _lastAppliedCollections) || SongStorage.CollectionIndex < 0 || SongStorage.CollectionIndex >= current.Length)
            {
                return;
            }

            SongCollection collection = current[SongStorage.CollectionIndex];
            if (collection == null || collection.Count == 0)
            {
                return;
            }

            ISongDetail song;
            try
            {
                song = collection.Current;
            }
            catch
            {
                song = null;
            }

            if (song == null || string.IsNullOrWhiteSpace(song.Hash))
            {
                return;
            }

            _lastKnownCollectionName = collection.Name ?? string.Empty;
            _lastKnownSongHash = song.Hash;
            _capturedStorageSelectionForScene = true;
        }

        private void RestoreLastKnownSelection(SongCollection[] collections)
        {
            if (collections == null || collections.Length == 0 || string.IsNullOrWhiteSpace(_lastKnownCollectionName))
            {
                return;
            }

            int collectionIndex = Array.FindIndex(collections, collection => collection != null && string.Equals(collection.Name, _lastKnownCollectionName, StringComparison.OrdinalIgnoreCase));
            if (collectionIndex < 0)
            {
                return;
            }

            SongStorage.CollectionIndex = collectionIndex;
            SongCollection collection = collections[collectionIndex];
            if (collection == null || collection.Count == 0 || string.IsNullOrWhiteSpace(_lastKnownSongHash))
            {
                return;
            }

            ISongDetail match = collection.ToArray().FirstOrDefault(song => song != null && string.Equals(song.Hash, _lastKnownSongHash, StringComparison.Ordinal));
            if (match != null)
            {
                collection.SetCursor(match);
            }
        }

        private void ClearVisibleOnlinePlayCountCache()
        {
            lock (_visibleOnlinePlayCountCacheLock)
            {
                _visibleOnlinePlayCountByHashCache = null;
            }
        }

        private SongCollection[] BuildCollections(MapListSettings settings, int selectedDifficulty)
        {
            SongCollection[] source = _baseCollections ?? SongStorage.Collections;
            if (settings.Grouping == MapListGroupingMode.Default)
            {
                SongCollection[] defaultCollections = HasActiveSongTransform(settings)
                    ? TransformCollections(source, settings, selectedDifficulty)
                    : source;
                return AppendRandomRecommended(AppendWebsiteCollections(defaultCollections, settings, selectedDifficulty));
            }

            List<ISongDetail> songs = ApplySongSettings(AllSongs(source), settings, selectedDifficulty).ToList();
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
                        .SelectMany(song => LevelBuckets(song).Select(bucket => new { Bucket = bucket, Song = song }))
                        .GroupBy(row => row.Bucket)
                        .OrderBy(group => LevelSortKey(group.Key))
                        .Select(group => new SongCollection(group.Key, SortDifficultyLevelGroupRows(group.Select(row => row.Song), settings.Sorting, selectedDifficulty)));
                    break;
                case MapListGroupingMode.Title:
                    grouped = songs
                        .GroupBy(song => TitleBucket(song.Title))
                        .OrderBy(group => group.Key == "#" ? "0" : group.Key == "Other" ? "ZZZ" : group.Key)
                        .Select(group => new SongCollection(group.Key, group.ToArray()));
                    break;
                case MapListGroupingMode.Artist:
                    Dictionary<string, List<ISongDetail>> artistGroups = new Dictionary<string, List<ISongDetail>>(StringComparer.OrdinalIgnoreCase);
                    foreach (ISongDetail song in songs)
                    {
                        string key = string.IsNullOrWhiteSpace(song.Artist) ? "Unknown Artist" : song.Artist.Trim();
                        List<ISongDetail> groupRows;
                        if (!artistGroups.TryGetValue(key, out groupRows))
                        {
                            groupRows = new List<ISongDetail>();
                            artistGroups.Add(key, groupRows);
                        }

                        groupRows.Add(song);
                    }

                    grouped = artistGroups
                        .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(group => new SongCollection(group.Key, group.Value.ToArray()));
                    break;
                case MapListGroupingMode.Rank:
                    grouped = songs
                        .GroupBy(song => RankFolder(song, selectedDifficulty))
                        .OrderBy(group => RankFolderSortKey(group.Key))
                        .Select(group => new SongCollection(group.Key, group.ToArray()));
                    break;
                default:
                    grouped = source;
                    break;
            }

            return AppendRandomRecommended(AppendWebsiteCollections(grouped.ToArray(), settings, selectedDifficulty));
        }

        private static bool HasActiveSongTransform(MapListSettings settings)
        {
            return settings.DifficultyFilter != DifficultyCountFilter.No ||
                settings.Sorting != MapListSortMode.Default ||
                settings.DownloadedSongsFilter != DownloadedSongsFilter.Mixed;
        }

        private static SongCollection[] TransformCollections(IEnumerable<SongCollection> collections, MapListSettings settings, int selectedDifficulty)
        {
            return (collections ?? Enumerable.Empty<SongCollection>())
                .Where(collection => collection != null)
                .Select(collection => CloneCollection(collection, ApplySongSettings(collection.ToArray(), settings, selectedDifficulty)))
                .ToArray();
        }

        private static SongCollection CloneCollection(SongCollection source, IEnumerable<ISongDetail> songs)
        {
            ISongDetail[] array = songs == null ? new ISongDetail[0] : songs.ToArray();
            SongCollection clone;
            if (!source.IsVirtual && !string.IsNullOrEmpty(source.Path) && Directory.Exists(source.Path))
            {
                clone = new SongCollection(source.Path, source.Name, array)
                {
                    IsOnline = source.IsOnline,
                    Type = source.Type
                };
            }
            else
            {
                clone = new SongCollection(source.Name, array)
                {
                    IsOnline = source.IsOnline,
                    Type = source.Type
                };
            }

            if (array.Length > 0)
            {
                int index = source.IsEmpty ? 0 : Array.FindIndex(array, song => song != null && source.Current != null && song.Hash == source.Current.Hash);
                clone.Index = index < 0 ? 0 : index;
            }

            return clone;
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

            ISongDetail[] recommendedRows = RandomRecommendedRows(result);
            result.Insert(insertIndex, new SongCollection(RandomRecommendedName, recommendedRows)
            {
                IsOnline = true,
                IsVirtual = true,
                Type = ChartStorageType.PlayList
            });
            return result.ToArray();
        }

        private SongCollection[] AppendWebsiteCollections(IEnumerable<SongCollection> collections, MapListSettings settings, int selectedDifficulty)
        {
            List<SongCollection> result = (collections ?? Enumerable.Empty<SongCollection>())
                .Where(collection => collection != null && !IsWebsiteCollectionName(collection.Name))
                .ToList();

            WebsiteRuntimeCollection[] websiteCollections;
            lock (_websiteCollectionLock)
            {
                websiteCollections = _websiteCollections.ToArray();
            }

            if (websiteCollections.Length == 0)
            {
                return result.ToArray();
            }

            List<ISongDetail> availableSongs = AllSongInstances(_baseCollections ?? SongStorage.Collections);
            foreach (WebsiteRuntimeCollection websiteCollection in websiteCollections)
            {
                WebsiteResolution resolution = ResolveWebsiteCollectionRows(websiteCollection, availableSongs, settings.DownloadedSongsFilter);
                ISongDetail[] rows = ApplySongSettings(resolution.Rows, settings, selectedDifficulty).ToArray();
                SongCollection collection = new SongCollection(websiteCollection.Name, rows)
                {
                    IsOnline = true,
                    IsVirtual = true,
                    Type = ChartStorageType.PlayList
                };
                result.Add(collection);
            }

            return result.ToArray();
        }

        private bool IsWebsiteCollectionName(string name)
        {
            lock (_websiteCollectionLock)
            {
                return _websiteCollections.Any(collection => string.Equals(collection.Name, name, StringComparison.OrdinalIgnoreCase));
            }
        }

        private ISongDetail[] ResolvePreferredSongsForHashes(IEnumerable<string> hashes)
        {
            Dictionary<string, ISongDetail> availableByHash = new Dictionary<string, ISongDetail>(StringComparer.Ordinal);
            foreach (ISongDetail song in AllSongInstances(_baseCollections ?? SongStorage.Collections))
            {
                if (!availableByHash.ContainsKey(song.Hash))
                {
                    availableByHash.Add(song.Hash, song);
                }
            }

            List<ISongDetail> result = new List<ISongDetail>();
            foreach (string hash in hashes ?? Enumerable.Empty<string>())
            {
                ISongDetail song;
                if (!string.IsNullOrWhiteSpace(hash) && availableByHash.TryGetValue(hash, out song))
                {
                    result.Add(song);
                }
            }

            return result.ToArray();
        }

        private static WebsiteResolution ResolveWebsiteCollectionRows(WebsiteRuntimeCollection collection, IEnumerable<ISongDetail> availableSongs, DownloadedSongsFilter scope)
        {
            Dictionary<string, ISongDetail> byHash = new Dictionary<string, ISongDetail>(StringComparer.Ordinal);
            foreach (ISongDetail song in availableSongs ?? Enumerable.Empty<ISongDetail>())
            {
                if (song == null || string.IsNullOrWhiteSpace(song.Hash) || !SourcePasses(song, scope))
                {
                    continue;
                }

                ISongDetail existing;
                if (!byHash.TryGetValue(song.Hash, out existing) || (existing.IsOnline && !song.IsOnline))
                {
                    byHash[song.Hash] = song;
                }
            }

            foreach (ISongDetail song in collection.PreferredSongs ?? new ISongDetail[0])
            {
                if (song != null && !string.IsNullOrWhiteSpace(song.Hash) && SourcePasses(song, scope))
                {
                    byHash[song.Hash] = song;
                }
            }

            List<ISongDetail> rows = new List<ISongDetail>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            int unresolved = 0;
            foreach (string hash in collection.Hashes)
            {
                ISongDetail song;
                if (!byHash.TryGetValue(hash, out song))
                {
                    unresolved++;
                    continue;
                }

                if (seen.Add(song.Hash))
                {
                    rows.Add(song);
                }
            }

            unresolved += Math.Max(0, collection.TotalCount - collection.Hashes.Count);
            return new WebsiteResolution(rows.ToArray(), collection.TotalCount, unresolved);
        }

        private sealed class WebsiteResolution
        {
            public WebsiteResolution(ISongDetail[] rows, int totalCount, int unresolvedCount)
            {
                Rows = rows ?? new ISongDetail[0];
                TotalCount = totalCount;
                UnresolvedCount = unresolvedCount;
            }

            public ISongDetail[] Rows { get; private set; }

            public int TotalCount { get; private set; }

            public int UnresolvedCount { get; private set; }
        }

        private ISongDetail[] RandomRecommendedRows(IEnumerable<SongCollection> availableCollections)
        {
            lock (_randomRecommendedLock)
            {
                if (_randomRecommendedSongs.Length == 0)
                {
                    _randomRecommendedSongs = PickLocalRecommendations(AllSongs(availableCollections), _randomRecommendedSeed).ToArray();
                    _lastRandomRecommendedStatus = _randomRecommendedSongs.Length == 0
                        ? "Random Recommended has no playable fallback songs"
                        : "Random Recommended populated from local fallback: " + _randomRecommendedSongs.Length.ToString(CultureInfo.InvariantCulture) + " songs";
                }

                return _randomRecommendedSongs.ToArray();
            }
        }

        private void RefreshRandomRecommended(bool forceFailure)
        {
            List<ISongDetail> availableSongs = AllSongs(_baseCollections ?? SongStorage.Collections);
            int seed = Interlocked.Increment(ref _randomRecommendedSeed);
            ISongDetail[] resolved = forceFailure ? new ISongDetail[0] : TryFetchMajdataNetRecommendations(availableSongs, seed);
            bool fromFallback = resolved.Length == 0;
            if (fromFallback)
            {
                resolved = PickLocalRecommendations(availableSongs, seed).ToArray();
            }

            lock (_randomRecommendedLock)
            {
                _randomRecommendedSongs = resolved;
                if (forceFailure)
                {
                    _lastRandomRecommendedStatus = "Random Recommended refresh failed; using local fallback: " + resolved.Length.ToString(CultureInfo.InvariantCulture) + " songs";
                }
                else if (fromFallback)
                {
                    _lastRandomRecommendedStatus = "Random Recommended refreshed from local fallback: " + resolved.Length.ToString(CultureInfo.InvariantCulture) + " songs";
                }
                else
                {
                    _lastRandomRecommendedStatus = "Random Recommended refreshed from MajdataNet: " + resolved.Length.ToString(CultureInfo.InvariantCulture) + " songs";
                }
            }
        }

        private static ISongDetail[] TryFetchMajdataNetRecommendations(IEnumerable<ISongDetail> availableSongs, int seed)
        {
            try
            {
                List<ISongDetail> songs = (availableSongs ?? Enumerable.Empty<ISongDetail>())
                    .Where(song => song != null && !string.IsNullOrWhiteSpace(song.Hash))
                    .ToList();
                Dictionary<string, ISongDetail> byHash = new Dictionary<string, ISongDetail>(StringComparer.Ordinal);
                foreach (ISongDetail song in songs)
                {
                    if (!byHash.ContainsKey(song.Hash) || (byHash[song.Hash].IsOnline && !song.IsOnline))
                    {
                        byHash[song.Hash] = song;
                    }
                }

                RandomRecommendationService service = new RandomRecommendationService(new MajdataNetAdapter("https://majdata.net", new HttpTextFetcher()));
                Task<MajdataNetResult<RandomRecommendationBatch>> fetchTask = Task.Run(() => service.BuildRecommendations(new RandomRecommendationRequest(seed, 24, false), null));
                if (!fetchTask.Wait(3000))
                {
                    return new ISongDetail[0];
                }

                MajdataNetResult<RandomRecommendationBatch> result = fetchTask.Result;
                if (!result.Success || result.Value == null)
                {
                    return new ISongDetail[0];
                }

                List<ISongDetail> resolved = new List<ISongDetail>();
                foreach (CatalogRow row in result.Value.Rows)
                {
                    ISongDetail song;
                    if (row != null && !string.IsNullOrWhiteSpace(row.Hash) && byHash.TryGetValue(row.Hash, out song))
                    {
                        resolved.Add(song);
                    }
                }

                return resolved.Take(12).ToArray();
            }
            catch
            {
                return new ISongDetail[0];
            }
        }

        private static IEnumerable<ISongDetail> PickLocalRecommendations(IEnumerable<ISongDetail> songs, int seed)
        {
            Dictionary<string, ISongDetail> byHash = new Dictionary<string, ISongDetail>(StringComparer.Ordinal);
            foreach (ISongDetail song in songs ?? Enumerable.Empty<ISongDetail>())
            {
                if (song == null || string.IsNullOrWhiteSpace(song.Hash))
                {
                    continue;
                }

                ISongDetail existing;
                if (!byHash.TryGetValue(song.Hash, out existing) || (existing.IsOnline && !song.IsOnline))
                {
                    byHash[song.Hash] = song;
                }
            }

            List<ISongDetail> shuffled = byHash.Values.ToList();
            System.Random random = new System.Random(seed);
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                ISongDetail current = shuffled[i];
                shuffled[i] = shuffled[swapIndex];
                shuffled[swapIndex] = current;
            }

            return shuffled.Take(12).ToArray();
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
                    if (string.IsNullOrEmpty(song.Hash))
                    {
                        continue;
                    }

                    ISongDetail existing;
                    if (!byHash.TryGetValue(song.Hash, out existing) || (existing.IsOnline && !song.IsOnline))
                    {
                        byHash[song.Hash] = song;
                    }
                }
            }

            return byHash.Values.ToList();
        }

        private static List<ISongDetail> AllSongInstances(IEnumerable<SongCollection> collections)
        {
            List<ISongDetail> result = new List<ISongDetail>();
            foreach (SongCollection collection in collections ?? Enumerable.Empty<SongCollection>())
            {
                if (collection == null)
                {
                    continue;
                }

                result.AddRange(collection.ToArray().Where(song => song != null && !string.IsNullOrWhiteSpace(song.Hash)));
            }

            return result;
        }

        private static IEnumerable<ISongDetail> ApplySongSettings(IEnumerable<ISongDetail> songs, MapListSettings settings, int selectedDifficulty)
        {
            IEnumerable<ISongDetail> filtered = (songs ?? Enumerable.Empty<ISongDetail>())
                .Where(song => song != null)
                .Where(song => SourcePasses(song, settings.DownloadedSongsFilter))
                .Where(song => DifficultyFilterPasses(song, settings.DifficultyFilter));

            return SortSongs(DeduplicateSongsByHash(filtered), settings.Sorting, selectedDifficulty);
        }

        private static IEnumerable<ISongDetail> DeduplicateSongsByHash(IEnumerable<ISongDetail> songs)
        {
            List<ISongDetail> result = new List<ISongDetail>();
            Dictionary<string, int> indexByHash = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (ISongDetail song in songs ?? Enumerable.Empty<ISongDetail>())
            {
                if (song == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(song.Hash))
                {
                    result.Add(song);
                    continue;
                }

                int index;
                if (!indexByHash.TryGetValue(song.Hash, out index))
                {
                    indexByHash.Add(song.Hash, result.Count);
                    result.Add(song);
                    continue;
                }

                if (result[index] != null && result[index].IsOnline && !song.IsOnline)
                {
                    result[index] = song;
                }
            }

            return result;
        }

        private static bool SourcePasses(ISongDetail song, DownloadedSongsFilter filter)
        {
            switch (filter)
            {
                case DownloadedSongsFilter.DownloadedOnly:
                    return !song.IsOnline;
                case DownloadedSongsFilter.OnlineOnly:
                    return song.IsOnline;
                default:
                    return true;
            }
        }

        private static bool DifficultyFilterPasses(ISongDetail song, DifficultyCountFilter filter)
        {
            int count = CountDifficulties(song);
            switch (filter)
            {
                case DifficultyCountFilter.MoreThan1:
                    return count > 1;
                case DifficultyCountFilter.MoreThan2:
                    return count > 2;
                case DifficultyCountFilter.MoreThan3:
                    return count > 3;
                default:
                    return true;
            }
        }

        private static ISongDetail[] SortSongs(IEnumerable<ISongDetail> songs, MapListSortMode sortMode, int selectedDifficulty)
        {
            ISongDetail[] array = (songs ?? Enumerable.Empty<ISongDetail>()).Where(song => song != null).ToArray();
            if (sortMode == MapListSortMode.Default)
            {
                return array;
            }

            Comparison<ISongDetail> comparison = SongComparison(sortMode, selectedDifficulty);
            return array.OrderBy(song => song, Comparer<ISongDetail>.Create(comparison)).ToArray();
        }

        private static ISongDetail[] SortDifficultyLevelGroupRows(IEnumerable<ISongDetail> songs, MapListSortMode sortMode, int selectedDifficulty)
        {
            ISongDetail[] array = (songs ?? Enumerable.Empty<ISongDetail>()).Where(song => song != null).ToArray();
            if (sortMode != MapListSortMode.Difficulty)
            {
                return array;
            }

            return array.OrderBy(song => song, Comparer<ISongDetail>.Create((left, right) =>
            {
                if (left.IsOnline != right.IsOnline)
                {
                    return left.IsOnline ? 1 : -1;
                }

                return CompareNullableAscending(LevelSortValue(left, selectedDifficulty), LevelSortValue(right, selectedDifficulty), SongTieBreak(left, right));
            })).ToArray();
        }

        private static Comparison<ISongDetail> SongComparison(MapListSortMode sortMode, int selectedDifficulty)
        {
            switch (sortMode)
            {
                case MapListSortMode.DateAdded:
                    return (left, right) => CompareDescending(left.Timestamp, right.Timestamp, SongTieBreak(left, right));
                case MapListSortMode.Difficulty:
                    return (left, right) => CompareNullableAscending(LevelSortValue(left, selectedDifficulty), LevelSortValue(right, selectedDifficulty), SourceThenSongTieBreak(left, right));
                case MapListSortMode.NoteDesigner:
                    return (left, right) => CompareText(DesignerForDifficulty(left, selectedDifficulty), DesignerForDifficulty(right, selectedDifficulty), SongTieBreak(left, right));
                case MapListSortMode.Title:
                    return (left, right) => CompareText(left.Title, right.Title, SongTieBreak(left, right));
                case MapListSortMode.Artist:
                    return (left, right) => CompareText(left.Artist, right.Artist, SongTieBreak(left, right));
                case MapListSortMode.PlayCount:
                    return (left, right) => CompareNullableDescending(ScoreForSong(left, selectedDifficulty).LocalPlayCount, ScoreForSong(right, selectedDifficulty).LocalPlayCount, SongTieBreak(left, right));
                case MapListSortMode.Rank:
                    return (left, right) => CompareNullableDescending(RankValue(ScoreForSong(left, selectedDifficulty).Rank), RankValue(ScoreForSong(right, selectedDifficulty).Rank), SongTieBreak(left, right));
                case MapListSortMode.ApFcRank:
                    return (left, right) => CompareNullableDescending(ApFcValue(ScoreForSong(left, selectedDifficulty)), ApFcValue(ScoreForSong(right, selectedDifficulty)), SongTieBreak(left, right));
                case MapListSortMode.DxScore:
                    return (left, right) => CompareNullableDescending(ScoreForSong(left, selectedDifficulty).DxScore, ScoreForSong(right, selectedDifficulty).DxScore, SongTieBreak(left, right));
                default:
                    return SongTieBreak;
            }
        }

        private void SetScoreOverride(string hash, ScoreFacet score)
        {
            if (string.IsNullOrWhiteSpace(hash) || score == null)
            {
                return;
            }

            lock (_scoreOverrideLock)
            {
                _scoreOverrides[hash] = score;
            }
        }

        private static ScoreFacet ScoreForSong(ISongDetail song, int selectedDifficulty)
        {
            if (song == null)
            {
                return ScoreFacet.Empty();
            }

            QolRuntimeBridge active = Active;
            if (active != null && !string.IsNullOrWhiteSpace(song.Hash))
            {
                lock (active._scoreOverrideLock)
                {
                    ScoreFacet overrideScore;
                    if (active._scoreOverrides.TryGetValue(song.Hash, out overrideScore))
                    {
                        return overrideScore;
                    }
                }
            }

            ScoreFacet runtimeScore = ReadRuntimeScore(song, selectedDifficulty);
            int? visibleOnlinePlayCount = ReadVisibleOnlinePlayCount(song);
            if (visibleOnlinePlayCount.HasValue)
            {
                return new ScoreFacet(runtimeScore.Rank, visibleOnlinePlayCount, runtimeScore.HasFullCombo, runtimeScore.HasAllPerfect, runtimeScore.DxScore);
            }

            return runtimeScore;
        }

        private static ScoreFacet ReadRuntimeScore(ISongDetail song, int selectedDifficulty)
        {
            try
            {
                Type scoreManager = typeof(SongStorage).Assembly.GetType("MajdataPlay.ScoreManager");
                Type chartLevel = typeof(SongStorage).Assembly.GetType("MajdataPlay.ChartLevel");
                if (scoreManager == null || chartLevel == null)
                {
                    return ScoreFacet.Empty();
                }

                MethodInfo getScore = scoreManager.GetMethod("GetScore", StaticFlags);
                if (getScore == null)
                {
                    return ScoreFacet.Empty();
                }

                object level = Enum.ToObject(chartLevel, selectedDifficulty);
                object score = getScore.Invoke(null, new object[] { song, level });
                if (score == null)
                {
                    return ScoreFacet.Empty();
                }

                object accurate = GetMemberValue(score, "Acc");
                double dxAccuracy;
                double? dx = TryDouble(GetMemberValue(accurate, "DX"), out dxAccuracy) ? dxAccuracy : (double?)null;
                long playCount;
                long? plays = TryLong(GetMemberValue(score, "PlayCount"), out playCount) ? playCount : (long?)null;
                long dxScore;
                long? scoreValue = TryLong(GetMemberValue(score, "DXScore"), out dxScore) ? dxScore : (long?)null;
                string comboState = Convert.ToString(GetMemberValue(score, "ComboState"), CultureInfo.InvariantCulture);
                return RuntimeScoreFacetAdapter.FromRuntimeScore(dx, plays.HasValue ? ClampToInt(plays.Value) : (int?)null, comboState, scoreValue);
            }
            catch
            {
                return ScoreFacet.Empty();
            }
        }

        private static int? ReadVisibleOnlinePlayCount(ISongDetail song)
        {
            if (song == null || !song.IsOnline)
            {
                return null;
            }

            QolRuntimeBridge active = Active;
            if (active != null && !string.IsNullOrWhiteSpace(song.Hash))
            {
                lock (active._scoreOverrideLock)
                {
                    int overridePlayCount;
                    if (active._onlinePlayCountOverrides.TryGetValue(song.Hash, out overridePlayCount))
                    {
                        return overridePlayCount;
                    }
                }
            }

            try
            {
                Type onlineType = typeof(SongStorage).Assembly.GetType("MajdataPlay.Net.Online");
                if (onlineType == null)
                {
                    return null;
                }

                MethodInfo getCachedResponse = onlineType.GetMethod("GetCachedResponse", StaticFlags);
                if (getCachedResponse == null)
                {
                    return null;
                }

                ParameterInfo[] parameters = getCachedResponse.GetParameters();
                if (parameters.Length != 1 || !parameters[0].ParameterType.IsAssignableFrom(song.GetType()))
                {
                    return ReadVisibleOnlinePlayCountByHash(onlineType, song.Hash);
                }

                object cachedResponse = getCachedResponse.Invoke(null, new object[] { song });
                int? directPlayCount = ReadPlayCountFromCachedResponse(cachedResponse);
                if (directPlayCount.HasValue)
                {
                    return directPlayCount;
                }

                return ReadVisibleOnlinePlayCountByHash(onlineType, song.Hash);
            }
            catch
            {
                return null;
            }
        }

        private static int? ReadPlayCountFromCachedResponse(object cachedResponse)
        {
            object interact = GetMemberValue(cachedResponse, "Interact");
            object response = GetMemberValue(interact, "Response");
            long plays;
            return TryLong(GetMemberValue(response, "Plays"), out plays) && plays > 0 ? ClampToInt(plays) : (int?)null;
        }

        private static int? ReadVisibleOnlinePlayCountByHash(Type onlineType, string hash)
        {
            QolRuntimeBridge active = Active;
            return active == null ? null : active.CachedVisibleOnlinePlayCountByHash(onlineType, hash);
        }

        private int? CachedVisibleOnlinePlayCountByHash(Type onlineType, string hash)
        {
            if (onlineType == null || string.IsNullOrWhiteSpace(hash))
            {
                return null;
            }

            lock (_visibleOnlinePlayCountCacheLock)
            {
                if (_visibleOnlinePlayCountByHashCache == null)
                {
                    _visibleOnlinePlayCountByHashCache = BuildVisibleOnlinePlayCountByHash(onlineType);
                }

                int playCount;
                return _visibleOnlinePlayCountByHashCache.TryGetValue(hash, out playCount) ? playCount : (int?)null;
            }
        }

        private static Dictionary<string, int> BuildVisibleOnlinePlayCountByHash(Type onlineType)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
            try
            {
                FieldInfo cachedResponsesField = onlineType.GetField("_cachedResponse", StaticFlags);
                object cachedResponsesObject = cachedResponsesField == null ? null : cachedResponsesField.GetValue(null);
                IDictionary dictionary = cachedResponsesObject as IDictionary;
                if (dictionary != null)
                {
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        AddCachedPlayCount(result, entry.Key as ISongDetail, entry.Value);
                    }

                    return result;
                }

                IEnumerable cachedResponses = cachedResponsesObject as IEnumerable;
                if (cachedResponses == null)
                {
                    return result;
                }

                foreach (object entry in cachedResponses)
                {
                    AddCachedPlayCount(result, GetMemberValue(entry, "Key") as ISongDetail, GetMemberValue(entry, "Value"));
                }
            }
            catch
            {
            }

            return result;
        }

        private static void AddCachedPlayCount(Dictionary<string, int> result, ISongDetail cachedSong, object cachedResponse)
        {
            if (result == null || cachedSong == null || string.IsNullOrWhiteSpace(cachedSong.Hash))
            {
                return;
            }

            int? playCount = ReadPlayCountFromCachedResponse(cachedResponse);
            if (playCount.HasValue)
            {
                result[cachedSong.Hash] = playCount.Value;
            }
        }

        private static string RankFolder(ISongDetail song, int selectedDifficulty)
        {
            string rank = ScoreForSong(song, selectedDifficulty).Rank;
            return string.IsNullOrWhiteSpace(rank) ? "No Play" : rank.Trim();
        }

        private static string RankFolderSortKey(string rank)
        {
            int? value = RankValue(rank);
            if (value.HasValue)
            {
                return (100 - value.Value).ToString("000", CultureInfo.InvariantCulture);
            }

            return string.Equals(rank, "No Play", StringComparison.OrdinalIgnoreCase) ? "999" : "998-" + rank;
        }

        private static int? RankValue(string rank)
        {
            if (string.IsNullOrWhiteSpace(rank))
            {
                return null;
            }

            switch (rank.Trim().ToUpperInvariant())
            {
                case "SSS+":
                    return 12;
                case "SSS":
                    return 11;
                case "SS+":
                    return 10;
                case "SS":
                    return 9;
                case "S+":
                    return 8;
                case "S":
                    return 7;
                case "AAA":
                    return 6;
                case "AA":
                    return 5;
                case "A":
                    return 4;
                case "BBB":
                    return 3;
                case "BB":
                    return 2;
                case "B":
                    return 1;
                case "C":
                    return 0;
                default:
                    return null;
            }
        }

        private static int? ApFcValue(ScoreFacet score)
        {
            if (score == null)
            {
                return null;
            }

            if (score.HasAllPerfect)
            {
                return 2;
            }

            return score.HasFullCombo ? 1 : (int?)null;
        }

        private static int SongTieBreak(ISongDetail left, ISongDetail right)
        {
            int title = StringComparer.OrdinalIgnoreCase.Compare(left.Title ?? string.Empty, right.Title ?? string.Empty);
            if (title != 0)
            {
                return title;
            }

            return StringComparer.OrdinalIgnoreCase.Compare(left.Hash ?? string.Empty, right.Hash ?? string.Empty);
        }

        private static int SourceThenSongTieBreak(ISongDetail left, ISongDetail right)
        {
            if (left != null && right != null && left.IsOnline != right.IsOnline)
            {
                return left.IsOnline ? 1 : -1;
            }

            return SongTieBreak(left, right);
        }

        private static int CompareText(string left, string right, int tieBreak)
        {
            bool leftKnown = !string.IsNullOrWhiteSpace(left);
            bool rightKnown = !string.IsNullOrWhiteSpace(right);
            if (leftKnown && !rightKnown)
            {
                return -1;
            }

            if (!leftKnown && rightKnown)
            {
                return 1;
            }

            if (!leftKnown && !rightKnown)
            {
                return tieBreak;
            }

            int compare = StringComparer.OrdinalIgnoreCase.Compare(left.Trim(), right.Trim());
            return compare != 0 ? compare : tieBreak;
        }

        private static int CompareDescending(DateTime left, DateTime right, int tieBreak)
        {
            int compare = right.CompareTo(left);
            return compare != 0 ? compare : tieBreak;
        }

        private static int CompareNullableAscending<T>(T? left, T? right, int tieBreak)
            where T : struct, IComparable<T>
        {
            if (left.HasValue && !right.HasValue)
            {
                return -1;
            }

            if (!left.HasValue && right.HasValue)
            {
                return 1;
            }

            if (!left.HasValue && !right.HasValue)
            {
                return tieBreak;
            }

            int compare = left.Value.CompareTo(right.Value);
            return compare != 0 ? compare : tieBreak;
        }

        private static int CompareNullableDescending<T>(T? left, T? right, int tieBreak)
            where T : struct, IComparable<T>
        {
            if (left.HasValue && !right.HasValue)
            {
                return -1;
            }

            if (!left.HasValue && right.HasValue)
            {
                return 1;
            }

            if (!left.HasValue && !right.HasValue)
            {
                return tieBreak;
            }

            int compare = right.Value.CompareTo(left.Value);
            return compare != 0 ? compare : tieBreak;
        }

        private static decimal? LevelSortValue(ISongDetail song, int selectedDifficulty)
        {
            string level = LevelForDifficulty(song, selectedDifficulty);
            if (string.IsNullOrWhiteSpace(level))
            {
                return null;
            }

            string text = level.Trim();
            bool hasPlus = text.EndsWith("+", StringComparison.Ordinal);
            if (hasPlus)
            {
                text = text.Substring(0, text.Length - 1).Trim();
            }

            decimal value;
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            {
                return null;
            }

            return hasPlus ? value + 0.7m : value;
        }

        private static string LevelForDifficulty(ISongDetail song, int selectedDifficulty)
        {
            ReadOnlySpan<string> levels = song.Levels;
            return selectedDifficulty >= 0 && selectedDifficulty < levels.Length ? levels[selectedDifficulty] : null;
        }

        private static string DesignerForDifficulty(ISongDetail song, int selectedDifficulty)
        {
            ReadOnlySpan<string> designers = song.Designers;
            if (selectedDifficulty >= 0 && selectedDifficulty < designers.Length && !string.IsNullOrWhiteSpace(designers[selectedDifficulty]))
            {
                return designers[selectedDifficulty];
            }

            for (int i = 0; i < designers.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(designers[i]))
                {
                    return designers[i];
                }
            }

            return null;
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

        private static IEnumerable<string> LevelBuckets(ISongDetail song)
        {
            HashSet<string> buckets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ReadOnlySpan<string> levels = song.Levels;
            for (int i = 0; i < levels.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(levels[i]))
                {
                    continue;
                }

                buckets.Add(LevelBucketizer.Bucketize(levels[i]));
            }

            if (buckets.Count == 0)
            {
                buckets.Add("Other");
            }

            return buckets;
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
                CoverListDisplayer displayer = ActiveCoverListDisplayer();
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

        private void SetSongStorageCollections(SongCollection[] collections, string preferredSongHash = null)
        {
            PropertyInfo property = typeof(SongStorage).GetProperty("Collections", StaticFlags);
            MethodInfo setter = property.GetSetMethod(true);
            setter.Invoke(null, new object[] { collections });
            if (SongStorage.CollectionIndex >= collections.Length)
            {
                SongStorage.CollectionIndex = 0;
            }

            SyncActiveCoverListCollections(collections, preferredSongHash);
        }

        private static void SyncActiveCoverListCollections(SongCollection[] collections, string preferredSongHash = null)
        {
            if (collections == null || collections.Length == 0)
            {
                return;
            }

            CoverListDisplayer displayer = ActiveCoverListDisplayer();
            if (displayer == null)
            {
                return;
            }

            if (SongStorage.CollectionIndex >= collections.Length)
            {
                SongStorage.CollectionIndex = 0;
            }

            bool wasChartList = displayer.IsChartList;
            bool wasDirList = displayer.IsDirList;
            bool switchedDirThroughSongList = false;
            ISongDetail selectedSong = PreferredSong(collections, SongStorage.CollectionIndex, preferredSongHash);
            if (selectedSong == null)
            {
                try
                {
                    selectedSong = wasChartList ? displayer.SelectedSong : SongStorage.WorkingCollection.Current;
                }
                catch
                {
                    selectedSong = null;
                }
            }

            if (wasChartList)
            {
                displayer.SwitchToDirList();
            }
            else if (wasDirList && CanSwitchCurrentCollectionToSongList(displayer))
            {
                displayer.SwitchToSongList();
                switchedDirThroughSongList = displayer.IsChartList;
            }

            PreserveCollectionCursor(collections, SongStorage.CollectionIndex, selectedSong);
            SongCollection[] easyCollections;
            SongCollection[] basicCollections;
            SongCollection[] advanceCollections;
            SongCollection[] expertCollections;
            SongCollection[] masterCollections;
            SongCollection[] reMasterCollections;
            SongCollection[] utageCollections;
            BuildDifficultyCollections(
                collections,
                out easyCollections,
                out basicCollections,
                out advanceCollections,
                out expertCollections,
                out masterCollections,
                out reMasterCollections,
                out utageCollections);

            SetPrivateField(displayer, "_collections", new ReadOnlyMemory<SongCollection>(collections));
            SetPrivateField(displayer, "_easySortedCollections", easyCollections);
            SetPrivateField(displayer, "_basicSortedCollections", basicCollections);
            SetPrivateField(displayer, "_advanceSortedCollections", advanceCollections);
            SetPrivateField(displayer, "_expertSortedCollections", expertCollections);
            SetPrivateField(displayer, "_masterSortedCollections", masterCollections);
            SetPrivateField(displayer, "_reMasterSortedCollections", reMasterCollections);
            SetPrivateField(displayer, "_utageSortedCollections", utageCollections);
            SetPrivateField(displayer, "_currentCollection", CollectionForDifficulty(displayer.selectedDifficulty, easyCollections, basicCollections, advanceCollections, expertCollections, masterCollections, reMasterCollections, utageCollections)[SongStorage.CollectionIndex]);

            if (wasChartList)
            {
                displayer.SwitchToSongList();
            }
            else if (switchedDirThroughSongList)
            {
                displayer.SwitchToDirList();
            }

            if (!string.IsNullOrWhiteSpace(preferredSongHash))
            {
                AlignActiveSongCursor(displayer, collections, preferredSongHash);
            }
        }

        private static void AlignActiveSongCursor(CoverListDisplayer displayer, SongCollection[] collections, string preferredSongHash)
        {
            if (displayer == null || collections == null || SongStorage.CollectionIndex < 0 || SongStorage.CollectionIndex >= collections.Length)
            {
                return;
            }

            SongCollection collection = collections[SongStorage.CollectionIndex];
            if (collection == null || collection.Count == 0)
            {
                return;
            }

            ISongDetail target = PreferredSong(collections, SongStorage.CollectionIndex, preferredSongHash) ?? collection.Current;
            if (target == null)
            {
                return;
            }

            collection.SetCursor(target);
            SetCursorInPrivateCollections(displayer, SongStorage.CollectionIndex, target);
            int index = Math.Max(0, Math.Min(collection.Index, collection.Count - 1));
            try
            {
                MethodInfo setCursor = typeof(CoverListDisplayer).GetMethod("SetCursor", InstanceFlags);
                if (setCursor != null)
                {
                    setCursor.Invoke(displayer, new object[] { target });
                    collection.SetCursor(target);
                    index = Math.Max(0, Math.Min(collection.Index, collection.Count - 1));
                }

                SetPrivateField(displayer, "_currentCollection", collection);
                MethodInfo slide = typeof(CoverListDisplayer).GetMethod("SlideListInternal", InstanceFlags);
                if (slide != null)
                {
                    slide.Invoke(displayer, new object[] { index });
                }
                SetPrivateField(displayer, "desiredListPos", index);
                SetPrivateField(displayer, "listPosReal", (float)index);
            }
            catch
            {
            }
        }

        private static void SetCursorInPrivateCollections(CoverListDisplayer displayer, int collectionIndex, ISongDetail target)
        {
            if (displayer == null || collectionIndex < 0 || target == null)
            {
                return;
            }

            string[] fields =
            {
                "_collections",
                "_easySortedCollections",
                "_basicSortedCollections",
                "_advanceSortedCollections",
                "_expertSortedCollections",
                "_masterSortedCollections",
                "_reMasterSortedCollections",
                "_utageSortedCollections"
            };

            foreach (string field in fields)
            {
                object value = GetPrivateField<object>(displayer, field);
                SongCollection collection = GetIndexedValue(value, collectionIndex) as SongCollection;
                if (collection != null && collection.Count > 0)
                {
                    collection.SetCursor(target);
                }
            }
        }

        private static bool ActiveSelectionMatches(string songHash)
        {
            if (string.IsNullOrWhiteSpace(songHash))
            {
                return true;
            }

            try
            {
                CoverListDisplayer list = ActiveCoverListDisplayer();
                ISongDetail selected = list == null ? null : list.SelectedSong;
                return selected != null && string.Equals(selected.Hash, songHash, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static bool CanSwitchCurrentCollectionToSongList(CoverListDisplayer displayer)
        {
            SongCollection current = GetPrivateField<SongCollection>(displayer, "_currentCollection");
            return current != null && current.Count > 0 && current.Type != ChartStorageType.Dan;
        }

        private static ISongDetail PreferredSong(SongCollection[] collections, int collectionIndex, string preferredSongHash)
        {
            if (collections == null || collectionIndex < 0 || collectionIndex >= collections.Length || string.IsNullOrWhiteSpace(preferredSongHash))
            {
                return null;
            }

            SongCollection collection = collections[collectionIndex];
            if (collection == null || collection.Count == 0)
            {
                return null;
            }

            return collection.ToArray().FirstOrDefault(song => song != null && string.Equals(song.Hash, preferredSongHash, StringComparison.Ordinal));
        }

        private static void PreserveCollectionCursor(SongCollection[] collections, int collectionIndex, ISongDetail selectedSong)
        {
            if (collections == null || collectionIndex < 0 || collectionIndex >= collections.Length || selectedSong == null || string.IsNullOrWhiteSpace(selectedSong.Hash))
            {
                return;
            }

            SongCollection collection = collections[collectionIndex];
            if (collection == null || collection.Count == 0)
            {
                return;
            }

            ISongDetail match = collection.ToArray().FirstOrDefault(song => song != null && string.Equals(song.Hash, selectedSong.Hash, StringComparison.Ordinal));
            if (match != null)
            {
                collection.SetCursor(match);
            }
        }

        private static void BuildDifficultyCollections(
            SongCollection[] collections,
            out SongCollection[] easyCollections,
            out SongCollection[] basicCollections,
            out SongCollection[] advanceCollections,
            out SongCollection[] expertCollections,
            out SongCollection[] masterCollections,
            out SongCollection[] reMasterCollections,
            out SongCollection[] utageCollections)
        {
            if (SongStorage.OrderBy.SortBy != SortType.ByRank)
            {
                easyCollections = collections;
                basicCollections = collections;
                advanceCollections = collections;
                expertCollections = collections;
                masterCollections = collections;
                reMasterCollections = collections;
                utageCollections = collections;
                return;
            }

            easyCollections = RankSortedCollections(collections, "Easy");
            basicCollections = RankSortedCollections(collections, "Basic");
            advanceCollections = RankSortedCollections(collections, "Advance");
            expertCollections = RankSortedCollections(collections, "Expert");
            masterCollections = RankSortedCollections(collections, "Master");
            reMasterCollections = RankSortedCollections(collections, "ReMaster");
            utageCollections = RankSortedCollections(collections, "UTAGE");
        }

        private static SongCollection[] RankSortedCollections(SongCollection[] collections, string scorePropertyName)
        {
            if (collections == null)
            {
                return new SongCollection[0];
            }

            SongCollection[] result = new SongCollection[collections.Length];
            for (int i = 0; i < collections.Length; i++)
            {
                SongCollection collection = collections[i];
                if (collection == null || collection.Count == 0 || collection.Type == ChartStorageType.Dan)
                {
                    result[i] = collection;
                    continue;
                }

                ISongDetail[] sorted = collection.ToArray()
                    .Select((song, index) => new
                    {
                        Song = song,
                        Index = index,
                        Accuracy = NativeRankAccuracy(song, scorePropertyName)
                    })
                    .OrderByDescending(item => item.Accuracy)
                    .ThenBy(item => item.Index)
                    .Select(item => item.Song)
                    .ToArray();
                result[i] = CloneCollection(collection, sorted);
            }

            return result;
        }

        private static double NativeRankAccuracy(ISongDetail song, string scorePropertyName)
        {
            if (song == null)
            {
                return 0d;
            }

            try
            {
                Type scoreManager = typeof(SongStorage).Assembly.GetType("MajdataPlay.ScoreManager");
                if (scoreManager == null)
                {
                    return 0d;
                }

                MethodInfo getSongScores = scoreManager.GetMethod("GetSongScores", StaticFlags);
                if (getSongScores == null)
                {
                    return 0d;
                }

                object scores = getSongScores.Invoke(null, new object[] { song });
                object score = scores == null ? null : GetMemberValue(scores, scorePropertyName);
                object accurate = score == null ? null : GetMemberValue(score, "Acc");
                string accuracyMember = IsClassicJudgeMode() ? "Classic" : "DX";
                double accuracy;
                return TryDouble(GetMemberValue(accurate, accuracyMember), out accuracy) ? accuracy : 0d;
            }
            catch
            {
                return 0d;
            }
        }

        private static bool IsClassicJudgeMode()
        {
            try
            {
                Type majEnv = typeof(SongStorage).Assembly.GetType("MajdataPlay.MajEnv");
                PropertyInfo settingsProperty = majEnv == null ? null : majEnv.GetProperty("Settings", StaticFlags);
                object settings = settingsProperty == null ? null : settingsProperty.GetValue(null, null);
                object judge = GetMemberValue(settings, "Judge");
                object mode = GetMemberValue(judge, "Mode");
                return string.Equals(Convert.ToString(mode, CultureInfo.InvariantCulture), "Classic", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static SongCollection[] CollectionForDifficulty(
            int selectedDifficulty,
            SongCollection[] easyCollections,
            SongCollection[] basicCollections,
            SongCollection[] advanceCollections,
            SongCollection[] expertCollections,
            SongCollection[] masterCollections,
            SongCollection[] reMasterCollections,
            SongCollection[] utageCollections)
        {
            switch ((ChartLevel)selectedDifficulty)
            {
                case ChartLevel.Easy:
                    return easyCollections;
                case ChartLevel.Basic:
                    return basicCollections;
                case ChartLevel.Advance:
                    return advanceCollections;
                case ChartLevel.Expert:
                    return expertCollections;
                case ChartLevel.Master:
                    return masterCollections;
                case ChartLevel.ReMaster:
                    return reMasterCollections;
                case ChartLevel.UTAGE:
                    return utageCollections;
                default:
                    return easyCollections;
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

        private void PatchSelectedSongMetadataLine()
        {
            CoverListDisplayer list = ActiveCoverListDisplayer();
            if (list == null || !list.IsChartList)
            {
                HideSelectedSongMetadataLine();
                return;
            }

            ISongDetail song = list.SelectedSong;
            if (song == null)
            {
                return;
            }

            CoverBigDisplayer big = Object.FindObjectOfType<CoverBigDisplayer>();
            if (big == null)
            {
                return;
            }

            TMP_Text artist = GetPrivateField<TMP_Text>(big, "_artist");
            TMP_Text charter = GetPrivateField<TMP_Text>(big, "_charter");
            TMP_Text archieveRate = GetPrivateField<TMP_Text>(big, "_archieveRate");
            TMP_Text rank = GetPrivateField<TMP_Text>(big, "_rank");
            if (artist == null || charter == null)
            {
                return;
            }

            TextMeshProUGUI line = GetOrCreateMetadataLine(charter);
            if (line == null)
            {
                return;
            }

            string source = SourceLabel(list.SelectedCollection, song);
            int difficultyCount = CountDifficulties(song);
            int selectedDifficulty = SelectedDifficultyIndex();
            SelectedSongRuntimeMetadata runtimeMetadata = GetSelectedSongMetadata(song, selectedDifficulty);
            if (runtimeMetadata == null || runtimeMetadata.ShouldHydrate)
            {
                QueueSelectedSongMetadataHydration(song, selectedDifficulty);
            }

            string length = runtimeMetadata == null ? "--:--" : runtimeMetadata.Length;
            BpmFacet bpm = runtimeMetadata == null ? BpmFacet.Pending() : runtimeMetadata.Bpm;
            SelectedSongMetadata metadata = SelectedSongMetadataFormatter.FromKnownFacts(source, length, difficultyCount, bpm);
            string text = metadata.FormatLine();

            RectTransform artistRect = artist.transform as RectTransform;
            RectTransform charterRect = charter.transform as RectTransform;
            RectTransform lineRect = line.transform as RectTransform;
            if (artistRect != null && charterRect != null && lineRect != null)
            {
                lineRect.anchorMin = charterRect.anchorMin;
                lineRect.anchorMax = charterRect.anchorMax;
                lineRect.pivot = charterRect.pivot;
                lineRect.anchoredPosition = artistRect.anchoredPosition + new Vector2(0f, -21f);
                lineRect.sizeDelta = new Vector2(charterRect.sizeDelta.x, 18f);
                charterRect.anchoredPosition = artistRect.anchoredPosition + new Vector2(0f, -43f);

                if (archieveRate != null && archieveRate.enabled && !string.IsNullOrWhiteSpace(archieveRate.text))
                {
                    RectTransform rateRect = archieveRate.transform as RectTransform;
                    if (rateRect != null)
                    {
                        rateRect.anchoredPosition = artistRect.anchoredPosition + new Vector2(9.5f, -75f);
                    }

                    RectTransform rankRect = rank == null ? null : rank.transform as RectTransform;
                    if (rankRect != null)
                    {
                        rankRect.anchoredPosition = artistRect.anchoredPosition + new Vector2(-35.8f, -130f);
                    }
                }
            }

            line.text = text;
            line.gameObject.SetActive(true);
            _lastMetadataLine = text;
        }

        private void HideSelectedSongMetadataLine()
        {
            CoverBigDisplayer big = Object.FindObjectOfType<CoverBigDisplayer>();
            if (big == null)
            {
                return;
            }

            TMP_Text[] texts = big.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text != null && text.gameObject != null && text.gameObject.name == MetadataLineName)
                {
                    text.gameObject.SetActive(false);
                    _lastMetadataLine = string.Empty;
                    return;
                }
            }
        }

        private TextMeshProUGUI GetOrCreateMetadataLine(TMP_Text charter)
        {
            Transform parent = charter.transform.parent;
            if (parent == null)
            {
                return null;
            }

            Transform existing = parent.Find(MetadataLineName);
            if (existing != null)
            {
                return existing.GetComponent<TextMeshProUGUI>();
            }

            GameObject lineObject = new GameObject(MetadataLineName, typeof(RectTransform));
            lineObject.transform.SetParent(parent, false);
            TextMeshProUGUI line = lineObject.AddComponent<TextMeshProUGUI>();
            line.font = charter.font;
            line.fontSharedMaterial = charter.fontSharedMaterial;
            line.color = charter.color;
            line.alignment = TextAlignmentOptions.Left;
            line.raycastTarget = false;
            line.enableWordWrapping = false;
            line.overflowMode = TextOverflowModes.Ellipsis;
            line.fontSize = 13f;
            return line;
        }

        private void PatchStatusOverlay()
        {
            CoverListDisplayer list = Object.FindObjectOfType<CoverListDisplayer>();
            if (list != null && list.IsDirList && list.SelectedCollection != null && list.SelectedCollection.Name == RandomRecommendedName)
            {
                ShowStatus("Long press refresh to get new recommendations", 0f);
            }
            else if (list != null && list.IsDirList && list.SelectedCollection != null)
            {
                string info = WebsiteCollectionInfo(list.SelectedCollection.Name);
                if (!string.IsNullOrWhiteSpace(info))
                {
                    ShowStatus(info, 0f);
                }
            }

            if (_statusOverlay != null)
            {
                _statusOverlay.Update();
            }
        }

        private string WebsiteCollectionInfo(string collectionName)
        {
            WebsiteRuntimeCollection collection;
            lock (_websiteCollectionLock)
            {
                collection = _websiteCollections.FirstOrDefault(item => string.Equals(item.Name, collectionName, StringComparison.OrdinalIgnoreCase));
            }

            if (collection == null)
            {
                return null;
            }

            MapListSettings settings = _runtimeSettings.Snapshot();
            WebsiteResolution resolution = ResolveWebsiteCollectionRows(collection, AllSongs(_baseCollections ?? SongStorage.Collections), settings.DownloadedSongsFilter);
            int resolved = resolution.Rows.Length;
            return collection.Name + " Count:" + resolved.ToString(CultureInfo.InvariantCulture) + "/" + resolution.TotalCount.ToString(CultureInfo.InvariantCulture) + " resolved";
        }

        private void ShowStatus(string message, float idleSeconds)
        {
            if (_statusOverlay == null)
            {
                _statusOverlay = new QolStatusOverlay();
            }

            _statusOverlay.Show(message, idleSeconds);
        }

        private SelectedSongRuntimeMetadata GetSelectedSongMetadata(ISongDetail song, int selectedDifficulty)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Hash))
            {
                return null;
            }

            string key = SelectedSongMetadataKey(song.Hash, selectedDifficulty);
            lock (_selectedSongMetadataLock)
            {
                SelectedSongRuntimeMetadata metadata;
                return _selectedSongMetadata.TryGetValue(key, out metadata) ? metadata : null;
            }
        }

        private void SetSelectedSongMetadata(string hash, int selectedDifficulty, SelectedSongRuntimeMetadata metadata)
        {
            if (string.IsNullOrWhiteSpace(hash) || metadata == null)
            {
                return;
            }

            string key = SelectedSongMetadataKey(hash, selectedDifficulty);
            lock (_selectedSongMetadataLock)
            {
                _selectedSongMetadata[key] = metadata;
            }
        }

        private static string SelectedSongMetadataKey(string hash, int selectedDifficulty)
        {
            return hash + "|diff:" + selectedDifficulty.ToString(CultureInfo.InvariantCulture);
        }

        private void QueueSelectedSongMetadataHydration(ISongDetail song, int selectedDifficulty)
        {
            if (song == null || string.IsNullOrWhiteSpace(song.Hash) || !_hydrationScheduler.AllowsHydration(CurrentHydrationSceneState()))
            {
                return;
            }

            string hash = song.Hash;
            string key = SelectedSongMetadataKey(hash, selectedDifficulty);
            lock (_selectedSongMetadataLock)
            {
                SelectedSongRuntimeMetadata existing;
                if (_selectedSongMetadata.TryGetValue(key, out existing) && !existing.ShouldHydrate)
                {
                    return;
                }

                _selectedSongMetadata[key] = new SelectedSongRuntimeMetadata(
                    existing == null ? "--:--" : existing.Length,
                    existing == null ? BpmFacet.Pending() : existing.Bpm,
                    true);
            }

            Task.Run(() => HydrateSelectedSongMetadata(hash, song, selectedDifficulty));
        }

        private void HydrateSelectedSongMetadata(string hash, ISongDetail song, int selectedDifficulty)
        {
            string length = "--:--";
            BpmFacet bpm = BpmFacet.Unknown();

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                cts.CancelAfter(TimeSpan.FromSeconds(12));
                try
                {
                    length = ResolveSongLength(song, cts.Token);
                }
                catch (Exception ex)
                {
                    _log("Selected-song length hydration skipped for " + hash + ": " + ex.Message);
                }

                try
                {
                    bpm = ResolveSongBpm(song, selectedDifficulty, cts.Token) ?? BpmFacet.Unknown();
                }
                catch (Exception ex)
                {
                    _log("Selected-song BPM hydration skipped for " + hash + ": " + ex.Message);
                }
            }

            SetSelectedSongMetadata(hash, selectedDifficulty, new SelectedSongRuntimeMetadata(length, bpm, false));
        }

        private static string ResolveSongLength(ISongDetail song, CancellationToken token)
        {
            object sample = InvokeValueTaskResult(song, "GetPreviewAudioTrackAsync", new object[] { null, token });
            if (sample == null)
            {
                return "--:--";
            }

            object length = GetMemberValue(sample, "Length");
            if (length is TimeSpan)
            {
                return SelectedSongMetadataFormatter.FormatLength((TimeSpan)length);
            }

            return "--:--";
        }

        private static BpmFacet ResolveSongBpm(ISongDetail song, int selectedDifficulty, CancellationToken token)
        {
            try
            {
                object maidata = InvokeValueTaskResult(song, "GetMaidataAsync", new object[] { false, null, token });
                BpmFacet parsed = ExtractBpmFromMaidata(maidata, selectedDifficulty);
                if (parsed != null && parsed.HasKnownValue)
                {
                    return parsed;
                }
            }
            catch
            {
            }

            BpmFacet wholeBpm = TryBpmFromRawMaidata(song, selectedDifficulty);
            if (wholeBpm != null && wholeBpm.HasKnownValue)
            {
                return wholeBpm;
            }

            return TryBpmFromChartAnalyzer();
        }

        private static object InvokeValueTaskResult(object target, string methodName, object[] args)
        {
            if (target == null)
            {
                return null;
            }

            MethodInfo method = target.GetType().GetMethod(methodName, InstanceFlags);
            if (method == null)
            {
                return null;
            }

            object valueTask = method.Invoke(target, args);
            if (valueTask == null)
            {
                return null;
            }

            MethodInfo asTask = valueTask.GetType().GetMethod("AsTask", Type.EmptyTypes);
            if (asTask == null)
            {
                return null;
            }

            Task task = asTask.Invoke(valueTask, null) as Task;
            if (task == null)
            {
                return null;
            }

            if (!task.Wait(15000))
            {
                return null;
            }

            PropertyInfo result = task.GetType().GetProperty("Result", InstanceFlags);
            return result == null ? null : result.GetValue(task, null);
        }

        private static BpmFacet ExtractBpmFromMaidata(object maidata, int selectedDifficulty)
        {
            object charts = GetMemberValue(maidata, "Charts");
            object selectedChart = GetIndexedValue(charts, selectedDifficulty);
            BpmFacet selectedBpm = ExtractBpmFromChart(selectedChart);
            if (selectedBpm != null && selectedBpm.HasKnownValue)
            {
                return selectedBpm;
            }

            return null;
        }

        private static BpmFacet ExtractBpmFromChart(object chart)
        {
            object noteTimings = GetMemberValue(chart, "NoteTimings");
            List<decimal> values = new List<decimal>();
            foreach (object timing in EnumerateValues(noteTimings))
            {
                decimal bpm;
                if (TryDecimal(GetMemberValue(timing, "Bpm"), out bpm) && bpm > 0m)
                {
                    values.Add(bpm);
                }
            }

            if (values.Count == 0)
            {
                return null;
            }

            decimal minimum = values[0];
            decimal maximum = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                minimum = Math.Min(minimum, values[i]);
                maximum = Math.Max(maximum, values[i]);
            }

            return BpmFacet.KnownRange(minimum, maximum);
        }

        private static BpmFacet TryBpmFromChartAnalyzer()
        {
            Type analyzerType = typeof(CoverListDisplayer).Assembly.GetType("MajdataPlay.Scenes.Game.ChartAnalyzer");
            if (analyzerType == null)
            {
                return null;
            }

            Object analyzer = Object.FindObjectOfType(analyzerType, false);
            object value = analyzer == null ? null : GetMemberValue(analyzer, "LastAnalyzeBpm");
            decimal bpm;
            return TryDecimal(value, out bpm) && bpm > 0m ? BpmFacet.Known(bpm) : null;
        }

        private static BpmFacet TryBpmFromRawMaidata(ISongDetail song, int selectedDifficulty)
        {
            string path = GetMemberValue(song, "_maidataPath") as string;
            if (string.IsNullOrWhiteSpace(path))
            {
                string cachePath = GetMemberValue(song, "_cachePath") as string;
                path = string.IsNullOrWhiteSpace(cachePath) ? null : Path.Combine(cachePath, "maidata.txt");
            }

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                string text = File.ReadAllText(path);
                return new ChartDataHydrator(new HttpTextFetcher(), "https://majdata.net").CalculateBpmFromMaidata(text, selectedDifficulty);
            }
            catch
            {
                return null;
            }
        }

        private static object GetMemberValue(object target, string name)
        {
            if (target == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                PropertyInfo property = type.GetProperty(name, InstanceFlags);
                if (property != null)
                {
                    return property.GetValue(target, null);
                }

                FieldInfo field = type.GetField(name, InstanceFlags);
                if (field != null)
                {
                    return field.GetValue(target);
                }
            }

            return null;
        }

        private static CoverListDisplayer ActiveCoverListDisplayer()
        {
            CoverListDisplayer[] active = Resources.FindObjectsOfTypeAll<CoverListDisplayer>()
                .Where(displayer => displayer != null && displayer.gameObject != null && displayer.gameObject.activeInHierarchy)
                .ToArray();

            return active.FirstOrDefault(displayer => displayer.IsChartList)
                ?? active.FirstOrDefault()
                ?? Object.FindObjectOfType<CoverListDisplayer>();
        }

        private static object GetIndexedValue(object target, int index)
        {
            if (target == null || index < 0)
            {
                return null;
            }

            Array array = target as Array;
            if (array != null)
            {
                return index < array.Length ? array.GetValue(index) : null;
            }

            IList list = target as IList;
            if (list != null)
            {
                return index < list.Count ? list[index] : null;
            }

            PropertyInfo indexer = target.GetType().GetProperty("Item", InstanceFlags);
            if (indexer != null)
            {
                try
                {
                    return indexer.GetValue(target, new object[] { index });
                }
                catch
                {
                }
            }

            return null;
        }

        private static IEnumerable<object> EnumerateValues(object target)
        {
            if (target == null)
            {
                yield break;
            }

            IEnumerable enumerable = target as IEnumerable;
            if (enumerable != null)
            {
                foreach (object value in enumerable)
                {
                    yield return value;
                }

                yield break;
            }

            int count = IndexedCount(target);
            for (int i = 0; i < count; i++)
            {
                object value = GetIndexedValue(target, i);
                if (value != null)
                {
                    yield return value;
                }
            }
        }

        private static int IndexedCount(object target)
        {
            if (target == null)
            {
                return 0;
            }

            object count = GetMemberValue(target, "Count") ?? GetMemberValue(target, "Length");
            int result;
            return count != null && int.TryParse(Convert.ToString(count, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                ? result
                : 0;
        }

        private static bool TryDecimal(object value, out decimal result)
        {
            if (value == null)
            {
                result = 0m;
                return false;
            }

            return decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryDouble(object value, out double result)
        {
            if (value == null)
            {
                result = 0d;
                return false;
            }

            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Number, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryLong(object value, out long result)
        {
            if (value == null)
            {
                result = 0L;
                return false;
            }

            return long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static int ClampToInt(long value)
        {
            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (value < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)value;
        }

        private static HydrationSceneState CurrentHydrationSceneState()
        {
            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            if (sceneName.IndexOf("Practice", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HydrationSceneState.Practice;
            }

            if (sceneName.IndexOf("Game", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HydrationSceneState.Gameplay;
            }

            if (sceneName.IndexOf("Setting", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HydrationSceneState.Setting;
            }

            if (sceneName.IndexOf("List", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HydrationSceneState.List;
            }

            if (sceneName.IndexOf("Login", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HydrationSceneState.Login;
            }

            if (sceneName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return HydrationSceneState.Title;
            }

            return HydrationSceneState.Menu;
        }

        private static BpmFacet ParseDiagnosticBpm(string bpm)
        {
            if (string.IsNullOrWhiteSpace(bpm))
            {
                return BpmFacet.Unknown();
            }

            string normalized = bpm.Replace("BPM", string.Empty).Replace("bpm", string.Empty).Trim();
            string[] parts = normalized.Split('-');
            decimal first;
            if (parts.Length == 1 && decimal.TryParse(parts[0].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out first) && first > 0m)
            {
                return BpmFacet.Known(first);
            }

            decimal second;
            if (parts.Length == 2 &&
                decimal.TryParse(parts[0].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out first) &&
                decimal.TryParse(parts[1].Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out second) &&
                first > 0m &&
                second > 0m)
            {
                return BpmFacet.KnownRange(Math.Min(first, second), Math.Max(first, second));
            }

            return BpmFacet.Unknown();
        }

        private static string SourceLabel(SongCollection collection, ISongDetail song)
        {
            if (song != null && song.IsOnline)
            {
                return "Online";
            }

            if (collection != null)
            {
                ISongDetail[] songs = collection.ToArray();
                bool hasOnline = songs.Any(candidate => candidate != null && candidate.IsOnline);
                bool hasDownloaded = songs.Any(candidate => candidate != null && !candidate.IsOnline);
                if (hasOnline && hasDownloaded)
                {
                    return "Mixed";
                }

                if (!collection.IsVirtual && !string.IsNullOrWhiteSpace(collection.Name))
                {
                    return SelectedSongMetadata.NormalizeSource(collection.Name);
                }
            }

            return "Downloaded";
        }

        private static int CountDifficulties(ISongDetail song)
        {
            if (song == null)
            {
                return 0;
            }

            int count = 0;
            ReadOnlySpan<string> levels = song.Levels;
            for (int i = 0; i < levels.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(levels[i]))
                {
                    count++;
                }
            }

            return count;
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

    public sealed class QolStatusOverlay
    {
        private const string RootName = "QoLUpperScreenStatus";

        private GameObject _root;
        private RectTransform _rootRect;
        private TextMeshProUGUI _text;
        private TextMeshProUGUI _shadow;
        private float _hideAt;
        private bool _persistent;

        public bool IsVisible
        {
            get { return _root != null && _root.activeSelf; }
        }

        public string CurrentText
        {
            get { return _text == null ? string.Empty : _text.text ?? string.Empty; }
        }

        public void Show(string message, float idleSeconds)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                Hide();
                return;
            }

            EnsureCreated();
            if (_root == null || _text == null || _shadow == null || _rootRect == null)
            {
                return;
            }

            _persistent = idleSeconds <= 0f;
            _hideAt = _persistent ? 0f : Time.realtimeSinceStartup + idleSeconds;
            _text.text = message;
            _shadow.text = message;
            _root.SetActive(true);
            UpdateWidth();
        }

        public void Update()
        {
            if (_root == null || !_root.activeSelf)
            {
                return;
            }

            if (!_persistent && Time.realtimeSinceStartup >= _hideAt)
            {
                Hide();
                return;
            }

            UpdateWidth();
        }

        private void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void EnsureCreated()
        {
            if (_root != null)
            {
                return;
            }

            TMP_Text styleSource = FindStyleSource();
            if (styleSource == null || styleSource.transform.parent == null)
            {
                return;
            }

            _root = new GameObject(RootName, typeof(RectTransform));
            _root.transform.SetParent(styleSource.transform.parent, false);
            _root.transform.SetAsLastSibling();

            _rootRect = (RectTransform)_root.transform;
            _rootRect.anchorMin = new Vector2(1f, 0.5f);
            _rootRect.anchorMax = new Vector2(1f, 0.5f);
            _rootRect.pivot = new Vector2(1f, 0.5f);
            _rootRect.anchoredPosition = new Vector2(-22f, 5f);
            _rootRect.sizeDelta = new Vector2(320f, 42f);

            GameObject panelObject = new GameObject("StatusPanel", typeof(RectTransform));
            panelObject.transform.SetParent(_root.transform, false);
            Image panel = panelObject.AddComponent<Image>();
            panel.color = new Color(0f, 0f, 0f, 0.58f);
            panel.raycastTarget = false;
            RectTransform panelRect = (RectTransform)panelObject.transform;
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = Vector2.zero;

            _shadow = CreateText("StatusTextShadow", styleSource);
            _shadow.color = new Color(0f, 0f, 0f, 0.75f);
            RectTransform shadowRect = (RectTransform)_shadow.transform;
            shadowRect.anchoredPosition = new Vector2(-14.5f, -1.5f);

            _text = CreateText("StatusText", styleSource);
            _text.color = Color.white;
            RectTransform textRect = (RectTransform)_text.transform;
            textRect.anchoredPosition = new Vector2(-16f, 0f);

            panelObject.transform.SetAsFirstSibling();
            _shadow.transform.SetAsLastSibling();
            _text.transform.SetAsLastSibling();
        }

        private static TextMeshProUGUI CreateText(string name, TMP_Text styleSource)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            textObject.transform.SetParent(styleSource.transform.parent.Find(RootName) ?? styleSource.transform.parent, false);
            if (styleSource != null)
            {
                text.font = styleSource.font;
                text.fontSharedMaterial = styleSource.fontSharedMaterial;
            }

            text.alignment = TextAlignmentOptions.MidlineRight;
            text.fontSize = 18f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 18f;
            text.enableWordWrapping = false;
            text.raycastTarget = false;

            RectTransform rect = (RectTransform)text.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(-32f, -4f);
            return text;
        }

        private void UpdateWidth()
        {
            if (_text == null || _rootRect == null)
            {
                return;
            }

            _text.ForceMeshUpdate();
            float width = Mathf.Clamp(_text.preferredWidth + 44f, 240f, 560f);
            _rootRect.sizeDelta = new Vector2(width, 42f);
        }

        private static TMP_Text FindStyleSource()
        {
            TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
            TMP_Text source = texts.FirstOrDefault(text =>
                text != null &&
                text.font != null &&
                text.gameObject != null &&
                text.gameObject.activeInHierarchy &&
                ((text.text ?? string.Empty).Contains("Press Select P1") || (text.text ?? string.Empty).Contains("Press")));

            return source ?? texts.FirstOrDefault(text =>
                text != null &&
                text.font != null &&
                text.gameObject != null &&
                text.gameObject.activeInHierarchy);
        }
    }
}
