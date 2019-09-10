# CheckAssemblyVersion.ps1
#
# This script will check the build.props file to get current assembly version on master branch
# It will then compare to the assembly version of the local branch
# If they are the same this script will exit with exitcode 2 therefore failing the travis build
#

$branch = $env:TRAVIS_BRANCH
$wget = $PSScriptRoot + "\..\tools\wget.exe"
$tempDir = $env:TEMP
$masterbuildProps = $tempDir + "\build.props"

# Master version is always used no need to check on master branch builds
if ($branch -eq "master")  
{
  Write-Host ("Master branch. Skipping assembly check")   
  Exit
}

# Echo the location of script for debug purposes
Write-Host ("Powershell script location is " + $PSScriptRoot)

if (Test-Path $masterbuildProps) 
{
  Remove-Item $masterbuildProps
}

# Clone just the build.props from master and get the current assembly version
& $wget https://raw.githubusercontent.com/ReactiveDomain/reactive-domain/master/src/build.props -P $tempDir

# Get the assembly and file version from build.props on the master branch
$masterProps = [xml] (get-content $masterbuildProps -Encoding UTF8)
$masterAssemblyVersionNode = $masterProps.SelectSingleNode("//Project/PropertyGroup/AssemblyVersion")

Write-Host ("Master branch Assembly Version is " + $masterAssemblyVersionNode.InnerText )

$masterMajor = $masterAssemblyVersionNode.InnerText.Split('.')[0]
$masterMinor = $masterAssemblyVersionNode.InnerText.Split('.')[1]
$masterBuild = $masterAssemblyVersionNode.InnerText.Split('.')[2]
$masterRevision = $masterAssemblyVersionNode.InnerText.Split('.')[3]


# Get the assemnbly and file version from build.props on current branch
$buildProps = $PSScriptRoot + "\..\src\build.props"
$props = [xml] (get-content $buildProps -Encoding UTF8)
$assemblyVersionNode = $props.SelectSingleNode("//Project/PropertyGroup/AssemblyVersion")
$fileVersionNode = $props.SelectSingleNode("//Project/PropertyGroup/FileVersion")

Write-Host ("Local Assembly Version node is " + $assemblyVersionNode.InnerText )

$major = $assemblyVersionNode.InnerText.Split('.')[0]
$minor = $assemblyVersionNode.InnerText.Split('.')[1]
$build = $assemblyVersionNode.InnerText.Split('.')[2]
$revision = $assemblyVersionNode.InnerText.Split('.')[3]


# If any of the version numbers are different from the master version then we are good and can exit successfully
if (($masterMajor -ne $major) -or ($masterMinor -ne $minor) -or ($masterBuild -ne $build) -or ($masterRevision -ne $revision))  
{
  Write-Host ("Assembly version already updated. Exiting script...")   
  Exit
}

# If version has not been updated log it and exit with resturn code 2. This will cause build to fail in Travis
# The write host stuff below will appear in the Travis console output
Write-Host ("*******************************************************************************************************************") 
Write-Host ("")
Write-Host ("")
Write-Host ("		Assembly version not updated! Assembly number in build.props must be incremented before merging into master!!!!") 
Write-Host ("")
Write-Host ("")
Write-Host ("*******************************************************************************************************************") 
Exit 2
