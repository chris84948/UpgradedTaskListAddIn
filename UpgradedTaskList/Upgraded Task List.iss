[Setup]
AppName=Upgraded Task List
AppVerName=Upgraded Task List v1.0.0
DefaultDirName={userdocs}
UsePreviousAppDir=false
DirExistsWarning=no
VersionInfoVersion=1.0.0
VersionInfoCompany=chrisbjohnsondev
VersionInfoDescription=Like Task List, But Better
VersionInfoProductName=Upgraded Task List
VersionInfoProductVersion=1.0.0

[Types]
Name: "Install"; Description: "Specify Visual Studio Versions"; Flags: isCustom

[Components]
Name: "VS2010"; Description: "Visual Studio 2010"; Types: Install
Name: "VS2012"; Description: "Visual Studio 2012"; Types: Install
Name: "VS2013"; Description: "Visual Studio 2013"; Types: Install

[Files]
Source: UpgradedTaskList - 2010.AddIn; DestDir: {userdocs}\Visual Studio 2010\Addins; DestName: UpgradedTaskList.AddIn; Components: VS2010
Source: bin\Debug\UpgradedTaskList.dll; DestDir: {userdocs}\Visual Studio 2010\Addins; DestName: UpgradedTaskList.dll; Components: VS2010
Source: UpgradedTaskList - 2012.AddIn; DestDir: {userdocs}\Visual Studio 2012\Addins; DestName: UpgradedTaskList.AddIn; Components: VS2012
Source: bin\Debug\UpgradedTaskList.dll; DestDir: {userdocs}\Visual Studio 2012\Addins; DestName: UpgradedTaskList.dll; Components: VS2012
Source: UpgradedTaskList - 2013.AddIn; DestDir: {userdocs}\Visual Studio 2013\Addins; DestName: UpgradedTaskList.AddIn; Components: VS2013
Source: bin\Debug\UpgradedTaskList.dll; DestDir: {userdocs}\Visual Studio 2013\Addins; DestName: UpgradedTaskList.dll; Components: VS2013

[UninstallDelete]
Name: {userdocs}\Visual Studio 2010\Addins\UpgradedTaskList.AddIn; Type: files
Name: {userdocs}\Visual Studio 2010\Addins\UpgradedTaskList.dll; Type: files

