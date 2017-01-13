@echo off

set THISDIR=%~dp0
set NUGET=%THISDIR%\.nuget\Nuget.exe
set NUGETDIR=%THISDIR%\.nuget\
set SOLUTIONDIR=%THISDIR%
set NUSPECDIR=%THISDIR%\Greylock.Presentation.Core

%NUGET% update -self

echo Restore all nugets
%NUGET% restore %SOLUTIONDIR%\Greylock.sln -NoCache -NonInteractive -ConfigFile %NUGETDIR%MyGet.NuGet.config


echo Building the Greylock Solution...
"%programfiles(x86)%\MSBuild\14.0\Bin\MSBuild.exe" Greylock.sln /p:Configuration="Debug" /m /v:M /fl /flp:LogFile=msbuild.log;Verbosity=Normal /nr:false


echo Updating NuGet version in nuspec file...
PowerShell.exe -ExecutionPolicy Bypass -Command "& '%SOLUTIONDIR%\Tools\UpdateNugetVersion.ps1'"


echo Package using the nuspec file...
pushd %NUSPECDIR%
%NUGET% pack Greylock.Presentation.Core.nuspec
popd


echo Push the nuget to PKI private feed
%NUGET% push %NUSPECDIR%\*.nupkg 345541e5-c350-41fd-9541-2c1d091f5190 -source https://www.myget.org/F/pki_invivo_private/api/v2/package
