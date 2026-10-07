; Per-user installer: no admin prompt. Build with: iscc /DAppVersion=1.2.3 installer\AlexaDesktop.iss
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{6F3B1C2E-8A4D-4C59-9E7B-2D5A1F0C3B84}
AppName=Alexa Desktop
AppVersion={#AppVersion}
AppVerName=Alexa Desktop {#AppVersion}
AppPublisher=Cartterr
AppPublisherURL=https://github.com/Cartterr/alexa-desktop
AppSupportURL=https://github.com/Cartterr/alexa-desktop/issues
DefaultDirName={localappdata}\Programs\Alexa Desktop
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
DisableDirPage=yes
OutputDir=..\dist
OutputBaseFilename=AlexaDesktop-Setup-{#AppVersion}
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\AlexaDesktop.exe
UninstallDisplayName=Alexa Desktop
LicenseFile=..\LICENSE
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
MinVersion=10.0

[Tasks]
Name: desktopicon; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: autostart; Description: "Start with Windows (in the system tray)"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{userprograms}\Alexa Desktop"; Filename: "{app}\AlexaDesktop.exe"
Name: "{userdesktop}\Alexa Desktop"; Filename: "{app}\AlexaDesktop.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AlexaDesktop"; ValueData: """{app}\AlexaDesktop.exe"" --background"; Tasks: autostart
; The app can also turn autostart on from its tray menu, so always clean it up on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "AlexaDesktop"; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\AlexaDesktop"; ValueType: none; Flags: uninsdeletekey

[Run]
Filename: "{app}\AlexaDesktop.exe"; Description: "{cm:LaunchProgram,Alexa Desktop}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c taskkill /im AlexaDesktop.exe /f"; Flags: runhidden; RunOnceId: "StopApp"

[UninstallDelete]
; Sign-in cookies and cache.
Type: filesandordirs; Name: "{localappdata}\AlexaDesktop"
