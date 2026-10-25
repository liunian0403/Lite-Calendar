#define AppName "轻历"
#define AppVersion "1.0.0"
#define AppExe "QingLi.exe"

[Setup]
AppId={{7C4B2A61-5D2E-4E8B-9A3F-1B6E2C0D9F31}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=轻历
AppComments=本地优先的 Windows 日程与提醒应用
DefaultDirName={localappdata}\Programs\QingLi
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=dist
OutputBaseFilename=QingLi-Setup-{#AppVersion}
SetupIconFile=build\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName} {#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
MinVersion=6.1sp1
ArchitecturesAllowed=x86compatible
CloseApplications=yes
RestartApplications=no
AllowNoIcons=no
AppMutex=QingLi.Calendar.SingleInstance
ChangesAssociations=no
LanguageDetectionMethod=none
ShowLanguageDialog=no

[Languages]
Name: "zh"; MessagesFile: "zh-CN.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "build\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.txt"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "立即启动 {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
function CmdLineParamExists(const Value: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Value) = 0 then
    begin
      Result := True;
      Exit;
    end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
  DoDelete: Boolean;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\QingLi');
    if DirExists(DataDir) then
    begin
      { 静默卸载时直接清理；交互卸载时询问一次 }
      DoDelete := CmdLineParamExists('/SILENT') or CmdLineParamExists('/VERYSILENT');
      if not DoDelete then
        DoDelete := MsgBox('是否同时删除本机的日程数据？' + #13#10#13#10 + DataDir,
                           mbConfirmation, MB_YESNO) = IDYES;
      if DoDelete then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
