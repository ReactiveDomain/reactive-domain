 # CheckAssemblyVersion.ps1
#
# This script will check the local build.props file to get current assembly version 
# It will then compare to the latest version of the reactivedomain nuget
# as listed on nuget.org
#

$masterString = "master"
$branch = $env:TRAVIS_BRANCH
$buildType = $env:TRAVIS_EVENT_TYPE 

# create and push nuget off of master branch ONLY
if ($branch -ne $masterString)  
{
  Write-Host ("Not a master branch. Will not verify updated assembly version")   
  Exit
}

if ($buildType -ne "api")  
{
  Write-Host ("Not a manual travis api build. Will not verify updated assembly version")   
  Exit
}

Write-Host ("Check Assembly script location is " + $PSScriptRoot)
$nuget = $PSScriptRoot + "\..\src\.nuget\nuget.exe"

# Get the assembly and file version from build.props on current branch 
$buildProps = $PSScriptRoot + "\..\src\build.props" 
$props = [xml] (get-content $buildProps -Encoding UTF8) 
$RDVersion = $props.SelectSingleNode("//Project/PropertyGroup/AssemblyVersion") 

Write-Host ("Local Assembly Version node is " + $RDVersion.InnerText ) 

$major = [int]$RDVersion.InnerText.Split('.')[0]
$minor = [int]$RDVersion.InnerText.Split('.')[1]
$build = [int]$RDVersion.InnerText.Split('.')[2]
$revision = [int]$RDVersion.InnerText.Split('.')[3]

$rdPackages = &$nuget list packageid:ReactiveDomain -source https://api.nuget.org/v3/index.json
# 'nuget list' is deprecated and prints a warning line ahead of the results, so the raw output is
# multi-line. Select the exact 'ReactiveDomain <version>' entry — not the warning, and not the
# ReactiveDomain.Testing/.Policy rows — then take the version token.
$masterLine = $rdPackages | Where-Object { $_ -match '^ReactiveDomain\s+\d' } | Select-Object -First 1
$masterRDversion = ($masterLine -split '\s+')[1]
Write-Host "Latest ReactiveDomain nuget version is: " $masterRDversion

$masterMajor = [int]$masterRDversion.Split('.')[0]
$masterMinor = [int]$masterRDversion.Split('.')[1]
$masterBuild = [int]$masterRDversion.Split('.')[2]
$masterRevision = [int]$masterRDversion.Split('.')[3]

# Verify the assembly has been incremented from the assembly version on master branch
if ($masterMajor -gt $major)
{
    Write-Host ("*******************************************************************************************************************") 
    Write-Host ("")
    Write-Host ("")
    Write-Host ("		Invalid Assembly Version!!! Master version of assembly is less than version on master branch!!!") 
    Write-Host ("")
    Write-Host ("")
    Write-Host ("*******************************************************************************************************************")
    Exit 2
}

if (($masterMajor -eq $major) -and ($masterMinor -gt $minor))
{
    Write-Host ("*******************************************************************************************************************") 
    Write-Host ("")
    Write-Host ("")
    Write-Host ("		Invalid Assembly Version!!! Minor version of assembly is less than version on master branch!!!") 
    Write-Host ("")
    Write-Host ("")
    Write-Host ("*******************************************************************************************************************")
    Exit 2 
}

if (($masterMajor -eq $major) -and ($masterMinor -eq $minor) -and ($masterBuild -gt $build))
{
    Write-Host ("*******************************************************************************************************************") 
    Write-Host ("")
    Write-Host ("")
    Write-Host ("		Invalid Assembly Version!!! Build version of assembly is less than version on master branch!!!") 
    Write-Host ("")
    Write-Host ("")
    Write-Host ("*******************************************************************************************************************")
    Exit 2 
}  
    
if (($masterMajor -eq $major) -and ($masterMinor -eq $minor) -and ($masterBuild -eq $build) -and ($masterRevision -gt $revision))
{
    Write-Host ("*******************************************************************************************************************") 
    Write-Host ("")
    Write-Host ("")
    Write-Host ("		Invalid Assembly Version!!! Revision version of assembly is less than version on master branch!!!") 
    Write-Host ("")
    Write-Host ("")
    Write-Host ("*******************************************************************************************************************")
    Exit 2 
}    
    
if (($masterMajor -eq $major) -and ($masterMinor -eq $minor) -and ($masterBuild -eq $build) -and ($masterRevision -eq $revision))
{
    Write-Host ("*******************************************************************************************************************") 
    Write-Host ("")
    Write-Host ("")
    Write-Host ("		Assembly version not updated! Assembly number in build.props must be incremented before merging into master!!!!") 
    Write-Host ("")
    Write-Host ("")
    Write-Host ("*******************************************************************************************************************")
    Exit 2 
}  

Write-Host ("Assembly version updated correctly. Build will proceed")   
Exit

