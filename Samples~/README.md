# Global Callbacks Sample

This sample demonstrates how to forward Unity lifecycle callbacks (Awake, Start, Update, etc.) to Odin code.

## What's Included

| File | Purpose |
|---|---|
| `OdinGlobalHooks.cs` | A `DontDestroyOnLoad` MonoBehaviour that bridges Unity callbacks to Odin |
| `global.odin` | The Odin implementations of the lifecycle callbacks |

## How to Use

Place `global.odin` directly in `Assets/Odin/` and `OdinGlobalHooks.cs` anywhere in your project.

## How It Works

The Odin-to-C# binding tool (`odin2cs`) reads `global.odin` and generates the corresponding C# partial method signatures with `[ForeignDecl]` attributes so everything wires up correctly.
