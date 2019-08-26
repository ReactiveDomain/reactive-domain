# UpdateAssemblyVersion.ps1
#
# This script will check the build.props file to get current assembly version of ReactiveDomain.Core.dll. 
# This version number will be incremented by 1 before a build if build is off of the master branch
#

$branch = $env:TRAVIS_BRANCH

# Only update the assembly version on master branch for the sake of creating a new nuget package
if ($branch -ne "update-nuspec-for-builds")  
{
  Write-Host ("Not a master branch. Assembly version will remain the same")   
  Exit
}

Write-Host ("Powershell script location is " + $PSScriptRoot)

$buildProps = $PSScriptRoot + "\..\src\build.props"
$props = [xml] (get-content $buildProps)

# Get the assemnbly and file version from build.props
$assemblyVersionNode = $props.SelectSingleNode("//Project/PropertyGroup/AssemblyVersion")
$fileVersionNode = $props.SelectSingleNode("//Project/PropertyGroup/FileVersion")

Write-Host ("Version node is " + $assemblyVersionNode.InnerText )

$major = $assemblyVersionNode.InnerText.Split('.')[0]
$minor = $assemblyVersionNode.InnerText.Split('.')[1]
$build = $assemblyVersionNode.InnerText.Split('.')[2]
$revision = $assemblyVersionNode.InnerText.Split('.')[3]
[int]$newRevision = 999
[bool]$result = [int]::TryParse($revision, [ref]$newRevision)

Write-Host ("revision is " + $revision )

$newRevision = $newRevision + 1
Write-Host ("New revision will be is " + $newRevision )

$newAssemblyVersion = $major + "." + $minor + "." + $build + "." + $newRevision
Write-Host ("New assembly version will be is " + $newAssemblyVersion )

#Update the props file with the revision number incremented by 1
$assemblyVersionNode.InnerText = $newAssemblyVersion
$fileVersionNode.InnerText = $newAssemblyVersion

$props.Save($buildProps)

