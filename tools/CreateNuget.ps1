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

# This changes when its a CI build or a manually triggered via the web UI
# api --> means manual/stable build ;  push --> means CI/unstable build
# pull_request --> CI build triggered when opening a PR (do nothing here)
$buildType = $env:TRAVIS_EVENT_TYPE 

Write-Host ("*********************   Begin Create NUget script   **************************************")   

# create and push nuget off of master branch ONLY
if (($branch -ne $masterString) -and ($buildType -ne "debug"))  
{
  Write-Host ("Not a master branch. Will not create nuget")   
  Exit
}

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

$ReactiveDomainDll = $PSScriptRoot + "\..\bld\$configuration\netstandard2.0\ReactiveDomain.Core.dll"
$RDVersion = (Get-Item $ReactiveDomainDll).VersionInfo.FileVersion
$ReactiveDomainNuspec = $PSScriptRoot + "\..\src\ReactiveDomain" + $nuspecExtension
$ReactiveDomainTestingNuspec = $PSScriptRoot + "\..\src\ReactiveDomain.Testing" + $nuspecExtension
$ReactiveDomainUINuspec = $PSScriptRoot + "\..\src\ReactiveDomain.UI" + $nuspecExtension
$ReactiveDomainUITestingNuspec = $PSScriptRoot + "\..\src\ReactiveDomain.UI.Testing" + $nuspecExtension

$RDCoreProject = $PSScriptRoot + "\..\src\ReactiveDomain.Core\ReactiveDomain.Core.csproj"
$RDFoundationProject = $PSScriptRoot + "\..\src\ReactiveDomain.Foundation\ReactiveDomain.Foundation.csproj"
$RDIdentityProject = $PSScriptRoot + "\..\src\ReactiveDomain.Identity\ReactiveDomain.Identity.csproj"
$RDMessagingProject = $PSScriptRoot + "\..\src\ReactiveDomain.Messaging\ReactiveDomain.Messaging.csproj"
$RDPolicyProject = $PSScriptRoot + "\..\src\ReactiveDomain.Messaging\ReactiveDomain.Messaging.csproj"
$RDPrivateLedgerProject = $PSScriptRoot + "\..\src\ReactiveDomain.PrivateLedger\ReactiveDomain.PrivateLedger.csproj"
$RDPersistenceProject = $PSScriptRoot + "\..\src\ReactiveDomain.Policy\ReactiveDomain.Policy.csproj"
$RDTransportProject = $PSScriptRoot + "\..\src\ReactiveDomain.Transport\ReactiveDomain.Transport.csproj"
$RDUsersProject = $PSScriptRoot + "\..\src\ReactiveDomain.User\ReactiveDomain.Users.csproj"

$ReactiveDomainTestingProject = $PSScriptRoot + "\..\src\ReactiveDomain.Testing\ReactiveDomain.Testing.csproj"
$RDUIProject = $PSScriptRoot + "\..\src\ReactiveDomain.UI\ReactiveDomain.UI.csproj"
$RDUITestingProject = $PSScriptRoot + "\..\src\ReactiveDomain.UI.Testing\ReactiveDomain.UI.Testing.csproj"
$nuget = $PSScriptRoot + "\..\src\.nuget\nuget.exe"

Write-Host ("Reactive Domain version is " + $RDVersion)
Write-Host ("Build type is " + $buildType)
Write-Host ("ReactiveDomain nuspec file is " + $ReactiveDomainNuspec)
Write-Host ("ReactiveDomain.Testing nuspec file is " + $ReactiveDomainTestingNuspec)
Write-Host ("ReactiveDomain.UI nuspec file is " + $ReactiveDomainUINuspec)
Write-Host ("ReactiveDomain.UI.Testing nuspec file is " + $ReactiveDomainUITestingNuspec)
Write-Host ("Branch is file is " + $branch)

class PackagRef
{
    [string]$Version
    [string]$ComparisonOperator
    [string]$Framework
}

# GetPackageRefFromProject
#
#     Helper function to get a specific PackageRef from a .csproj file
#     Parses and returns a PackagRef object (defined above) that contains:
#         Version - (version of the package)
#         ConditionOperator - (the equality operator for a framework, == or !=)
#         Framework - The framework this Packageref applies to: (net452, net472, netstandard2.0)
#
function GetPackageRefFromProject([string]$Id, [string]$CsProj, [string]$Framework)
{
    [xml]$xml = Get-Content -Path $CsProj -Encoding UTF8

    $Xpath = "//Project/ItemGroup/PackageReference[@Include='" + $Id + "']"
    $targetPackage = $xml | Select-XML -XPath $Xpath
    $currentCondition = ""
    $compOperator = ""
    $currentFramework = ""
    $currentVersion = ""

    # There may be duplicates of the same package when there are different versions
    # for different frameworks (i.e. ReactiveUI). Therefore if our search
    # returns more than one node, then we take the one that matches 
    # the Framework in its Condition

    if ($targetPackage.Node.Count -gt 1)
    {
        foreach ($tn in $targetPackage.Node)
        {
            if ($tn.Condition -match $Framework )
            {
                $currentCondition = $tn.Condition
                $currentVersion = $tn.Version
            }
        }
    }
    else
    {
        $currentCondition = $targetPackage.Node.Condition
        $currentVersion = $targetPackage.Node.Version
    }

    if ($currentCondition -match "==")
    {
        $compOperator = "=="
    }

    if ($currentCondition -match "!=")
    {
        $compOperator = "!="
    }
       

    if ($currentCondition -match "net472")
    {
        $currentFramework = "net472"
    }

    if ($currentCondition -match "netstandard2.0")
    {
        $currentFramework = "netstandard2.0"
    }

    $myObj = New-Object -TypeName PackagRef 
    $myObj.Version = $currentVersion
    $myObj.ComparisonOperator = $compOperator 
    $myObj.Framework = $currentFramework
    
    return $myObj
}

# UpdateDependencyVersions
#
#    Helper function that updates all non-ReactiveDomain dependencies 
#    in a nuspec file. Loops through all dependencies listed in a 
#    nuspec file and gets the versions from its
#    entry in the corresponding .csproj file
#
function UpdateDependencyVersions([string]$Nuspec, [string]$CsProj)
{
    Write-Host "Updating dependency versions of: " $Nuspec

    [xml]$xml = Get-Content -Path $Nuspec -Encoding UTF8
    $dependencyNodes = $xml.package.metadata.dependencies.group.dependency
        
    $f472 = $xml | Select-XML -XPath "//package/metadata/dependencies/group[@targetFramework='net472']"
    $framework472Nodes = $f472.Node.ChildNodes

    $netstandard2 = $xml | Select-XML -XPath "//package/metadata/dependencies/group[@targetFramework='netstandard2.0']"
    $netstandard2Nodes = $netstandard2.Node.ChildNodes
       

    foreach($refnode in $framework472Nodes)
    {
        if ( $refnode.id -match "ReactiveDomain")
        {
            $refnode.version = $RDVersion
            continue
        }

        $pRef = GetPackageRefFromProject $refnode.id $CsProj "net472"
        if ((($pRef.ComparisonOperator -eq "" -or $pRef.Framework -eq "") -or 
            ($pRef.ComparisonOperator -eq "==" -and $pRef.Framework -eq "net472") -or 
            ($pRef.ComparisonOperator -eq "!=" -and $pRef.Framework -ne "net472")) -and
            ($pRef.version -ne ""))
        {
            $refnode.version = $pRef.Version
        }      
    }

    foreach($refnode in $netstandard2Nodes)
    {
        if ( $refnode.id -match "ReactiveDomain")
        {
            $refnode.version = $RDVersion
            continue
        }
        
        $pRef = GetPackageRefFromProject $refnode.id $CsProj "netstandard2.0"
        if ((($pRef.ComparisonOperator -eq "" -or $pRef.Framework -eq "") -or 
            ($pRef.ComparisonOperator -eq "==" -and $pRef.Framework -eq "netstandard2.0") -or 
            ($pRef.ComparisonOperator -eq "!=" -and $pRef.Framework -ne "netstandard2.0")) -and
            ($pRef.version -ne ""))
        { 
            $refnode.version = $pRef.Version
        }      
    }

    $xml.Save($Nuspec)
    Write-Host "Updated dependency versions of: $Nuspec"
}

# Make the nuget.exe update itself (We must be at least at nuget 5.0 for this all to work) **************
Write-Host "Update nuget.exe"
& $nuget update -self

# Update the dependency versions in the nuspec files ****************************************************

# These all go into updating the main ReactiveDomain.nuspec
UpdateDependencyVersions $ReactiveDomainNuspec $RDCoreProject  
UpdateDependencyVersions $ReactiveDomainNuspec $RDFoundationProject  
UpdateDependencyVersions $ReactiveDomainNuspec $RDIdentityProject  
UpdateDependencyVersions $ReactiveDomainNuspec $RDMessagingProject  
UpdateDependencyVersions $ReactiveDomainNuspec $RDPersistenceProject 
UpdateDependencyVersions $ReactiveDomainNuspec $RDPolicyProject 
UpdateDependencyVersions $ReactiveDomainNuspec $RDPrivateLedgerProject 
UpdateDependencyVersions $ReactiveDomainNuspec $RDTransportProject 
UpdateDependencyVersions $ReactiveDomainNuspec $RDUsersProject

# These go into updating the ReactiveDomainUI.nuspec
UpdateDependencyVersions $ReactiveDomainUINuspec $RDUIProject 

# These go into updating the ReactiveDomainTesting.nuspec
UpdateDependencyVersions $ReactiveDomainTestingNuspec $ReactiveDomainTestingProject 

# These go into updating the ReactiveDomain.UI.Testing.nuspec
UpdateDependencyVersions $ReactiveDomainUITestingNuspec $RDUITestingProject 

# *******************************************************************************************************

# Pack the nuspec files to create the .nupkg files using the set versionString  *************************
Write-Host "Packing reactivedomain nuget packages"
$versionString = $RDVersion
& $nuget pack $ReactiveDomainNuspec -Version $versionString
& $nuget pack $ReactiveDomainTestingNuspec -Version $versionString
& $nuget pack $ReactiveDomainUINuspec -Version $versionString
& $nuget pack $ReactiveDomainUITestingNuspec -Version $versionString

# *******************************************************************************************************************************

# Push the nuget packages to nuget.org ******************************************************************************************
Write-Host "Push nuget packages to nuget.org"
$ReactiveDomainNupkg = $PSScriptRoot + "\..\ReactiveDomain." + $versionString + ".nupkg"
$ReactiveDomainTestingNupkg = $PSScriptRoot + "\..\ReactiveDomain.Testing." + $versionString + ".nupkg"
$ReactiveDomainUINupkg = $PSScriptRoot + "\..\ReactiveDomain.UI." + $versionString + ".nupkg"
$ReactiveDomainUITestingNupkg = $PSScriptRoot + "\..\ReactiveDomain.UI.Testing." + $versionString + ".nupkg"

& $nuget push $ReactiveDomainNupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 
& $nuget push $ReactiveDomainTestingNupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 
& $nuget push $ReactiveDomainUINupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 
& $nuget push $ReactiveDomainUITestingNupkg -Source "https://api.nuget.org/v3/index.json" -ApiKey $apikey 

# *******************************************************************************************************************************