Status: ready-for-agent

# Selected song metadata and hydration status UI

## What to build

Add the selected-song metadata line and passive upper-screen status overlay using native Majdata UI objects and layout rules validated in prototypes.

## Acceptance criteria

- [ ] Selected song info panel shows `{Folder/Source} | {Length} | {Diff Count} diffs | {BPM}` between artist and charter/author.
- [ ] Newline folder names render as spaces in selected-song metadata.
- [ ] Score/rank display does not overlap the metadata line.
- [ ] Source text distinguishes downloaded, online, and mixed rows where known.
- [ ] Hydration progress/status uses a right-side upper-screen text rectangle with dynamic width and hides after idle.
- [ ] Random Recommended selected state shows a refresh instruction/status affordance.
- [ ] In-game smoke or capture verifies the line and status overlay render without replacing the list UI.

## Blocked by

- 09-chart-data-and-online-stats-hydrators
- 10-settings-bridge-and-list-patches
