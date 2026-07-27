# Odin Interop

Continuation of brilliant work by [**herohiralal**](https://github.com/herohiralal/com.herohiralal.odininterop)

Lets you use Odin language from Unity. Check for original author motivation in his repo as I share many of his motivations.


## Features

- [Odin language](https://github.com/odin-lang/Odin)
- Hot reload
- Automatic binding generation

## My changes

I changed some of the repo structure from original authors, and did a lot of changes blindly trusting AI, seems to work fine.


## How To Use

Download project and place in your Assets folder. I just don't want to understand better
whatever Unity's package management system does.

Place your source code on Assets/OdinLanguage/Sources. 

Bindings for odin code are automatically generated from the files in Assets/OdinLanguage/Source. 
These bindings create classes and glue code to bind and hot reload.

Bindings to call C# code from Odin are automatically generated from MonoBehaviours that use `[OdinExport]` attribute

A ready-to-use sample that forwards Unity lifecycle callbacks (Awake, Start, Update, etc.) to Odin is available in the samples folder.

See [Samples](./Samples~/README.md) to see how its set up


### Supported Interop Types

- Primitives:
  - `bool` <-> `bool`
  - `byte` <-> `u8`
  - `sbyte` <-> `i8`
  - `short` <-> `i16`
  - `ushort` <-> `u16`
  - `int` <-> `i32`
  - `uint` <-> `u32`
  - `long` <-> `i64`
  - `ulong` <-> `u64`
  - `float` <-> `f32`
  - `double` <-> `f64`
- Enums:
  - `T` <-> `T` (gets recreated 1:1 in generated code)
- Strings:
  - `OdinInterop.String16` <-> `string16` (UTF-16; auto-converting from C# `string`)
  - `OdinInterop.String8` <-> `string8` (UTF-8 str; auto-converting from C# `string`)
- Collections:
  - `OdinInterop.Slice<T>` <-> `[]T` (Odin slices; auto-converting from C# `T[]`)
  - `OdinInterop.DynamicArray<T>` <-> `[dynamic]T` (Odin dynamic arrays; auto-converting from C# `List<T>`)
- Odin Internals:
  - `OdinInterop.Allocator` <-> `runtime.Allocator` (Odin memory allocator; useful for creating unmanaged Odin strings/collections in C#)
- Unity Objects:
  - `T` <-> `OdinInterop.ObjectHandle<T>` (unmanaged wrapper handle; auto-converting from C# Objects; all classes deriving from `UnityEngine.Object` are supported)
