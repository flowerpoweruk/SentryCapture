# Sentry Capture — Build Brief for Claude Code CLI

## 1. Project Overview

**Sentry Capture** is a standalone Windows 11 desktop application that continuously downloads still images from up to ten configurable UK motorway traffic camera feeds (e.g. publicly available M6 camera images), saving them to a structured local folder hierarchy for later analysis.

This is the **first of two separate tools** in a larger project:
1. **Sentry Capture** (this brief) — collects and stores camera images.
2. **A separate analysis tool** (not part of this brief) — will later read the image folders produced by Sentry Capture and run them through a local vision model (NVIDIA Nemotron 3 Nano Omni via Ollama) to detect road incidents (crashes, debris, stopped vehicles, congestion).

Sentry Capture's only job is **reliable, observable, long-running image collection**. It must not attempt any image analysis, AI inference, or incident detection — that is explicitly out of scope and belongs to the second tool.

## 2. Platform & Tech Requirements

- **Target OS:** Windows 11 (desktop only, no browser/web UI)
- **App type:** Native-feeling standalone desktop application with its own window (not a browser tab, not accessed via `localhost` in a browser). A lightweight desktop framework that can package as a Windows executable is appropriate (e.g. Electron, Tauri, or .NET/WPF/WinUI — use your judgement on what will give the cleanest result and easiest one-click packaging for a non-developer end user).
- **Distribution:** Will be published to GitHub. Needs:
  - A **one-click setup script** (installs all dependencies, no manual steps for the user).
  - A **one-click launch file** to start the app after setup.
- **No cloud dependency.** Everything runs locally. No telemetry, no external services other than fetching the camera images themselves.

## 3. Core Functionality

### 3.1 Camera Management
- Supports up to **10 camera feeds**, but works correctly with fewer (e.g. 4 or 5) configured at once.
- For each camera, the user can configure:
  - A **custom name** (user-defined, e.g. "M6 J19 Northbound") — this name is used to build folder and file names.
  - The **image URL** to poll (the direct JPEG URL for that camera, which the user will obtain manually by inspecting network requests on public camera-viewing websites).
- Cameras can be **added, edited, or removed while the app is running**, without needing to stop the collection process. Changes take effect immediately for that camera only — other cameras are unaffected.
- The app should gracefully handle any number of active cameras between 1 and 10.

### 3.2 Polling / Download Behaviour
- Each active camera's image URL is polled and the JPEG downloaded **every 30 seconds**.
- Downloads for different cameras happen independently of each other (one camera failing or being slow must not block or delay others).
- If a download fails (timeout, 404, connection error, corrupted/non-image response, etc.), this is treated as a non-fatal error for that camera: it's logged in detail (see Section 3.5) and the app continues retrying on the normal 30-second cycle.

### 3.3 File & Folder Structure
- Root output folder, e.g. `SentryCapture_Data/`.
- Inside that, one folder per camera, named using the **user-defined camera name** (sanitised for filesystem safety, e.g. spaces/special characters handled sensibly).
- Inside each camera folder, a **new subfolder per calendar day** (e.g. `2026-06-30`), created automatically when the day rolls over.
- Inside each daily folder, individual image files named using the camera name and a timestamp (e.g. `M6_J19_Northbound_2026-06-30_14-32-05.jpg`), so files sort chronologically and are unambiguous when viewed later in Windows Explorer.

Example structure:
```
SentryCapture_Data/
  M6_J19_Northbound/
    2026-06-30/
      M6_J19_Northbound_2026-06-30_14-32-05.jpg
      M6_J19_Northbound_2026-06-30_14-32-35.jpg
      ...
    2026-07-01/
      ...
  M6_J15_StokeOnTrent/
    2026-06-30/
      ...
```

### 3.4 Dashboard / Main UI
A clean, modern, minimalistic interface (see Section 4) showing, **per camera**, in real time:
- **Status indicator**: green light = currently downloading successfully; red light = currently failing/erroring.
- **Image count**: total number of images successfully captured for that camera since the app/camera was started.
- **Uptime**: how long that camera has been actively running/monitored.

Global controls:
- **Start All** / **Stop All** — starts or stops polling for every configured camera simultaneously.
- Add / Edit / Remove camera (name + URL), available at any time, including while running.

### 3.5 Debug Log
- A **detailed debug log**, visible within the app (and also written to a log file on disk so history persists across sessions/crashes).
- Each log entry includes:
  - A **timestamp**.
  - What happened (success or failure for a given camera's download attempt).
  - On failure: the **actual error detail** (exception message, HTTP status code, stack trace / underlying code-level error info) — not a vague "something went wrong" message. The user has explicitly said this internal detail is important because it's what they'll copy and paste into Claude Code to diagnose issues.
- The log should be easy to select and copy in full (e.g. a text panel with a "copy log" button), since the user's workflow is to paste it directly into an AI coding assistant for troubleshooting.

### 3.6 Feedback Button
- A visible **"Send Feedback"** button/menu item in the app.
- Clicking it opens the user's default email client with a pre-filled `mailto:` link addressed to **purplepenguin.apps@gmail.com**.

## 4. UI/UX Requirements

- **Clean, modern, minimalistic design.** Avoid clutter; prioritise clarity of the per-camera status information.
- Should feel like a lightweight monitoring/utility app — think a simple status dashboard, not a complex multi-pane application.
- Per-camera rows/cards should make it easy to glance and see at a glance which cameras are healthy (green) vs failing (red), image counts, and uptime.
- Debug log can be a collapsible/secondary panel or separate tab so it doesn't dominate the main dashboard view.

## 5. Explicitly Out of Scope (for this tool)

- No AI/vision model inference of any kind.
- No incident detection, classification, or alerting based on image content.
- No live video streaming/decoding — this tool only fetches periodic still JPEG images.
- No analysis of pre-existing image folders — that will be a **separate** tool built later, which will simply read the folder structure Sentry Capture produces.

## 6. Suggested Build Approach for Claude Code

1. Scaffold the desktop app shell (choose the framework best suited for a clean one-click-packaged Windows 11 app).
2. Implement the camera configuration model (add/edit/remove, persisted locally e.g. to a local config file so settings survive a restart).
3. Implement the per-camera polling/download loop (independent timers per camera, 30s interval, robust error handling).
4. Implement the folder/file naming and creation logic (per-camera folder, per-day subfolder, timestamped filenames).
5. Implement the dashboard UI (status lights, image counts, uptime, start/stop all).
6. Implement the debug log (in-app panel + persisted log file + copy-to-clipboard).
7. Implement the Send Feedback mailto button.
8. Write the one-click setup script and one-click launch file.
9. Prepare for GitHub publishing (README, basic usage instructions reflecting the above).

## 7. Naming

- Application name: **Sentry Capture**
