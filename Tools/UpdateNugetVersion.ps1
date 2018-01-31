# UpdateNugetVersion.ps1: Build script to modify reactiveDomain .nuspec file
#			  Packages.config file will be checked for the correct
#			  Nuget version and will insert that version in the nuspec file	
#
# Author: Allen Thurman
#
# Revision: As Issued
#

$currentScriptDirectory = Get-Location
[System.IO.Directory]::SetCurrentDirectory($currentScriptDirectory.Path)

Set-Location -Path ${PSScriptRoot}
$solutiondir = Resolve-Path -Path "$currentScriptDirectory\src\ReactiveDomain"

$packages_Config = "$solutiondir\ReactiveDomain\packages.config"
Write-Host "UpdateNuGetVersion:  Packages configuration file is: " $packages_Config

Write-Host "Solution dir is " $solutiondir

$path = "$solutiondir\ReactiveDomain\ReactiveDomain.nuspec"
Write-Host "NuSpec File is" $path

$dll = "$solutiondir\ReactiveDomain\bin\x64\Debug\ReactiveDomain.dll" 
Write-Host "Loading ReactiveDomain dll: " $dll

$Assembly = [Reflection.Assembly]::Loadfile($dll)
$AssemblyName = $Assembly.GetName()
$Assemblyversion = $AssemblyName.version.ToString()

Write-Host 
Write-Host "ReactiveDomain Assembly version is" $Assemblyversion

# Get version of the Dependent packages that are installed
$packagexml = [xml](Get-Content $packages_Config)

$ES = $packagexml.SelectSingleNode('//packages/package[@id="EventStore.Client"]')
$EventStoreVersion = $ES.version
Write-Host "Event Store Nuget version is" $EventStoreVersion

$Tpl = $packagexml.SelectSingleNode('//packages/package[@id="Microsoft.Tpl.Dataflow"]')
$TplVersion = $Tpl.version
Write-Host "TPL Dataflow Nuget version is" $TplVersion

$NitoAsync = $packagexml.SelectSingleNode('//packages/package[@id="Nito.AsyncEx.Dataflow"]')
$NitoAsyncVersion = $NitoAsync.version
Write-Host "Nito Async Nuget version is" $NitoAsyncVersion

$NLog = $packagexml.SelectSingleNode('//packages/package[@id="NLog"]')
$NLogVersion = $NLog.version
Write-Host "NLog Nuget version is" $NLogVersion

$NugetCmd = $packagexml.SelectSingleNode('//packages/package[@id="NuGet.CommandLine"]')
$NugetCmdVersion = $NugetCmd.version
Write-Host "Nuget Commandline Nuget version is" $NugetCmdVersion

$ReactiveUI = $packagexml.SelectSingleNode('//packages/package[@id="reactiveui"]')
$ReactiveUIVersion = $ReactiveUI.version
Write-Host "ReactiveUI Nuget version is" $ReactiveUIVersion

$PlatformServices = $packagexml.SelectSingleNode('//packages/package[@id="Rx-PlatformServices"]')
$PlatformServicesVersion = $PlatformServices.version
Write-Host "Rx-PlatformServices Nuget version is" $PlatformServicesVersion

$RxXaml = $packagexml.SelectSingleNode('//packages/package[@id="Rx-XAML"]')
$RxXamlVersion = $RxXaml.version
Write-Host "Rx-XAML Nuget version is" $RxXamlVersion

$Splat = $packagexml.SelectSingleNode('//packages/package[@id="Splat"]')
$SplatVersion = $Splat.version
Write-Host "Splat Nuget version is" $SplatVersion


# Open reactiveDomain.nuspec file for editing
$xml = [xml](Get-Content $path)
$node = $xml.package.metadata

#Set nuget package to be version of the ReactiveDomain assembly
$node.version = $Assemblyversion

# Modify the nuspec file to get the versions of the dependencies
$esNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="EventStore.Client.Embedded"]')
$esNode.version = $EventStoreVersion

$TplNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="Microsoft.Tpl.DataFlow"]')
$TplNode.version = $TplVersion

$NitoNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="Nito.AsyncEx.Dataflow"]') 
$NitoNode.version = $NitoAsyncVersion

$NLogNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="NLog"]')
$NLogNode.version = $NLogVersion

$NugetCmdNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="NuGet.CommandLine"]')
$NugetCmdNode.version = $NugetCmdVersion

$ReactiveUINode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="ReactiveUI"]')
$ReactiveUINode.version = $ReactiveUIVersion

$RxplatformNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="Rx-PlatformServices"]')
$RxplatformNode.version = $PlatformServicesVersion

$RxXamlNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="Rx-XAML"]')
$RxXamlNode.version = $RxXamlVersion

$SplatNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="Splat"]')
$SplatNode.version = $SplatVersion

# Save the .nuspec file (this will be used to pack the nuget)
$xml.Save($path)
