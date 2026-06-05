# Majdata UI Mod Patterns

This note captures prototype learnings from the song-list QoL work. It is intentionally practical: these are patterns that worked against the live MajdataPlay scene through `mod-test-tools/test-hook-mod`, and should translate into MelonLoader mod code with the usual lifecycle and cleanup added.

## General Approach

Use the game's existing UI objects whenever possible. MajdataPlay screens already have prefabs, fonts, materials, colors, positions, and behavior conventions. The quickest reliable prototype path was:

1. Find the active scene component with `Resources.FindObjectsOfTypeAll(type)`.
2. Read private fields with reflection.
3. Either create a fresh child object that copies visual style, or clone a prefab/menu/card and then override its text.
4. Capture with `UnityEngine.ScreenCapture.CaptureScreenshot(path)`.

Important caveat: cloning a live text object can duplicate stale mesh/subobject state. For simple extra text, create a fresh `GameObject` with `RectTransform` and `TextMeshProUGUI`, then copy font/material/color from an existing label.

## Adding Text To Existing Panels

For the song info panel, the relevant component was:

```csharp
MajdataPlay.Scenes.List.CoverBigDisplayer
```

Useful private fields:

- `_title`
- `_artist`
- `_charter`
- `_archieveRate`
- `_rank`

Pattern:

```csharp
var artist = (TMPro.TMP_Text)field("_artist").GetValue(displayer);
var charter = (TMPro.TMP_Text)field("_charter").GetValue(displayer);

var lineObject = new GameObject("QoLPrototypeInfoLine", typeof(RectTransform));
lineObject.transform.SetParent(charter.transform.parent, false);

var extra = lineObject.AddComponent<TMPro.TextMeshProUGUI>();
extra.font = charter.font;
extra.fontSharedMaterial = charter.fontSharedMaterial;
extra.color = charter.color;
extra.alignment = TMPro.TextAlignmentOptions.Left;
extra.raycastTarget = false;
extra.enableWordWrapping = false;
extra.overflowMode = TMPro.TextOverflowModes.Ellipsis;
extra.text = "JPORTAL | 01:20 | 3 diffs | 240BPM";
```

Positioning that worked:

- Put the metadata line directly after the artist line.
- Move the charter/author label below it.
- If score/rank is visible, move score/rank down enough to avoid overlap.

Do not append the new line to `artist.text` unless you want it to behave as one block. A separate TMP object is easier to position and remove.

## Adding Or Changing Folder Tiles

Folder browsing is handled by:

```csharp
MajdataPlay.Scenes.List.CoverListDisplayer
MajdataPlay.Scenes.List.FolderCoverSmallDisplayer
MajdataPlay.Collections.SongCollection
MajdataPlay.SongStorage
```

To prototype a new folder, insert a fake `SongCollection` into `SongStorage.Collections`:

```csharp
var collection = Activator.CreateInstance(
    collectionType,
    new object[] { "Random Recommended", emptySongArray });

collection.GetType().GetProperty("IsOnline").SetValue(collection, true, null);
collection.GetType().GetProperty("IsVirtual").SetValue(collection, true, null);
```

Then rebuild and position the directory carousel:

```csharp
coverList.SwitchToDirListInternal();
coverList.SlideListInternal(index);
```

The visible folder tile is a `FolderCoverSmallDisplayer`. Useful fields:

- `_folderText`
- `_icon`
- `_danIcon`
- `_boundCollection`

The online cloud icon is the `_icon` child with the existing `CloudIcon` sprite. Setting `SongCollection.IsOnline = true` is semantically correct, but for a prototype the tile may still need:

```csharp
folderCoverType.GetProperty("IsOnline", flags).SetValue(tile, true, null);
((GameObject)folderCoverType.GetField("_icon", flags).GetValue(tile)).SetActive(true);
```

For long folder names, keep the data name with spaces for selected info, and only override the tile label:

```csharp
folderText.text = "Random\nRecommended";
folderText.alignment = TMPro.TextAlignmentOptions.Center;
folderText.enableAutoSizing = true;
folderText.fontSizeMin = 12f;
folderText.fontSizeMax = 18f;
folderText.overflowMode = TMPro.TextOverflowModes.Overflow;
((RectTransform)folderText.transform).sizeDelta = new Vector2(118f, 54f);
```

This keeps the selected info panel as `Random Recommended` while making the small folder tile readable.

## Adding Settings Groups And Cards

Settings UI is built from:

```csharp
MajdataPlay.Scenes.Setting.SettingManager
MajdataPlay.Scenes.Setting.Menu
MajdataPlay.Scenes.Setting.Option
MajdataPlay.Settings.GameSetting
```

Existing settings groups come from public properties on `GameSetting`. The current first built-in group is `Game`; visible `GameOptions` include:

- `TapSpeed`
- `TouchSpeed`
- `SlideFadeInOffset`
- `BackgroundDim`
- `StarRotation`
- `BGInfo`
- `SecondaryBGInfo`
- `SubScreenBGInfo`
- `TopInfo`
- `EnableJudgeTimingGauge`
- `TrackSkip`
- `EnforceGameFailure`
- `FastRetry`
- `GameplaySubScreenClickBehavior`
- `Mirror`
- `Rotation`
- `SlideSkipping`
- `Random`
- `RecordMode`
- `ManualStartGame`

For production, adding a settings group should ideally expose a new public property before `Game` in whatever object `SettingManager.Start()` reflects. The game constructs menu order from:

```csharp
Setting.GetType().GetProperties()
```

For prototyping, cloning the existing `menuPrefab` and `optionPrefab` worked:

```csharp
var menuObject = Object.Instantiate(menuPrefab, manager.transform);
var menu = menuObject.GetComponent<Menu>();
menu.Name = "Map List";
titleText.text = "Map List Settings";

var optionObject = Object.Instantiate(optionPrefab, menuObject.transform);
var option = optionObject.GetComponent<Option>();
((MonoBehaviour)option).enabled = false;

nameText.text = "Difficulty Filter";
valueText.text = "No";
descriptionText.text = "Difficulty Filter Description";
```

The setting card layout expects one selected card at scale `1` and the adjacent card at scale `0.6`:

```csharp
selected.transform.localPosition = new Vector3(0, 0, 0);
selected.transform.localScale = Vector3.one;

next.transform.localPosition = new Vector3(330, 0, 0);
next.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
```

For the `Map List` prototype, the intended group order is:

```text
Map List
Game
Judge
Display
Volume
Mod
Debug
ChartSetting
```

Current prototype settings:

- `Difficulty Filter`: default `No`
- `Sorting`: default `Default`
- `Grouping`: default `Default`
- `Downloaded Songs Filter`: default `Mixed`

## Auto Tests For UI Prototypes

The test-hook eval path can provide lightweight integration checks without building a real mod DLL. Useful checks from this work:

- Settings group order includes `Map List` at index 0 and `Game` at index 1.
- The settings screen can be entered, rendered, and captured.
- Returning to the list scene still lets `CoverListDisplayer` switch folders.
- The folder carousel can select both `Random Recommended` and `All`.

One important detail: `SlideListInternal(index)` is not always the same as storage array index after the directory carousel rebuilds and wraps. For robust tests, scan carousel positions and assert the selected collection names observed through `SelectedCollection`.

Example assertion shape:

```json
{
  "settingsGroupOrderOk": true,
  "folderSwitchingOk": true
}
```

## Practical Gotchas

- Scene initialization can be async. A short delay after `SwitchScene` is usually needed before probing scene objects.
- `SettingManager` may not fully initialize if entered from a test route at the wrong time. For UI-only prototypes, cloning visible prefabs is more reliable than depending on full settings binding.
- TMP text created by cloning a live label can retain unwanted child/submesh state. Prefer fresh `TextMeshProUGUI` for added labels.
- For score-bearing song cards, always test with an actual score/rank visible. Empty-score screenshots can hide layout collisions.
- Keep prototype object names prefixed, for example `QoLPrototype...`, so reruns can delete old objects cleanly.
- Do not treat prototype eval code as production mod architecture. Production should move this into a normal MelonLoader mod with lifecycle hooks, cleanup, and stable patch/injection points.
