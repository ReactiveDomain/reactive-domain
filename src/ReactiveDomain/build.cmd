@echo off

set THISDIR=%~dp0
set NUGET=%THISDIR%\.nuget\Nuget.exe
set NUGETDIR=%THISDIR%\.nuget\
set SOLUTIONDIR=%THISDIR%
set NUSPECDIR=%THISDIR%\ReactiveDomain
set TESTNUSPECDIR=%THISDIR%\ReactiveDomain.Tests

%NUGET% update -self

echo Restore all nugets
%NUGET% restore %SOLUTIONDIR%\ReactiveDomain.sln -NoCache -NonInteractive -ConfigFile %NUGETDIR%MyGet.NuGet.Config


echo Building the ReactiveDomain Solution...
"%programfiles(x86)%\MSBuild\14.0\Bin\MSBuild.exe" %SOLUTIONDIR%\ReactiveDomain.sln /p:Configuration="Debug" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false


REM Make ReactiveDomain Nuget ***********************************************************************************************

echo Updating ReactiveDomain NuGet version in nuspec file...
PowerShell.exe -ExecutionPolicy Bypass -Command "& '%SOLUTIONDIR%\Tools\UpdateNugetVersion.ps1'"

echo Package ReactiveDomain NuGet using the nuspec file...
pushd %NUSPECDIR%
%NUGET% pack ReactiveDomain.nuspec
popd

echo Push the nuget to PKI private feed
%NUGET% push %NUSPECDIR%\*.nupkg %APIKEY% -source %PKIFEED%



REM Make ReactiveDomain.Tests Nuget ***********************************************************************************************

echo Updating ReactiveDomain.Tests NuGet version in nuspec file...
PowerShell.exe -ExecutionPolicy Bypass -Command "& '%SOLUTIONDIR%\Tools\UpdateTestsNugetVersion.ps1'"

echo Package using the nuspec file...
pushd %TESTNUSPECDIR%
%NUGET% pack ReactiveDomain.Tests.nuspec
popd

echo Push the nuget to PKI private feed
%NUGET% push %TESTNUSPECDIR%\*.nupkg %APIKEY% -source %PKIFEED%
