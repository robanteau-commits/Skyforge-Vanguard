# Building Skyforge Vanguard

Three platforms for v1: **Windows** (with an installer), **web** (WebGL) and **Android**. iOS
waits until there's a Mac. Every build reads its platform settings from
`Assets/Skyforge/Editor/SkyforgeBuild.cs`, so you don't need to change anything in Player Settings.

All builds land in `D:\Games\SkyforgeVanguard\Builds\` (Git ignores that folder).

## One-time setup: add the build modules
Unity needs a module per platform. You only do this once.
1. Close Unity. Open **Unity Hub** → **Installs** (left side).
2. On **Unity 6000.6.4f1**, click the **gear** icon → **Add modules**.
3. Tick **Web Build Support** and **Android Build Support** (with its **OpenJDK** and
   **Android SDK & NDK Tools** sub-items). Windows Build Support is already there.
4. Click **Install** (a few GB; it takes a while), then reopen the project.

For the Windows installer, also install Inno Setup once. In Command Prompt:
`winget install JRSoftware.InnoSetup`

## Windows
1. In Unity's top menu: **Skyforge → Build → Windows**. Wait for the progress bar to finish.
2. The game is `Builds\Windows\Skyforge Vanguard.exe` (double-click to play).
3. For the installer, in Command Prompt:
   ```
   cd /d D:\Games\SkyforgeVanguard
   powershell -ExecutionPolicy Bypass -File Installer\build-installer.ps1
   ```
   It writes `Builds\Installer\SkyforgeVanguardSetup-<version>.exe`.

## Web (WebGL)
1. **Skyforge → Build → Web (WebGL)**. The first web build takes several minutes.
2. The game is the `Builds\Web` folder (`index.html` plus a `Build` folder). It won't run by
   double-clicking `index.html`; browsers block that. To try it on your PC, in Command Prompt:
   ```
   cd /d D:\Games\SkyforgeVanguard\Builds\Web
   python -m http.server 8000
   ```
   then open <http://localhost:8000> in your browser (Ctrl+C in Command Prompt stops it).
3. To put it online: zip the **contents** of `Builds\Web` and upload the zip to itch.io as an
   HTML game ("This file will be played in the browser"), or copy the folder to any web host. It's
   built with Gzip plus a decompression fallback, so it works without special server settings.

The web build uses the wide PC field on desktop browsers and the portrait field on phone browsers.
Sound starts after your first click or tap (browsers require that).

## Android
1. **Skyforge → Build → Android (APK, to install on a phone)**. The first Android build takes a
   long time (10+ minutes).
2. The app is `Builds\Android\SkyforgeVanguard.apk`. To install it on your phone:
   - **USB:** on the phone, turn on Developer options (Settings → About phone → tap **Build number**
     seven times), then Settings → Developer options → **USB debugging** on. Plug the phone in,
     accept the prompt, and in Unity use **File → Build And Run** with Android selected instead;
     Unity installs and starts it.
   - **Or copy the file:** put the APK on the phone (Google Drive, USB cable or email), open it, and
     allow "install unknown apps" when asked.
3. It runs in portrait, with a pause button at the top and a core button in the bottom-right
   corner. Android's back button pauses.

### For Google Play (later)
Play needs an **App Bundle** signed with your own release key, not the debug key.
1. Make a key once (keep the file and passwords safe: losing them means you can't update the app):
   Unity → **Edit → Project Settings → Player → Android → Publishing Settings → Keystore Manager**.
2. Before building, set these environment variables (System Properties → Environment Variables),
   then restart Unity: `SKYFORGE_KEYSTORE` (the .keystore file's full path),
   `SKYFORGE_KEYSTORE_PASS`, `SKYFORGE_KEY_ALIAS`, `SKYFORGE_KEY_PASS`.
3. **Skyforge → Build → Android (App Bundle, for Google Play)** writes `Builds\Android\SkyforgeVanguard.aab`.

The Android package id is `com.robanteau.skyforgevanguard`, and the version code comes from
`bundleVersion` (0.7.0 → 700), so every new version can be installed over the old one.

## From a Claude session on Rob's PC
`bash Tools/claude/build.sh [windows|web|android]` builds through the open Editor (Windows also
makes the installer when Inno Setup is installed).
