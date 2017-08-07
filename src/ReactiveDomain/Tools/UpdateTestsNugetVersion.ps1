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

$packages_Config = "$solutiondir\ReactiveDomain.Tests\packages.config"
Write-Host "UpdateNuGetVersion:  Packages configuration file is: " $packages_Config

Write-Host "Solution dir is " $solutiondir

$path = "$solutiondir\ReactiveDomain.Tests\ReactiveDomain.Tests.nuspec"
Write-Host "NuSpec File is" $path

$dll = "$solutiondir\ReactiveDomain.Tests\bin\x64\Debug\ReactiveDomain.Tests.dll" 
Write-Host "Loading ReactiveDomain.Tests dll: " $dll

$Assembly = [Reflection.Assembly]::Loadfile($dll)
$AssemblyName = $Assembly.GetName()
$Assemblyversion = $AssemblyName.version.ToString()

Write-Host 
Write-Host "ReactiveDomain.Tests Assembly version is" $Assemblyversion

# Get version of the Dependent packages that are installed
$packagexml = [xml](Get-Content $packages_Config)

$FA = $packagexml.SelectSingleNode('//packages/package[@id="FluentAssertions"]')
$FluentAssertionsVersion = $FA.version
Write-Host "FluentAssertions Nuget version is" $FluentAssertionsVersion

$xunit = $packagexml.SelectSingleNode('//packages/package[@id="xunit"]')
$xunitVersion = $xunit.version
Write-Host "Xunit Nuget version is" $xunitVersion

$xunitabstractions = $packagexml.SelectSingleNode('//packages/package[@id="xunit.abstractions"]')
$xunitabstractionsVersion = $xunitabstractions.version
Write-Host "Xunit abstractions Nuget version is" $xunitabstractionsVersion

$xunitassert = $packagexml.SelectSingleNode('//packages/package[@id="xunit.assert"]')
$xunitassertVersion = $xunitassert.version
Write-Host "Xunit assert Nuget version is" $xunitassertVersion

$xunitcore = $packagexml.SelectSingleNode('//packages/package[@id="xunit.core"]')
$xunitcoreVersion = $xunitcore.version
Write-Host "Xunit core Nuget version is" $xunitcoreVersion

$xunitEC = $packagexml.SelectSingleNode('//packages/package[@id="xunit.extensibility.core"]')
$xunitECVersion = $xunitEC.version
Write-Host "Xunit.extensibility.core Nuget version is" $xunitECVersion

$xunitEE = $packagexml.SelectSingleNode('//packages/package[@id="xunit.extensibility.execution"]')
$xunitEEVersion = $xunitEE.version
Write-Host "Xunit extensibility.execution Nuget version is" $xunitEEVersion

$xunitRM = $packagexml.SelectSingleNode('//packages/package[@id="xunit.runner.msbuild"]')
$xunitRMVersion = $xunitRM.version
Write-Host "Xunit.runner.msbuild Nuget version is" $xunitRMVersion

$xunitRV = $packagexml.SelectSingleNode('//packages/package[@id="xunit.runner.visualstudio"]')
$xunitRVVersion = $xunitRV.version
Write-Host "Xunit.runner.visualstudio Nuget version is" $xunitRVVersion


# Open reactiveDomain.tests.nuspec file for editing
$xml = [xml](Get-Content $path)
$node = $xml.package.metadata

#Set nuget package to be version of the ReactiveDomain.Tests assembly
$node.version = $Assemblyversion

# Modify the .nuspec file to get the versions of the dependencies
$faNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="FluentAssertions"]')
$faNode.version = $FluentAssertionsVersion

$xunitNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="xunit"]')
$xunitNode.version = $xunitVersion

$xunitabsNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="xunit.abstractions"]')
$xunitabsNode.version = $xunitabstractionsVersion

$xunitassertNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="xunit.assert"]')
$xunitassertNode.version = $xunitassertVersion

$xunitcoreNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="xunit.core"]')
$xunitcoreNode.version = $xunitcoreVersion

$xunitECNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="xunit.extensibility.core"]')
$xunitECNode.version = $xunitECVersion

$xunitEENode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="xunit.extensibility.execution"]')
$xunitEENode.version = $xunitEEVersion

$xunitRMNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="xunit.runner.msbuild"]')
$xunitRMNode.version = $xunitRMVersion

$xunitRVNode = $xml.SelectSingleNode('//package/metadata/dependencies/dependency[@id="xunit.runner.visualstudio"]')
$xunitRVNode.version = $xunitRVVersion

# Save the .nuspec file (this will be used to pack the nuget)
$xml.Save($path)
