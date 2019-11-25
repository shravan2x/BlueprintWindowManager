# BlueprintWindowManager

[![AppVeyor](https://img.shields.io/appveyor/ci/Shravan2x/blueprintwindowmanager.svg?maxAge=2592000&style=flat-square)](https://ci.appveyor.com/project/Shravan2x/blueprintwindowmanager)

Blueprint is a layout manager for Windows that lets you define complex layouts neatly via JSON.

### What problem does this tool solve?

A common problem for me is that my window positions often get arranged and resized automatically to random values. This happens in situations like:

1. When I connect new external monitors (it changes my monitor resolution and adjusts all my window sizes).
2. When I reconnect external monitors in the wrong order after disconnecting them (it positions windows wrong and in wrong sizes).
3. When I open a fullscreen game that uses the wrong resolution (it changes the monitor resolution and all my windows get resized).
4. When I close my laptop lid before unplugging external monitors (Windows changes my main monitor to one of the external ones and repositions+resizes windows).
5. And a ton of other times like when I update my Nvidia drivers (why does the screen resolution change when updating a driver?).

Since resulting positions and sizes are effectively random, I needed a way to reposition them to my liking. This tool solves that problem.

### Features (or why existing solutions were insufficient)

Before I wrote BlueprintWindowManager I tried other similar solutions, but found them lacking. Features of this app are:

- Ability to define relative or calculated sizes. For example, positioning a window 50 pixels from the bottom right of the primary monitor's working area (i.e. discluding the space occupied by the taskbar). Defining window positions and sizes relatively makes them portable across multiple systems (eg. home and work), resolutions, and DPIs.
- Scripting support - this app lets you perform arbitrarily advanced calculations using a built-in JS engine.
- Actively updated and supports the latest builds of Windows.
- Open-source - inspect the code, build it yourself, make improvements, etc..
- Lots of filters - enough to differentiate between all windows on your screen.

Currently supported window filters are:

- `windowTitle` - A regex filter to match the window title against.
- `windowClass` - A regex filter to match the window class against.
- `programPath` - A regex filter to match the program path against.
- `taskbarAppId` - A regex filter to match the taskbar app ID against.
- `taskbarIndex` - A regex filter to match the window's taskbar button group index against. Use an empty string to match against windows that aren't on the taskbar.
- `taskbarSubIndex` - A regex filter to match the window's taskbar button index against. Use an empty string to match against windows that aren't in a button group.
- `isToolWindow` - A filter to match on whether the window is a tool window.

*Note: The `taskbarIndex` and `taskbarSubIndex` filters depend on the [7+ Taskbar Tweaking Library by RaMMicHaeL](https://rammichael.com/7-taskbar-tweaking-library).*

## Getting Binaries

Binaries can be found on the [releases page](https://github.com/Shravan2x/BlueprintWindowManager/releases).

## Documentation

Browse the documentation on [the wiki](https://github.com/shravan2x/BlueprintWindowManager/wiki).

An example positioning rule is:
```json
{
    "enabled": true,
    "name": "MySQL Workbench",
    "filters": {
        "windowTitle": "^MySQL Workbench$",
        "programPath": "^C:\\\\Program Files\\\\MySQL\\\\MySQL Workbench 8\\.0 CE\\\\MySQLWorkbench\\.exe$"
    },
    "targetMonitor": "laptop",
    "targetRect": {
        "width": "workAreaWidth * 0.90",
        "height": "workAreaHeight * 0.85",
        "center": true
    },
    "targetState": "restored"
}
```

## License

A license will be added shortly.
