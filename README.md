# Figma uGUI Prefab Builder

Figma uGUI Prefab Builder is a Unity Editor package that turns Figma node data and a declarative hierarchy description into reusable uGUI prefabs.

It creates the GameObject hierarchy, reconstructs layout with `RectTransform`, applies colors and TextMeshPro text styles, downloads image nodes as sprites, adds button components, and saves the generated roots as prefab assets.

> This package is the Unity-side prefab builder. It does not currently fetch a Figma URL or infer the target prefab hierarchy by itself. The Figma raw node JSON and hierarchy JSON must be prepared before generation.

> To extract raw node JSON and hierarchy JSON from Figma designs, refer to [FigmaToUGUIHierarchy](https://github.com/beddup/FigmaToUGUIHierarchy.git), which leverages AI large model semantic understanding and visual understanding to parse Figma nodes and generate the required JSON files.

## Features

- Builds nested uGUI GameObjects from a custom hierarchy description.
- Maps Figma bounds to `RectTransform` size, position, pivot, and anchor alignment.
- Generates solid-color `Image` components.
- Generates `TextMeshProUGUI` components with font matching, alignment, decoration, case, gradient, outline, and shadow support.
- Downloads Figma-rendered image nodes and imports them as Unity sprites.
- Caches downloaded sprites and deduplicates identical images by content hash.
- Adds `Button` components to nodes marked as interactive.
- Generates multiple selected pages sequentially from a single configuration asset.
- Saves each generated root under a configurable prefab folder.

## Requirements

- Unity 6 or newer.
- An active Canvas in the scene.
- [FigmaClient](https://github.com/beddup/figmaclient).
- [Figma TMP Styler](https://github.com/beddup/figmatmpstyler).
- A Figma personal access token with permission to read the source file.
- Prepared Figma raw node JSON and prefab hierarchy JSON files.

## Installation


Add these lines to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
     "com.beddup.figmatmpstyler": "https://github.com/beddup/figmatmpstyler.git", 
     "com.beddup.figmaclient": "https://github.com/beddup/figmaclient.git",
     "com.beddup.figma-ugui-prefab-builder":"https://github.com/beddup/FigmaUGUIPrefabBuilder.git"
  }
}
```

You can also download the source code directly and import it into your Unity project.

## Quick start

1. Make sure the current scene contains an active `Canvas`.
2. Create a configuration asset from **Assets > Create > FigmaToUGUI > Config**.
3. Assign the output folders:
   - `Material Save Folder` for generated TextMeshPro materials.
   - `Sprite Save Folder` for downloaded PNG sprites.
   - `Prefab Save Folder` for generated prefabs.
4. Add the TextMeshPro font assets used by the Figma design to `Font Items`. Match each entry by family name, weight, and optional style.
5. Add one or more generation-result JSON files to `Figma Pages`.
6. In the configuration Inspector, select the pages to build and click **Generate Selected Pages**.

During generation, existing children of the scene Canvas are temporarily deactivated. Newly generated root objects remain active and are saved as prefabs.

## Generation-result JSON

Each TextAsset in `Figma Pages` describes one generation job:

```json
{
  "file_key": "FIGMA_FILE_KEY",
  "api_token": "FIGMA_PERSONAL_ACCESS_TOKEN",
  "node_name": "ExamplePanel",
  "raw_content_path": "Library/FigmaToUGUI/ExamplePanel/raw.json",
  "prefab_hierarchy_path": "Library/FigmaToUGUI/ExamplePanel/prefab_hierarchy.json"
}
```

The referenced paths are read from the local project when generation starts. Do not commit access tokens or private design data to a public repository.

## Prefab hierarchy format

The hierarchy JSON controls which Figma nodes become GameObjects and how they are rendered:

```json
{
  "nodeId": "1:2",
  "nodeName": "Example Panel",
  "gameObjectCategory": "container",
  "gameObjectName": "ExamplePanel",
  "isButton": false,
  "horizontal_alignment": "center",
  "vertical_alignment": "center",
  "children": [
    {
      "nodeId": "1:3",
      "nodeName": "Title",
      "gameObjectCategory": "text",
      "gameObjectName": "Title",
      "isButton": false,
      "horizontal_alignment": "center",
      "vertical_alignment": "top",
      "children": []
    }
  ]
}
```

Supported `gameObjectCategory` values:

| Value | Generated component or behavior |
| --- | --- |
| `container` | Creates a hierarchy-only GameObject |
| `color` | Adds a uGUI `Image` using the Figma solid fill |
| `image` | Downloads or reuses a sprite and adds a uGUI `Image` |
| `text` | Adds `TextMeshProUGUI` and applies Figma text styling |

Set `isButton` to `true` to add a uGUI `Button`. Alignment values are `left`, `center`, or `right` horizontally and `top`, `center`, or `bottom` vertically.

A container may omit a matching Figma node. Such synthetic containers stretch to fill their parent and can be used to organize the generated hierarchy.

## Generated assets

- Prefabs are written to `Prefab Save Folder`, defaulting to `Assets/Prefabs`.
- Sprites are written to `Sprite Save Folder`.
- TextMeshPro materials are written to `Material Save Folder`.
- The sprite cache is stored as `Assets/FigmaData/FigmaImagesLocalPool.asset`.

The sprite cache can be inspected in Unity. It supports cleaning invalid entries, clearing records, and retrying downloads that retained a source URL.

## Current limitations

- Figma URL parsing and hierarchy inference are external preprocessing steps.
- Generation requires a Canvas in the currently open scene.
- Only the explicitly supported container, solid color, image, text, and button mappings are generated.
- Figma masks and clipping are not currently applied.
- Figma constraints are not currently converted; layout uses the alignment values from the hierarchy description.
- A changed Figma image may continue using a cached sprite when its download identifier is unchanged.
- Generation deactivates existing Canvas children and does not restore their active state automatically.

## License

Released under the [MIT License](LICENSE).
