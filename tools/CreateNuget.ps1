# CreateNuget.ps1
#
# This script will get the fileversion of ReactiveDomain.Core.dll. 
# This version number will be used to create the corresponding nuget package 
# The nuget is then pushed to nuget.org
# 
# Note: If build is unstable, a beta (pre release) version of the nuget will be pushed
#       If build is stable, a stable (release) version will be pushed

# branch must be master to create a nuget
$masterString = "update-nuspec-for-builds"
$branch = $env:TRAVIS_BRANCH
$apikey = $env:NugetOrgApiKey
$githubToken = $env:GithubApiToken

# create and push nuget off of master branch ONLY
if ($branch -ne $masterString)  
{
  Write-Host ("Not a master branch. Will not create nuget")   
  Exit
}

# This changes when its a CI build or a manually triggered via the web UI
# api --> means manual/stable build ;  push --> means CI/unstable build
$buildType = $env:TRAVIS_EVENT_TYPE    


Write-Host ("Powershell script location is " + $PSScriptRoot)

$ReactiveDomainDll = $PSScriptRoot + "\..\bld\Release\net472\ReactiveDomain.Core.dll"
$RDVersion = (Get-Item $ReactiveDomainDll).VersionInfo.FileVersion
$ReactiveDomainNuspec = $PSScriptRoot + "\..\src\ReactiveDomain.nuspec"
$ReactiveDomainTestingNuspec = $PSScriptRoot + "\..\src\ReactiveDomain.Testing.nuspec"
$ReactiveDomainUINuspec = $PSScriptRoot + "\..\src\ReactiveDomain.UI.nuspec"
$ReactiveDomainUITestingNuspec = $PSScriptRoot + "\..\src\ReactiveDomain.UI.Testing.nuspec"
$nuget = $PSScriptRoot + "\..\src\.nuget\nuget.exe"

Write-Host ("Reactive Domain version is " + $RDVersion)
Write-Host ("Build type is " + $buildType)
Write-Host ("ReactiveDomain nuspec file is " + $ReactiveDomainNuspec)
Write-Host ("ReactiveDomain.Testing nuspec file is " + $ReactiveDomainTestingNuspec)
Write-Host ("ReactiveDomain.UI nuspec file is " + $ReactiveDomainUINuspec)
Write-Host ("ReactiveDomain.UI.Testing nuspec file is " + $ReactiveDomainUITestingNuspec)
Write-Host ("Branch is file is " + $branch)

& $nuget update -self

# Set the version string:
#     If the Travis Build type is a push then that means its a build from a push from a feature branch (nuget should be beta)
#     If Travis Build type is api then that means the build was manually triggered and string should be stable
$versionString = ""

if ($buildType -eq "push" )
{
  $versionString = $RDVersion + "-beta"
  Write-Host ("This is an unstable master build. pushing unstable nuget version: " + $versionString)
}

if ($buildType -eq "api" )
{
  $versionString = $RDVersion
  Write-Host ("This is a stable master build. pushing stable nuget version: " + $versionString)
}

# Pack the nuspec files to create the .nupkg files using the set versionString
& $nuget pack $ReactiveDomainNuspec -Version $versionString
& $nuget pack $ReactiveDomainTestingNuspec -Version $versionString
& $nuget pack $ReactiveDomainUINuspec -Version $versionString
& $nuget pack $ReactiveDomainUITestingNuspec -Version $versionString


# Push the nuget packages to nuget.org
$ReactiveDomainNupkg = $PSScriptRoot + "\..\ReactiveDomain." + $versionString + ".nupkg"
$ReactiveDomainTestingNupkg = $PSScriptRoot + "\..\ReactiveDomain.Testing." + $versionString + ".nupkg"
$ReactiveDomainUINupkg = $PSScriptRoot + "\..\ReactiveDomain.UI." + $versionString + ".nupkg"
$ReactiveDomainUITestingNupkg = $PSScriptRoot + "\..\ReactiveDomain.UI.Testing." + $versionString + ".nupkg"

& $nuget push $ReactiveDomainNupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 
& $nuget push $ReactiveDomainTestingNupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 
& $nuget push $ReactiveDomainUINupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 
& $nuget push $ReactiveDomainUITestingNupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 

