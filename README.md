# Sentry Capture

**Sentry Capture** is a lightweight Windows 11 desktop app that continuously downloads still JPEG images from up to **10 UK motorway traffic camera feeds** and saves them into a tidy, dated folder structure for later analysis.

It is a native desktop application (WPF / .NET 8) — its own window, not a browser tab — and runs entirely **locally**: no cloud, no telemetry, no external services other than fetching the camera images you configure.

> Sentry Capture only **collects and stores** images. It does not analyse them. Incident detection is a separate tool that will read the folders this app produces.

![Sentry Capture dashboard](Screenshot%202026-06-30%20203003.png)

---

## Quick start (for end users)

1. **Download / clone** this repository to a folder on your PC.
2. Double-click **`setup.bat`**.
   - Installs the .NET 8 SDK automatically (via `winget`) if it isn't already present.
   - Builds the app into a single self-contained `.exe` (under `publish\`).
   - Creates a **"Sentry Capture"** desktop shortcut.
3. Launch the app with **`launch.bat`** or the desktop shortcut.

> First-time setup needs an internet connection (to fetch the SDK and build). After that the app runs fully offline apart from fetching your camera images.

---

## Using the app

### Adding cameras
1. On the **Dashboard** tab, click **➕ Add Camera**.
2. Enter:
   - **Camera name** — e.g. `M6 J19 Northbound`. This is used to name the folders and files.
   - **Image URL** — the *direct JPEG URL* for that camera. You obtain this manually by inspecting the network requests on a public camera-viewing website (look for the `.jpg` request).
   - **Custom headers** *(optional)* — only needed if a feed rejects plain requests. One per line, e.g. `Referer: https://example-camera-site.co.uk`. A browser-like `User-Agent` is sent by default.
3. Click **Save**. Repeat for up to 10 cameras.

Cameras can be **added, edited, or removed at any time** — including while collection is running. Changes affect only that camera; the others keep running.

### Collecting
- **▶ Start All** begins polling every configured camera every **30 seconds**, independently.
- **■ Stop All** stops all polling.
- Each camera card shows a **status light** (🟢 healthy / 🔴 error / 🟡 starting / ⚪ stopped), the **image count** this session, and the **uptime**.

### Where images go
Images are saved next to the app under `SentryCapture_Data\`:

```
SentryCapture_Data/
  M6_J19_Northbound/
    2026-06-30/
      M6_J19_Northbound_2026-06-30_14-32-05.jpg
      M6_J19_Northbound_2026-06-30_14-32-35.jpg
    2026-07-01/
      ...
```

- One folder per camera (name sanitised for Windows).
- A new sub-folder per calendar day, created automatically at midnight.
- Files named `Name_YYYY-MM-DD_HH-mm-ss.jpg` so they sort chronologically.

Use **📂 Open Data Folder** to jump straight there in Explorer.

### Debug log
The **Debug Log** tab shows a timestamped, detailed log of every download attempt. Failures include the **full error detail** (exception type, HTTP status, stack trace) — exactly what you'd paste into an AI assistant to diagnose a problem.

- **📋 Copy Log** copies the visible log to the clipboard.
- The full history is also written to `logs\sentry-capture_YYYY-MM-DD.log` and survives restarts/crashes.
- **📂 Open Log Folder** opens that folder.

### Feedback
**✉ Send Feedback** opens your default email client with a pre-filled message to `purplepenguin.apps@gmail.com`.

---

## Files & folders (portable layout)

Everything lives next to the executable, so the whole thing is portable:

| Path | Purpose |
|------|---------|
| `publish\SentryCapture.exe` | The built application |
| `publish\config.json` | Your camera configuration (auto-saved) |
| `publish\SentryCapture_Data\` | Captured images |
| `publish\logs\` | Daily log files |

> The runtime files above are created next to the `.exe`. The repo's own `config.json` / data / logs are git-ignored.

---

## For developers

```powershell
# Run from source
dotnet run --project src/SentryCapture/SentryCapture.csproj

# Produce the distributable single-file exe
dotnet publish src/SentryCapture/SentryCapture.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

- **Stack:** .NET 8, WPF (C#), MVVM, no third-party runtime dependencies.
- **Structure:**
  - `Models/` — `CameraConfig`, `AppConfig`
  - `Services/` — `ConfigStore`, `CaptureManager`, `CameraWorker`, `ImageWriter`, `Logger`, `AppPaths`
  - `ViewModels/` — `MainViewModel`, `CameraViewModel`
  - `Views/` — `CameraEditDialog`
  - `MainWindow.xaml`, `Themes/Styles.xaml`

---

## Scope

**In scope:** reliable, observable, long-running still-image collection.

**Out of scope (handled by a separate, later tool):** AI/vision inference, incident detection/classification/alerting, live video, and analysis of existing image folders.
