# CreateDebugNuget.ps1
#
# This script should be run from the local reactive-domain repo
# To create local debug reactive-domain nugets
# reactive-domain debug build should be done before running this script
# It will copy the debug nuspec files and the bld directory from the reactive-doain repo
# to a temp directory. From There it will pack the nugets 
# The resulting .nupkg files will be in the nupkgs dir 

# args[0]: - the desired build suffix i.e. '-beta5'
#           Note: if no suffix is specified the suffix will be '-local{tempnumber}'

$configuration = "Debug"
$nuspecExtension = ".nuspec"
$masterString = "master"
$ReactiveDomainRepo = ".\"
$nupkgsDir = ".\nupkgs\"

Write-Host ("*********************   Begin Create Nuget script   **************************************")  

Write-Host ("Copy ReactiveDomain build folder and nuspec files to a temp directory")


$TempDir = Join-Path $env:temp $TempNum.ToString()
$buildDir = Join-Path $ReactiveDomainRepo "bld"
$sourceDir = Join-Path $ReactiveDomainRepo "src"
$tempBuildDir = Join-Path $TempDir "bld"
$tempSourceDir = Join-Path $TempDir "src"
New-Item -ItemType "directory" -Path $tempSourceDir
Copy-Item -Path $buildDir -Destination $tempBuildDir -Recurse

#source nuspec file paths
$sourceRDNuspec = Join-Path $sourceDir "ReactiveDomain.Debug.nuspec"
$sourceRDTestNuspec = Join-Path $sourceDir "ReactiveDomain.Testing.Debug.nuspec"
$sourceRDUINuspec = Join-Path $sourceDir "ReactiveDomain.UI.Debug.nuspec"
$sourceRDUITestNuspec = Join-Path $sourceDir "ReactiveDomain.UI.Testing.Debug.nuspec"
$sourceRDIdentityTestNuspec = Join-Path $sourceDir "ReactiveDomain.IdentityStorage.Debug.nuspec"
$sourceRDPolicyTestNuspec = Join-Path $sourceDir "ReactiveDomain.Policy.Debug.nuspec

#target nuspec file paths in temp dir
$ReactiveDomainNuspec = Join-Path $tempSourceDir "ReactiveDomain.Debug.nuspec"
$ReactiveDomainTestingNuspec = Join-Path $tempSourceDir "ReactiveDomain.Testing.Debug.nuspec"
$ReactiveDomainUINuspec = Join-Path $tempSourceDir "ReactiveDomain.UI.Debug.nuspec"
$ReactiveDomainUITestingNuspec = Join-Path $tempSourceDir "ReactiveDomain.UI.Testing.Debug.nuspec"
$ReactiveDomainIdentityNuspec = Join-Path $tempSourceDir "ReactiveDomain.IdentityStorage.Debug.nuspec"
$ReactiveDomainPolicyNuspec = Join-Path $tempSourceDir "ReactiveDomain.Policy.Debug.nuspec"
#$ReactiveDomainUsersNuspec = Join-Path $tempSourceDir "ReactiveDomain.Users.Debug.nuspec"


#copy nuspec files to temp
Copy-Item $sourceRDNuspec -Destination $ReactiveDomainNuspec
Copy-Item $sourceRDTestNuspec -Destination $ReactiveDomainTestingNuspec
Copy-Item $sourceRDUINuspec -Destination $ReactiveDomainUINuspec
Copy-Item $sourceRDUITestNuspec -Destination $ReactiveDomainUITestingNuspec
Copy-Item $sourceRDIdentityTestNuspec -Destination $ReactiveDomainIdentityNuspec
Copy-Item $sourceRDPolicyTestNuspec -Destination $ReactiveDomainPolicyNuspec
#Copy-Item $sourceRDUsersTestNuspec -Destination $ReactiveDomainUsersNuspec

Write-Host ("Powershell script location is " + $PSScriptRoot)

# Get the assembly and file version from build.props on current branch 
# N.B. this is sourced from build.props <AssemblyVersion/> not the projects or other files
$buildProps = $ReactiveDomainRepo + "\src\build.props" 
$props = [xml] (get-content $buildProps -Encoding UTF8) 
$localRDVersion = $props.SelectSingleNode("//Project/PropertyGroup/AssemblyVersion") 

Write-Host ("Local Assembly Version node is " + $localRDVersion.InnerText ) 

$major = $localRDVersion.InnerText.Split('.')[0]
$minor = $localRDVersion.InnerText.Split('.')[1]
$build = $localRDVersion.InnerText.Split('.')[2]
$revision = $localRDVersion.InnerText.Split('.')[3]

# Use random numbers for build revs
$RandomMajor = Get-Random -Minimum 100 -Maximum 1000
$RandomMinor = Get-Random -Minimum 100 -Maximum 1000
$RandomBuild = Get-Random -Minimum 100 -Maximum 1000
$RandomRevision = Get-Random -Minimum 100 -Maximum 1000

$RDVersion = $RandomMajor.ToString() + "." + $RandomMinor.ToString() + "." + $RandomBuild.ToString() + "." + $RandomRevision.ToString()
Write-Host "Debug ReactiveDomain nuget version is: " $RDVersion

#list of target projects
$RDFoundationProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Foundation\ReactiveDomain.Foundation.csproj"
$RDMessagingProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Messaging\ReactiveDomain.Messaging.csproj"
$RDPersistenceProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Persistence\ReactiveDomain.Persistence.csproj"
$RDTransportProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Transport\ReactiveDomain.Transport.csproj"
$ReactiveDomainTestingProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Testing\ReactiveDomain.Testing.csproj"
$RDUIProject = $ReactiveDomainRepo + "\src\ReactiveDomain.UI\ReactiveDomain.UI.csproj"
$RDUITestingProject = $ReactiveDomainRepo + "\src\ReactiveDomain.UI.Testing\ReactiveDomain.UI.Testing.csproj"
$RDUsersProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Users\ReactiveDomain.Users.csproj"
$RDPolicyProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Policy\ReactiveDomain.Policy.csproj"
$RDIdentityProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Identity\ReactiveDomain.Identity.csproj"

$nuget = $ReactiveDomainRepo + "\src\.nuget\nuget.exe"

Write-Host ("Reactive Domain version is " + $RDVersion)
Write-Host ("Build type is " + $buildType)
Write-Host ("ReactiveDomain nuspec file is " + $ReactiveDomainNuspec)
Write-Host ("ReactiveDomain.Testing nuspec file is " + $ReactiveDomainTestingNuspec)
Write-Host ("ReactiveDomain.UI nuspec file is " + $ReactiveDomainUINuspec)
Write-Host ("ReactiveDomain.UI.Testing nuspec file is " + $ReactiveDomainUITestingNuspec)
Write-Host ("ReactiveDomain.Identity.Testing nuspec file is " + $ReactiveDomainIdentityNuspec)
Write-Host ("ReactiveDomain.Policy.Testing nuspec file is " + $sourceRDPolicyTestNuspec)
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

    
    if ($currentCondition -match "net48")
    {
        $currentFramework = "net48"
    }

    if ($currentCondition -match "netstandard2.1")
    {
        $currentFramework = "netstandard2.1"
    }

     if ($currentCondition -match "netcoreapp3.1")
    {
        $currentFramework = "netcoreapp3.1"
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

    #framework 48 processing
    $f48 = $xml | Select-XML -XPath "//package/metadata/dependencies/group[@targetFramework='.NETFramework4.8.0']"
    $framework48Nodes = $f48.Node.ChildNodes
    
    foreach($refnode in $framework48Nodes)
    {
        if ( $refnode.id -match "ReactiveDomain")
        {
            $refnode.version = $RDVersion
            continue
        }

        $pRef = GetPackageRefFromProject $refnode.id $CsProj "net48"
        if ((($pRef.ComparisonOperator -eq "" -or $pRef.Framework -eq "") -or 
            ($pRef.ComparisonOperator -eq "==" -and $pRef.Framework -eq "net48") -or 
            ($pRef.ComparisonOperator -eq "!=" -and $pRef.Framework -ne "net48")) -and
            ($pRef.version -ne ""))
        {
            $refnode.version = $pRef.Version
        }      
    }
    
    #netstandard2.1 processing
    $netstandard21 = $xml | Select-XML -XPath "//package/metadata/dependencies/group[@targetFramework='.NETStandard2.1']"
    $netstandard21Nodes = $netstandard21.Node.ChildNodes
    
    foreach($refnode in $netstandard21Nodes)
    {
        if ( $refnode.id -match "ReactiveDomain")
        {
            $refnode.version = $RDVersion
            continue
        }
        
        $pRef = GetPackageRefFromProject $refnode.id $CsProj "netstandard2.1"
        if ((($pRef.ComparisonOperator -eq "" -or $pRef.Framework -eq "") -or 
            ($pRef.ComparisonOperator -eq "==" -and $pRef.Framework -eq "netstandard2.1") -or 
            ($pRef.ComparisonOperator -eq "!=" -and $pRef.Framework -ne "netstandard2.1")) -and
            ($pRef.version -ne ""))
        { 
            $refnode.version = $pRef.Version
        }      
    }
    #netcoreapp3.1 processing
    $netcore31 = $xml | Select-XML -XPath "//package/metadata/dependencies/group[@targetFramework='netcoreapp3.1']"
    $netcore31Nodes = $netcore31.Node.ChildNodes
    
    foreach($refnode in $netcore31Nodes)
    {
        if ( $refnode.id -match "ReactiveDomain")
        {
            $refnode.version = $RDVersion
            continue
        }
        
        $pRef = GetPackageRefFromProject $refnode.id $CsProj "netcoreapp3.1"
        if ((($pRef.ComparisonOperator -eq "" -or $pRef.Framework -eq "") -or 
            ($pRef.ComparisonOperator -eq "==" -and $pRef.Framework -eq "netcoreapp3.1") -or 
            ($pRef.ComparisonOperator -eq "!=" -and $pRef.Framework -ne "netcoreapp3.1")) -and
            ($pRef.version -ne ""))
        { 
            $refnode.version = $pRef.Version
        }      
    }
    $xml.Save($Nuspec)
    Write-Host "Updated dependency versions of: $Nuspec"
}


# Update the dependency versions in the nuspec files ****************************************************
# N.B. this *overwrites* the dependencies listed in the NuSpec files to make the current with the project
# any *new* dependecies the project require will also need to added to the NuSpec files or this will skip them

# These all go into updating the main ReactiveDomain.nuspec
UpdateDependencyVersions $ReactiveDomainNuspec $RDFoundationProject  
UpdateDependencyVersions $ReactiveDomainNuspec $RDMessagingProject  
UpdateDependencyVersions $ReactiveDomainNuspec $RDPersistenceProject 
UpdateDependencyVersions $ReactiveDomainNuspec $RDTransportProject 
UpdateDependencyVersions $ReactiveDomainNuspec $RDUsersProject 

# These go into updating the ReactiveDomainUI.nuspec
UpdateDependencyVersions $ReactiveDomainUINuspec $RDUIProject 

# These go into updating the ReactiveDomainTesting.nuspec
UpdateDependencyVersions $ReactiveDomainTestingNuspec $ReactiveDomainTestingProject 

# These go into updating the ReactiveDomain.UI.Testing.nuspec
UpdateDependencyVersions $ReactiveDomainUITestingNuspec $RDUITestingProject 

# These go into updating the ReactiveDomain.IdentityStorage.nuspec
UpdateDependencyVersions $ReactiveDomainIdentityNuspec $RDIdentityProject 

# These go into updating the ReactiveDomain.Policy.nuspec
UpdateDependencyVersions $ReactiveDomainPolicyNuspec $RDPolicyProject 

# *******************************************************************************************************

# Pack the nuspec files to create the .nupkg files using the set versionString  *************************

Write-Host "Packing reactivedomain nuget packages"
Write-Host "Version string to use: " + $versionString
$versionString = $RDVersion

& $nuget pack $ReactiveDomainNuspec -Version $versionString -OutputDirectory $nupkgsDir
& $nuget pack $ReactiveDomainTestingNuspec -Version $versionString -OutputDirectory $nupkgsDir
& $nuget pack $ReactiveDomainUINuspec -Version $versionString -OutputDirectory $nupkgsDir
& $nuget pack $ReactiveDomainUITestingNuspec -Version $versionString -OutputDirectory $nupkgsDir
& $nuget pack $ReactiveDomainIdentityNuspec -Version $versionString -OutputDirectory $nupkgsDir
& $nuget pack $ReactiveDomainPolicyNuspec -Version $versionString -OutputDirectory $nupkgsDir

# *******************************************************************************************************************************

# Cleanup the temp directory
Remove-Item $TempDir -Recurse 