<p align="center">
  <img src="assets/icon.png" width="96" alt="Alexa Desktop icon">
</p>

<h1 align="center">Alexa Desktop</h1>

<p align="center">
  A native Windows app for <b>Alexa+</b>, the generative-AI Alexa.<br>
  Unofficial. Not made by, affiliated with, or endorsed by Amazon.
</p>

<p align="center">
  <a href="https://github.com/Cartterr/alexa-desktop/releases/latest"><img alt="Download" src="https://img.shields.io/github/v/release/Cartterr/alexa-desktop?label=download"></a>
  <a href="https://github.com/Cartterr/alexa-desktop/actions/workflows/build.yml"><img alt="Build" src="https://github.com/Cartterr/alexa-desktop/actions/workflows/build.yml/badge.svg"></a>
  <img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4">
  <a href="LICENSE"><img alt="MIT" src="https://img.shields.io/badge/license-MIT-green"></a>
</p>

---

Amazon removed its Windows Alexa app from the Microsoft Store in 2023, and Alexa+ on PC now lives only at [alexa.com](https://alexa.com) in a browser tab. Alexa Desktop puts it back on Windows as a real app: its own window, a tray icon, global shortcuts and start-with-Windows.

It runs Amazon's own Alexa+ web app inside Microsoft Edge WebView2. Every feature of the web app works exactly as it does at alexa.com, and it stays current automatically when Amazon ships changes.

## Everything Alexa+ on the web does

| Section | What you can do |
| --- | --- |
| **Chat** | Type or dictate (speech-to-text), upload files and documents, create images, plan events, shop |
| **Recent chats** | Pick up conversations started on Echo, Fire TV, the Alexa phone app or the web |
| **Smart Home** | Control favorite lights, switches and other devices |
| **Lists** | Shopping list and custom lists |
| **Calendar** | Month view, add events |
| **Reminders & Tasks** | Upcoming and completed items, add new ones |
| **Notes & Files** | Notes, documents, images, e-mails and web pages you shared with Alexa |
| **Music** | Amazon Music playback with a mini player |
| **Settings** | Profiles, light/dark/auto theme, language, privacy |

## What the desktop app adds

| | |
| --- | --- |
| **Own window and taskbar icon** | Remembers its size, position and monitor |
| **System tray** | Closing the window keeps Alexa (and music) running in the tray |
| **Win + Alt + A** | Show or hide Alexa from anywhere, with the cursor in "Ask Alexa" |
| **Win + Alt + V** | Show Alexa and start talking (presses the mic button) |
| **Start with Windows** | Optional, starts quietly in the tray |
| **No permission pop-ups** | Microphone, location, notifications and paste are allowed for Amazon sites only |
| **Links open in your browser** | Anything outside Amazon opens in your default browser |
| **Dark title bar** | Follows the Windows light/dark setting |
| **Single instance** | Launching it again brings the existing window forward |
| **Tiny** | About 1.5 MB. Uses .NET Framework 4.8 and WebView2, both built into Windows 10 and 11 |

## Install

1. Download `AlexaDesktop-Setup-x.y.z.exe` from the [latest release](https://github.com/Cartterr/alexa-desktop/releases/latest).
2. Run it. It installs for your user only, no admin rights needed.
3. Sign in with your Amazon account.

Prefer no installer? Download the `-portable.zip`, extract it anywhere and run `AlexaDesktop.exe`.

> **"Windows protected your PC"**: the app is not code-signed yet, so SmartScreen warns on first run. Click **More info → Run anyway**. Each release lists SHA-256 checksums in `SHA256SUMS.txt`.

**Requirements:** Windows 10 or 11 and an Amazon account with Alexa+ access. Alexa+ availability depends on your account and region; if alexa.com works for you in a browser, it works here.

## Privacy

- The app only talks to Amazon, exactly like alexa.com in a browser. No telemetry, analytics or servers of its own.
- Your sign-in and cache are stored in `%LOCALAPPDATA%\AlexaDesktop`. Window position and preferences are in `HKEY_CURRENT_USER\Software\AlexaDesktop`.
- Uninstalling removes both, which signs you out.

## Build from source

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (8 or newer) on Windows.

```powershell
dotnet build src -c Release -o publish
publish\AlexaDesktop.exe
```

The installer is built with [Inno Setup](https://jrsoftware.org/isinfo.php): `iscc /DAppVersion=1.0.0 installer\AlexaDesktop.iss`.

Pushing a `v*` tag builds the installer and portable zip on GitHub Actions and publishes a release.

## Disclaimer

Alexa, Echo, Amazon and all related marks are trademarks of Amazon.com, Inc. or its affiliates. This project is an independent, unofficial client. It shows Amazon's own web app and does not modify it, scrape it or call private APIs. Use of Alexa is subject to Amazon's terms.

## License

[MIT](LICENSE)
