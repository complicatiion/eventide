; Eventide installer (Inno Setup 6.x).
;
; Build:
;   Automatic: every Release build in Visual Studio (or "dotnet build -c Release") runs ISCC after the build –
;   see the target "EventideInstaller" in src\Eventide.App\Eventide.App.csproj. Output: artifacts\eventide_v<version>_setup.exe.
;
;   Manual: build the Release configuration, then open this file in the Inno Setup Compiler and press Compile (Ctrl+F9).
;   Input is the normal (self-contained) build folder:
;     src\Eventide.App\bin\Release\net10.0-windows10.0.19041.0\win-x64\
;   Another folder (e.g. the publish output of build\publish.ps1) can be passed with /DSourceDir=<folder>.
;
;   The version is read from Eventide.exe (product version), so it never has to be passed by hand.
;
; English is the default language; German is offered in the language dialog.
; Targets: Windows 10 / 11, 64-bit.
;
; Extended gamma range: an own wizard page explains GdiIcmGammaRange (HKLM, system-wide) and lets the user decide
; (checked by default). Only when Eventide sets the value it writes the marker
; HKLM\SOFTWARE\sksdesign\Eventide\GammaRangeSetByEventide = 1; the uninstaller removes the value only in that case,
; so tools like f.lux that rely on it keep working.

#ifndef SourceDir
  #define SourceDir SourcePath + "..\..\src\Eventide.App\bin\Release\net10.0-windows10.0.19041.0\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir SourcePath + "..\..\artifacts"
#endif

#define AppExe AddBackslash(SourceDir) + "Eventide.exe"
#if !FileExists(AppExe)
  #error Eventide.exe not found in the build folder. Build the Release configuration first (Visual Studio: Release, Build) or pass /DSourceDir=<folder>.
#endif

#ifndef AppVersion
  #define AppVersion GetStringFileInfo(AppExe, "ProductVersion")
#endif
#if AppVersion == ""
  #undef AppVersion
  #define AppVersion GetVersionNumbersString(AppExe)
#endif

[Setup]
AppId={{6E1B7C3A-52D4-4B7E-9A1F-0E7D5C2B9A41}
AppName=Eventide
AppVersion={#AppVersion}
AppVerName=Eventide {#AppVersion}
AppPublisher=sksdesign | complicatiion
AppPublisherURL=https://sksdesign.de
AppSupportURL=https://github.com/complicatiion/Eventide
AppCopyright=Copyright (C) 2026 Sven Klocke
DefaultDirName={autopf}\Eventide
DefaultGroupName=Eventide
DisableProgramGroupPage=yes
LicenseFile=..\..\licenses\LICENSE.md
OutputDir={#OutputDir}
OutputBaseFilename=eventide_v{#AppVersion}_setup
SetupIconFile=..\..\src\Eventide.App\Assets\eventide.ico
UninstallDisplayIcon={app}\Eventide.exe
UninstallDisplayName=Eventide {#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
WizardImageFile=images\wizard-100.bmp,images\wizard-125.bmp,images\wizard-150.bmp,images\wizard-175.bmp,images\wizard-200.bmp,images\wizard-225.bmp,images\wizard-250.bmp
WizardSmallImageFile=images\wizard-small-100.bmp,images\wizard-small-125.bmp,images\wizard-small-150.bmp,images\wizard-small-175.bmp,images\wizard-small-200.bmp,images\wizard-small-225.bmp,images\wizard-small-250.bmp
WizardImageStretch=no
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Admin: Program Files and the system-wide gamma range value (HKLM).
PrivilegesRequired=admin
CloseApplications=no
RestartApplications=no
ShowLanguageDialog=yes
LanguageDetectionMethod=none

[Languages]
; First entry = default
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
en.GroupExtras=Additional options:
de.GroupExtras=Weitere Optionen:
en.TaskAutostart=Start Eventide with Windows (system tray, current user)
de.TaskAutostart=Eventide mit Windows starten (System-Tray, aktueller Benutzer)
en.TaskDesktopIcon=Create a desktop shortcut (opens the settings)
de.TaskDesktopIcon=Desktop-Verknüpfung erstellen (öffnet die Einstellungen)
en.GammaCaption=Extended colour range
de.GammaCaption=Erweiterter Farbbereich
en.GammaDescription=Needed for the intensities "Strong" and "Extreme"
de.GammaDescription=Nötig für die Intensitäten „Stark“ und „Extrem“
en.GammaText=By default Windows only accepts mild colour changes through the display gamma ramp. Warm values below about 4000 K (Strong ≈ 3200 K, Extreme ≈ 2700 K) need the system-wide registry value GdiIcmGammaRange = 256 under HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM.%n%nThe value applies to all users of this PC and is also used by other colour tools such as f.lux. It takes effect after signing in again or a restart. Eventide removes it on uninstall only if Eventide set it.%n%nYou can change this later in Eventide › Settings › General.
de.GammaText=Standardmäßig akzeptiert Windows über die Gamma-Rampe nur leichte Farbänderungen. Warme Werte unter ca. 4000 K (Stark ≈ 3200 K, Extrem ≈ 2700 K) brauchen den systemweiten Registry-Wert GdiIcmGammaRange = 256 unter HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM.%n%nDer Wert gilt für alle Benutzer dieses PCs und wird auch von anderen Farbtools wie f.lux genutzt. Er wirkt nach einer Neuanmeldung oder einem Neustart. Eventide entfernt ihn bei der Deinstallation nur, wenn Eventide ihn gesetzt hat.%n%nDu kannst das später unter Eventide › Einstellungen › Allgemein ändern.
en.GammaOption=Enable the extended gamma range (recommended)
de.GammaOption=Erweiterten Gamma-Bereich aktivieren (empfohlen)
en.GammaAlready=The extended gamma range is already enabled on this PC – nothing to change.
de.GammaAlready=Der erweiterte Gamma-Bereich ist auf diesem PC bereits aktiviert – nichts zu ändern.
en.GammaRestart=The extended gamma range was enabled. Sign out and in again (or restart Windows) so that Windows accepts the strong colour values.
de.GammaRestart=Der erweiterte Gamma-Bereich wurde aktiviert. Melde dich ab und wieder an (oder starte Windows neu), damit Windows die starken Farbwerte akzeptiert.
en.LaunchEventide=Start Eventide
de.LaunchEventide=Eventide starten
en.PurgeQuestion=Also delete your Eventide settings?
de.PurgeQuestion=Auch deine Eventide-Einstellungen löschen?

[Tasks]
Name: "autostart"; Description: "{cm:TaskAutostart}"; GroupDescription: "{cm:GroupExtras}"
Name: "desktopicon"; Description: "{cm:TaskDesktopIcon}"; GroupDescription: "{cm:GroupExtras}"; Flags: unchecked

[Files]
; Build output without debug symbols and without portable data
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,Eventide.portable,data\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Eventide"; Filename: "{app}\Eventide.exe"
Name: "{group}\Eventide – Settings"; Filename: "{app}\Eventide.exe"; Parameters: "--settings"
Name: "{autodesktop}\Eventide"; Filename: "{app}\Eventide.exe"; Parameters: "--settings"; Tasks: desktopicon

[Run]
; Autostart is a per-user Run key: written as the user who started the setup, not as the elevated admin.
Filename: "{app}\Eventide.exe"; Parameters: "--register-autostart"; Flags: runhidden waituntilterminated runasoriginaluser; Tasks: autostart
Filename: "{app}\Eventide.exe"; Parameters: "--unregister-autostart"; Flags: runhidden waituntilterminated runasoriginaluser; Tasks: not autostart
Filename: "{app}\Eventide.exe"; Description: "{cm:LaunchEventide}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
; Restore the display colours and quit the tray, then remove the autostart entry of the uninstalling user.
Filename: "{app}\Eventide.exe"; Parameters: "--exit"; Flags: runhidden waituntilterminated; RunOnceId: "ExitTray"
Filename: "{app}\Eventide.exe"; Parameters: "--unregister-autostart"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveAutostart"

[Code]
const
  IcmKey = 'SOFTWARE\Microsoft\Windows NT\CurrentVersion\ICM';
  GammaValue = 'GdiIcmGammaRange';
  MarkerKey = 'SOFTWARE\sksdesign\Eventide';
  MarkerValue = 'GammaRangeSetByEventide';

var
  GammaPage: TInputOptionWizardPage;
  GammaChanged: Boolean;

function GammaAlreadyExtended(): Boolean;
var
  Value: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, IcmKey, GammaValue, Value) and (Value >= 256);
end;

procedure InitializeWizard();
begin
  GammaPage := CreateInputOptionPage(wpSelectTasks, CustomMessage('GammaCaption'), CustomMessage('GammaDescription'),
    CustomMessage('GammaText'), False, False);
  GammaPage.Add(CustomMessage('GammaOption'));
  GammaPage.Values[0] := True;
  if GammaAlreadyExtended() then
  begin
    GammaPage.Values[0] := True;
    GammaPage.CheckListBox.ItemEnabled[0] := False;
    GammaPage.SubCaptionLabel.Caption := CustomMessage('GammaText') + #13#10#13#10 + CustomMessage('GammaAlready');
  end;
end;

{ Quit a running tray before files are replaced (restores the colours first). }
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  if FileExists(ExpandConstant('{app}\Eventide.exe')) then
    Exec(ExpandConstant('{app}\Eventide.exe'), '--exit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and GammaPage.Values[0] and (not GammaAlreadyExtended()) then
  begin
    if RegWriteDWordValue(HKLM64, IcmKey, GammaValue, 256) then
    begin
      RegWriteDWordValue(HKLM64, MarkerKey, MarkerValue, 1);
      GammaChanged := True;
    end;
  end;
end;

function NeedRestart(): Boolean;
begin
  Result := GammaChanged;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and GammaChanged then
    WizardForm.FinishedLabel.Caption := WizardForm.FinishedLabel.Caption + #13#10#13#10 + CustomMessage('GammaRestart');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Marker: Cardinal;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Only undo what Eventide did itself. }
    if RegQueryDWordValue(HKLM64, MarkerKey, MarkerValue, Marker) and (Marker = 1) then
      RegDeleteValue(HKLM64, IcmKey, GammaValue);
    RegDeleteKeyIncludingSubkeys(HKLM64, MarkerKey);
    RegDeleteKeyIfEmpty(HKLM64, 'SOFTWARE\sksdesign');
  end;

  if (CurUninstallStep = usPostUninstall) and (not UninstallSilent) and
     (MsgBox(CustomMessage('PurgeQuestion'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
    DelTree(ExpandConstant('{userappdata}\Eventide'), True, True, True);
end;
