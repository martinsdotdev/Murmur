# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**Murmur** is a WinUI 3 / Windows App SDK desktop ambient-sound mixer (an unofficial Windows-native port of the Linux app *Blanket*). It plays multiple looping OGG soundscapes simultaneously, each with an independent volume slider, and saves named "presets" of mixes. The default preset UUID (`e52f7134-cff9-463b-9f7d-3740d2cc1d57`) is intentionally identical to Blanket's so settings could theoretically migrate.

- **Repo layout:** the repo root contains:
  - `Murmur/`, the desktop app (one level deep). All source, `Murmur.csproj`, `Package.appxmanifest`, `AGENTS.md`, and `.github/instructions/` live inside `Murmur/Murmur/`.
  - `website/`, the marketing site (Astro). Deploys to `martinsdotdev.github.io/Murmur` via `.github/workflows/website.yml`. See `website/README.md`.
  - `.github/workflows/`, `release.yml` (builds + signs MSIX on `v*.*.*` tag, uploads to GitHub Releases) and `website.yml`.
- **No solution file, no test project, no top-level README**, `Murmur/AGENTS.md` is the existing onboarding doc.

## Build, run, register (the bits you'll use most)

Read `Murmur/AGENTS.md` for the full Build/Run/Deploy + troubleshooting workflow. Quick reference:

```powershell
# Always detect platform first - never hard-code x64/ARM64
$Platform = $env:PROCESSOR_ARCHITECTURE
$Rid = $Platform.ToLower()

# Build (run from the project folder, not the repo root)
cd Murmur
dotnet build -c Debug -p:Platform=$Platform

# Register the MSIX so Start menu / autostart work
Add-AppxPackage -Register ".\bin\$Platform\Debug\net10.0-windows10.0.26100.0\win-$Rid\AppxManifest.xml"

# Quick smoke run (no MSIX install)
dotnet run -c Debug -p:Platform=$Platform

# If launch fails because an old instance is stuck (single-instance redirection)
taskkill /IM Murmur.exe /F
```

- **Developer Mode must be on** in Windows settings (`Get-WindowsDeveloperLicense` to verify).
- `<TargetFramework>` is `net10.0-windows10.0.26100.0` and `<WindowsAppSDKSelfContained>true`, both load-bearing; don't downgrade without reason.
- `<DefineConstants>DISABLE_XAML_GENERATED_MAIN</DefineConstants>` is also load-bearing, see "Single-instance redirection" below.

### Website

The marketing site lives in `/website/` (Astro + TypeScript, single landing page). It mirrors the app's icon and bundled OGGs at build time so the in-browser demo plays the real sounds. Quick reference:

```powershell
cd website
npm install
npm run dev      # → http://localhost:4321/Murmur
npm run build    # → website/dist
```

The site is deployed by `.github/workflows/website.yml` on every push to `master` that touches `website/**` or any of the mirrored asset files. Repo *Settings → Pages → Source* must be set to **GitHub Actions** (one-time toggle).

**Sources of truth, mirrored to the site:**

| Site asset | App source | Sync |
|---|---|---|
| `website/public/icon.svg` | `Murmur/Assets/Source/AppIcon.svg` | `website/scripts/copy-assets.mjs` |
| `website/public/sounds/*.ogg` | `Murmur/Sounds/*.ogg` | same |
| `website/src/data/sounds.ts` (catalog + lucide paths) | `Murmur/Models/SoundCatalog.cs` + `Murmur/ViewModels/SoundCardViewModel.cs:80-126` | hand-edited |
| Footer attribution table | `Murmur/Sounds/SOUNDS_LICENSING.md` | hand-edited |

When the in-app catalog or icon changes, update the mirrors. Only 14 sounds; not worth automating.

### Releases

Push a tag matching `v*.*.*` to trigger `.github/workflows/release.yml`. The workflow:

1. Generates a fresh self-signed code-signing cert (`CN=MurmurDev, O=MurmurDev`, 2-year validity).
2. Patches `Package.appxmanifest` with the cert subject as `Identity.Publisher` and `<tag>.0` as `Identity.Version`.
3. Builds an MSIX bundle for x64 + ARM64 in parallel via `dotnet publish -p:GenerateAppxPackageOnBuild=true -p:AppxBundle=Always`.
4. Uploads `Murmur-<version>-{x64,arm64}.msixbundle` and `Murmur-<version>.cer` to the GitHub Release.

Users install by trusting the `.cer` (Local Machine → Trusted People) once per release, then double-clicking the `.msixbundle`. The website's Download section walks them through it visually.

The cert is regenerated each release, that's intentional. Free, no secret management. To upgrade later: replace the `Generate self-signed code-signing certificate` step with `windows-store-credentials` or a SignPath workflow.

### Tests

There is no test project today. `Murmur/.github/instructions/testing.instructions.md` defines the convention: a sibling `Murmur.Tests/` MSTest+Moq project mirroring source folders, naming `MethodName_Scenario_ExpectedResult`. When a test project exists, run filtered tests with:

```powershell
cd Murmur.Tests
dotnet test -c Debug -p:Platform=$Platform --filter "FullyQualifiedName~MainViewModelTests.SomeTestName"
```

## Architecture, the parts that aren't obvious from one file

### Initialization order (sensitive, read before refactoring)

```
Program.Main (custom STA entry, suppresses XAML's generated Main)
 └── DecideRedirection (single-instance check via AppLifecycle.AppInstance)
 └── Application.Start sets up DispatcherQueueSynchronizationContext
     └── App ctor (App.xaml.cs static singletons resolve here)
         └── App.OnLaunched
             └── new MainWindow()
                 ├── new MainViewModel(App.Mixer)   ← BEFORE InitializeComponent (x:Bind)
                 ├── InitializeComponent
                 ├── tray + SMTC initialized        ← can fire before mixer is ready
                 └── RootFrame.Navigate(MainPage, ViewModel)
                     └── MainPage.Loaded → ViewModel.InitializeAsync
                         ├── AppState.LoadOrCreateAsync
                         ├── _mixer.InitializeAsync (AudioGraph)
                         ├── RegisterSoundAsync × 14 built-ins (parallel)
                         ├── CustomAudioImportService.RestoreSavedAsync
                         ├── apply persisted prefs (with _suppressVolumeWrites = true)
                         └── new PresetService → ApplyActivePreset → optional Play
```

**Why ViewModel is constructed before `InitializeComponent`** (MainWindow.xaml.cs:22): so `x:Bind` expressions in the TitleBar can evaluate against a real instance instead of `null`. Don't reorder.

**Why tray/SMTC actions wrap calls in try/catch** (MainWindow.xaml.cs `SafePlay`/`SafePause`): they exist before `InitializeAsync` finishes, so a tray click in that window must no-op rather than throw `InvalidOperationException("Mixer not initialized.")`.

### Service-locator pattern (no DI container)

`App` (App.xaml.cs) holds **static singletons**: `Mixer`, `ViewModel`, `State`, `PresetService`, `Tray`, `Smtc`, `Startup`, `ImportService`. The `null!` initializers signal "set during MainViewModel.InitializeAsync." This is a deliberate small-app choice; don't replace with a DI container without a reason, every consumer reads `App.X` directly and there's nothing to register.

### AudioGraphMixerService, the "silent-stream-paused" pattern

The single `AudioGraph` runs **continuously** from first init to dispose. Per-sound `AudioFrameInputNode`s are attached lazily when volume rises above 0 and disposed when it drops back. Master Play/Pause is implemented as `_output.OutgoingGain = masterVolume / 0` on the device output node, **not** a graph stop, because input nodes attached to a graph that hasn't been `Start()`ed yet never get pumped. Stopping/restarting the graph would break that.

Audio-thread / UI-thread synchronization uses **two gates together**:
1. A `volatile bool IsActive` on each `SoundEntry`. UI thread sets it `false` *before* disposing the input node; the audio thread reads it at the top of `OnQuantumStarted` and exits early.
2. A defensive `try { sender.AddFrame(frame); } catch (ObjectDisposedException) { }` to close the rare race where the audio callback ran past the `IsActive` check just as the UI disposed.

Don't remove either gate, both are needed.

There's also a CsWinRT quirk inside `OnQuantumStarted`: `(IMemoryBufferByteAccess)reference` throws under CsWinRT, so the code uses `reference.As<IMemoryBufferByteAccess>()` (explicit COM QI). Preserve that if you touch the audio frame loop.

The `ShouldBePlaying(volume) => volume > 0` predicate (line ~161) is intentionally strict, see the docstring there for the alternatives (epsilon / hysteresis) and why strict-zero was kept.

### State persistence & the `_suppressVolumeWrites` flag

`AppState` (`Murmur/Services/AppState.cs`) lives at `%LOCALAPPDATA%\Murmur\state.json`. Persistence has three subtle properties:

1. **Atomic write**: serialize → write `state.json.tmp` → `File.Move(..., overwrite: true)`. A crash mid-write can never produce a partial file.
2. **Debounced save** with `RequestSave(delayMs=500)`, coalesces dozens of slider-drag mutations into one write. Implemented with an `Interlocked.Exchange` on the `CancellationTokenSource` so old debounces are cancelled+disposed deterministically (older code leaked CTSs under heavy slider activity).
3. **Serialization happens on the UI thread** (`SerializeOnUIThreadAsync`). `Presets` / `SoundVolumes` / `CustomAudios` are mutated from the UI; reflection-based JSON serialization on a background thread can throw "Collection was modified."

`MainViewModel._suppressVolumeWrites` is a reentrancy guard set to `true` while applying state from disk or applying a preset. Without it, the `OnFooChanged` partial-method handlers would fire on assignment and write back to `_state` mid-load, corrupting the freshly-loaded state. Always wrap "I'm setting these because I just read them" assignments in:

```csharp
_suppressVolumeWrites = true;
try { /* assign properties that have OnFooChanged handlers */ }
finally { _suppressVolumeWrites = false; }
```

### Single-instance redirection (Program.cs)

Without `DISABLE_XAML_GENERATED_MAIN` + the custom `Program.Main`, every Start menu click launches a new Murmur process. Instead, `AppInstance.FindOrRegisterForKey("Murmur-singleinstance")` runs **before** `Application.Start`; the second instance hands its activation args to the first via `RedirectActivationToAsync` and exits. The first instance's `OnActivated` marshals to its dispatcher and brings the window forward.

### TitleBar adapts to navigation

`MainWindow.OnFrameNavigated` toggles visibility of every mixer-only TitleBar surface (preset chip, master volume button, view toggle, overflow menu, playback footer) when the user navigates from `MainPage` to `SettingsPage`, and uses `AppTitleBar.Subtitle` as a breadcrumb. Page transitions use `DrillInNavigationTransitionInfo` for mixer→settings (drill is the recommended motion for going *into* an app).

### Keyboard accelerators are window-scoped, not page-scoped

Wired on `MainWindow`'s outer Grid so they fire regardless of focus. Mixer-only shortcuts (`Reset`, `Import`, `GridView`) are gated by `IsOnMixerPage` so they don't mutate hidden mixer state from a subpage. `Space` (play/pause) and `Ctrl+P` (Settings) stay window-scoped.

### Background playback / hide-to-tray

`OnWindowClosed` checks `App.State.BackgroundPlayback` and *handles* the close event (`AppWindow.Hide`) instead of exiting. The tray icon's "Quit" action does the real shutdown, flushes state synchronously, disposes mixer + SMTC, then `Application.Current.Exit()`.

### StartupTask (autostart) only works packaged

`StartupService.SetEnabledAsync` returns the **actually applied** `StartupTaskState` because group policy / "user disabled in Task Manager" / unpackaged builds can override the request. `MainViewModel.OnAutoStartChanged` uses an `Interlocked.Increment`-based version token to discard out-of-order completions, then reflects the real state back into the UI if it diverged. Don't simplify this to "just toggle and assume it worked."

### SMTC fires on a non-UI thread

`SmtcService.OnButtonPressed` callbacks come in on a worker thread; the service marshals every callback through the captured `DispatcherQueue` before invoking the handler delegates. Any future SMTC additions must do the same.

## Project conventions

These are extracted from `.github/instructions/` (read those for the full ruleset):

- **File-scoped namespaces.** No `this.` qualification. Private fields use `_camelCase` (SA1101 / SA1309 are intentionally suppressed; followed throughout existing code).
- **`[ObservableProperty] public partial T Foo { get; set; }`**, uses C#-13 partial properties + CommunityToolkit.Mvvm 8.4.2 source generators. The matching `partial void OnFooChanged(T value)` / `OnFooChanged(T oldValue, T newValue)` hooks are the canonical place for side effects.
- **Glyphs are inline Segoe Fluent Icons** (e.g., Play/Pause ``/``). The `ViewToggleGlyph` shows the *target* view's icon, clicking it switches *to* that view.
- **Sound IDs are stable strings** (`"rain"`, `"summer-night"`); custom imports get `{slug}-{guid8}` IDs from `CustomAudioImportService.MakeUniqueId`.
- **Volumes of 0 are omitted from `Preset.SoundVolumes`** (treated as "missing == 0" on apply). Reflect that when reading/writing preset JSON.
- **All log lines go through `DiagnosticLog.Log`** to `%LOCALAPPDATA%\Murmur\debug.log`. Don't `Console.WriteLine`, there's no console in a packaged WinUI app.
- **User data lives at `%LOCALAPPDATA%\Murmur\`**: `state.json`, `debug.log`, and `CustomAudio\*.ogg`.

## Authoritative instruction files

`Murmur/.github/instructions/` contains scope-specific rules that the repo treats as load-bearing, particularly for accessibility, performance, and WinUI patterns. **Open the relevant one when its scope applies**, rather than re-deriving rules:

| File | When to consult |
|---|---|
| `winui-best-practices.instructions.md` | MVVM, x:Bind, community toolkit, API verification |
| `windows-apis.instructions.md` | Looking up an unknown Windows / WinAppSDK type |
| `accessibility.instructions.md` | Touching XAML / controls (AutomationProperties, keyboard nav, contrast) |
| `performance.instructions.md` | Bindings, collections, layout, async / IO |
| `code-quality.instructions.md` | CA*/SA*/IDE* analyzer fixes, naming |
| `globalization.instructions.md` | Adding user-facing strings (`.resw`, `x:Uid`) |
| `security.instructions.md` | Secrets, user input, HTTP, permissions |
| `testing.instructions.md` | Adding the test project / writing tests |

When facing an unknown type or build error, **always web-search the Microsoft docs first** (`microsoft_docs_search` / `microsoft_code_sample_search`); only fall back to inspecting `.winmd` / decompiling as a last resort. See `AGENTS.md` "Troubleshooting Build Errors" for the full escalation order.
