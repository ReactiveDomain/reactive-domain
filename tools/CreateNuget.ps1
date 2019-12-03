# CreateNuget.ps1
#
# This script will get the fileversion of ReactiveDomain.Core.dll. 
# This version number will be used to create the corresponding ReactiveDomain nuget packages 
# The ReactiveDomain nugets are then pushed to nuget.org
# 
# Note: If build is unstable, a beta (pre release) version of the nuget will be pushed
#       If build is stable, a stable (release) version will be pushed

# branch must be master to create a nuget
$configuration = "Release"
$nuspecExtension = ".nuspec"
$masterString = "master"
$branch = $env:TRAVIS_BRANCH
$apikey = $env:NugetOrgApiKey

# create and push nuget off of master branch ONLY
if ($branch -ne $masterString)  
{
  Write-Host ("Not a master branch. Will not create nuget")   
  Exit
}

# This changes when its a CI build or a manually triggered via the web UI
# api --> means manual/stable build ;  push --> means CI/unstable build
# pull_request --> CI build triggered when opening a PR (do nothing here)
$buildType = $env:TRAVIS_EVENT_TYPE 

if ($buildType -eq "pull_request")  
{
  Write-Host ("Pull request build. Will not create nuget")   
  Exit
}  

if ($buildType -eq "debug")  
{
  Write-Host ("Debug build. Will create debug nuget") 
  $configuration = "Debug" 
  $nuspecExtension = ".Debug.nuspec" 
}   
 

Write-Host ("Powershell script location is " + $PSScriptRoot)

$ReactiveDomainDll = $PSScriptRoot + "\..\bld\$configuration\net472\ReactiveDomain.Core.dll"
$RDVersion = (Get-Item $ReactiveDomainDll).VersionInfo.FileVersion
$ReactiveDomainNuspec = $PSScriptRoot + "\..\src\ReactiveDomain" + $nuspecExtension
$ReactiveDomainTestingNuspec = $PSScriptRoot + "\..\src\ReactiveDomain.Testing" + $nuspecExtension
$ReactiveDomainUINuspec = $PSScriptRoot + "\..\src\ReactiveDomain.UI" + $nuspecExtension
$ReactiveDomainUITestingNuspec = $PSScriptRoot + "\..\src\ReactiveDomain.UI.Testing" + $nuspecExtension
$nuget = $PSScriptRoot + "\..\src\.nuget\nuget.exe"

Write-Host ("Reactive Domain version is " + $RDVersion)
Write-Host ("Build type is " + $buildType)
Write-Host ("ReactiveDomain nuspec file is " + $ReactiveDomainNuspec)
Write-Host ("ReactiveDomain.Testing nuspec file is " + $ReactiveDomainTestingNuspec)
Write-Host ("ReactiveDomain.UI nuspec file is " + $ReactiveDomainUINuspec)
Write-Host ("ReactiveDomain.UI.Testing nuspec file is " + $ReactiveDomainUITestingNuspec)
Write-Host ("Branch is file is " + $branch)

function UpdateDependencyVersions([string]$Nuspec)
{

    Write-Host "Updating dependency versions of: " $Nuspec

    [xml]$xml = Get-Content -Path $Nuspec
    $dependencyNodes = $xml.package.metadata.dependencies.group.dependency

    foreach($node in $dependencyNodes)
    {
        if ($node.id.Contains("ReactiveDomain"))
        {
            $node.version = $RDVersion
        }
    }
    $xml.Save($Nuspec)

   Write-Host "Updated dependency versions of: $Nuspec"
}

& $nuget update -self

# Update the corresponding ReactiveDomain dependency versions in the nuspec files ***********************************************

UpdateDependencyVersions($ReactiveDomainTestingNuspec)
UpdateDependencyVersions($ReactiveDomainUINuspec)
UpdateDependencyVersions($ReactiveDomainUITestingNuspec)

# *******************************************************************************************************************************

# Pack the nuspec files to create the .nupkg files using the set versionString  *************************************************
$versionString = $RDVersion
& $nuget pack $ReactiveDomainNuspec -Version $versionString
& $nuget pack $ReactiveDomainTestingNuspec -Version $versionString
& $nuget pack $ReactiveDomainUINuspec -Version $versionString
& $nuget pack $ReactiveDomainUITestingNuspec -Version $versionString

# *******************************************************************************************************************************

# Push the nuget packages to nuget.org ******************************************************************************************
$ReactiveDomainNupkg = $PSScriptRoot + "\..\ReactiveDomain." + $versionString + ".nupkg"
$ReactiveDomainTestingNupkg = $PSScriptRoot + "\..\ReactiveDomain.Testing." + $versionString + ".nupkg"
$ReactiveDomainUINupkg = $PSScriptRoot + "\..\ReactiveDomain.UI." + $versionString + ".nupkg"
$ReactiveDomainUITestingNupkg = $PSScriptRoot + "\..\ReactiveDomain.UI.Testing." + $versionString + ".nupkg"

& $nuget push $ReactiveDomainNupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 
& $nuget push $ReactiveDomainTestingNupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 
& $nuget push $ReactiveDomainUINupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 
& $nuget push $ReactiveDomainUITestingNupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 

# *******************************************************************************************************************************