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
                settings.Sorting != _lastAppliedSettings.Sorting ||
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
                SongCollection[] defaultCollections = HasActiveSongTransform(settings)
                    ? TransformCollections(source, settings, selectedDifficulty)
                    : source;
                return AppendRandomRecommended(defaultCollections);
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

        private static IEnumerable<ISongDetail> ApplySongSettings(IEnumerable<ISongDetail> songs, MapListSettings settings, int selectedDifficulty)
        {
            IEnumerable<ISongDetail> filtered = (songs ?? Enumerable.Empty<ISongDetail>())
                .Where(song => song != null)
                .Where(song => SourcePasses(song, settings.DownloadedSongsFilter))
                .Where(song => DifficultyFilterPasses(song, settings.DifficultyFilter));

            return SortSongs(filtered, settings.Sorting, selectedDifficulty);
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

        private static Comparison<ISongDetail> SongComparison(MapListSortMode sortMode, int selectedDifficulty)
        {
            switch (sortMode)
            {
                case MapListSortMode.DateAdded:
                    return (left, right) => CompareDescending(left.Timestamp, right.Timestamp, SongTieBreak(left, right));
                case MapListSortMode.Difficulty:
                    return (left, right) => CompareNullableAscending(LevelSortValue(left, selectedDifficulty), LevelSortValue(right, selectedDifficulty), SongTieBreak(left, right));
                case MapListSortMode.NoteDesigner:
                    return (left, right) => CompareText(DesignerForDifficulty(left, selectedDifficulty), DesignerForDifficulty(right, selectedDifficulty), SongTieBreak(left, right));
                case MapListSortMode.Title:
                    return (left, right) => CompareText(left.Title, right.Title, SongTieBreak(left, right));
                case MapListSortMode.Artist:
                    return (left, right) => CompareText(left.Artist, right.Artist, SongTieBreak(left, right));
                case MapListSortMode.PlayCount:
                case MapListSortMode.Rank:
                case MapListSortMode.ApFcRank:
                    return SongTieBreak;
                default:
                    return SongTieBreak;
            }
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

        private void PatchSelectedSongMetadataLine()
        {
            CoverListDisplayer list = Object.FindObjectOfType<CoverListDisplayer>();
            if (list == null || !list.IsChartList)
            {
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
            SelectedSongMetadata metadata = SelectedSongMetadataFormatter.FromKnownFacts(source, "--:--", difficultyCount, BpmFacet.Pending());
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

            if (_statusOverlay != null)
            {
                _statusOverlay.Update();
            }
        }

        private void ShowStatus(string message, float idleSeconds)
        {
            if (_statusOverlay == null)
            {
                _statusOverlay = new QolStatusOverlay();
            }

            _statusOverlay.Show(message, idleSeconds);
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
