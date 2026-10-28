#ifndef AppFiles
  #define AppFiles "..\..\outputs\release\ConverterAqui-2.1.0-win-x64"
#endif
#ifndef ReleaseOutput
  #define ReleaseOutput "..\..\outputs\release"
#endif
#define AppVersion "2.1.0"
[Setup]
AppId={{3864B938-15CE-4F39-855C-A569C9287CB1}
AppName=Converter Aqui
AppVersion={#AppVersion}
AppVerName=Converter Aqui {#AppVersion}
AppPublisher=Arthur Hermes
AppPublisherURL=https://github.com/ArthurHermes/converter-aqui
AppSupportURL=https://github.com/ArthurHermes/converter-aqui/issues
AppUpdatesURL=https://github.com/ArthurHermes/converter-aqui/releases
DefaultDirName={localappdata}\Programs\ConverterAqui
DefaultGroupName=Converter Aqui
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#ReleaseOutput}
OutputBaseFilename=ConverterAqui-{#AppVersion}-Setup-win-x64
Compression=lzma2/fast
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
UninstallDisplayIcon={app}\ConverterAqui.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
LicenseFile={#AppFiles}\LICENSE.txt

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "contextmenu"; Description: "Adicionar Converter para… ao botão direito do mouse"; Flags: checkedonce
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked

[Files]
Source: "{#AppFiles}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Converter Aqui"; Filename: "{app}\ConverterAqui.exe"
Name: "{autodesktop}\Converter Aqui"; Filename: "{app}\ConverterAqui.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ConverterAqui.exe"; Parameters: "--install"; Tasks: contextmenu; Flags: runhidden waituntilterminated
Filename: "{app}\ConverterAqui.exe"; Description: "Abrir Converter Aqui"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\ConverterAqui.exe"; Parameters: "--uninstall"; Flags: runhidden waituntilterminated skipifdoesntexist
