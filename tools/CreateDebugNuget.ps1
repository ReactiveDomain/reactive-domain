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

$buildSuffix = ""
$TempNum = Get-Random -Minimum 1000 -Maximum 10000
if ($args[0] -eq $null )
{	
	$buildSuffix = "-local" + $TempNum.ToString()
}
else
{
	$buildSuffix = $args[0]
}
Write-Host ("*********************   Begin Create Nuget script   **************************************")  

Write-Host ("Copy ReactiveDomain build folder and nuspec files to a temp directory")


$TempDir = Join-Path $env:temp $TempNum.ToString()
$buildDir = Join-Path $ReactiveDomainRepo "bld"
$propsDir = Join-Path $ReactiveDomainRepo "build"
$sourceDir = Join-Path $ReactiveDomainRepo "src"
$tempBuildDir = Join-Path $TempDir "bld"
$tempPropsDir = Join-Path $TempDir "build"
$tempSourceDir = Join-Path $TempDir "src"
New-Item -ItemType "directory" -Path $tempSourceDir
Copy-Item -Path $buildDir -Destination $tempBuildDir -Recurse
Copy-Item -Path $propsDir -Destination $tempPropsDir -Recurse

#source nuspec file paths
$sourceRDNuspec = Join-Path $sourceDir "ReactiveDomain.Debug.nuspec"
$sourceRDPolicyNuspec = Join-Path $sourceDir "ReactiveDomain.Policy.Debug.nuspec"
$sourceRDPolicyTargets = Join-Path $sourceDir "ReactiveDomain.Policy.targets"
$sourceRDTestNuspec = Join-Path $sourceDir "ReactiveDomain.Testing.Debug.nuspec"


#target nuspec file paths in temp dir
$ReactiveDomainNuspec = Join-Path $tempSourceDir "ReactiveDomain.Debug.nuspec"
$ReactiveDomainPolicyNuspec = Join-Path $tempSourceDir "ReactiveDomain.Policy.Debug.nuspec"
$ReactiveDomainPolicyTargets = Join-Path $tempSourceDir "ReactiveDomain.Policy.targets"
$ReactiveDomainTestingNuspec = Join-Path $tempSourceDir "ReactiveDomain.Testing.Debug.nuspec"


#copy nuspec files to temp
Copy-Item $sourceRDNuspec -Destination $ReactiveDomainNuspec
Copy-Item $sourceRDPolicyNuspec -Destination $ReactiveDomainPolicyNuspec
Copy-Item $sourceRDPolicyTargets -Destination $ReactiveDomainPolicyTargets
Copy-Item $sourceRDTestNuspec -Destination $ReactiveDomainTestingNuspec

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

$RDVersion = $major + "." + $minor + "." + $build + "." + $revision + $buildSuffix

Write-Host "Debug ReactiveDomain nuget version is: " $RDVersion

#list of target projects
$RDFoundationProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Foundation\ReactiveDomain.Foundation.csproj"
$RDMessagingProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Messaging\ReactiveDomain.Messaging.csproj"
$RDPersistenceProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Persistence\ReactiveDomain.Persistence.csproj"
$RDTransportProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Transport\ReactiveDomain.Transport.csproj"
$ReactiveDomainTestingProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Testing\ReactiveDomain.Testing.csproj"
$RDPolicyProject = $ReactiveDomainRepo + "\src\ReactiveDomain.Policy\ReactiveDomain.Policy.csproj"
$RDPolicyStorageProject = $ReactiveDomainRepo + "\src\ReactiveDomain.PolicyStorage\ReactiveDomain.PolicyStorage.csproj"
$RDIdentityStorageProject = $ReactiveDomainRepo + "\src\ReactiveDomain.IdentityStorage\ReactiveDomain.IdentityStorage.csproj"

$nuget = $ReactiveDomainRepo + "\src\.nuget\nuget.exe"

Write-Host ("Reactive Domain version is " + $RDVersion)
Write-Host ("Build type is " + $buildType)
Write-Host ("ReactiveDomain nuspec file is " + $ReactiveDomainNuspec)
Write-Host ("ReactiveDomain.Policy nuspec file is " + $ReactiveDomainPolicyNuspec)
Write-Host ("ReactiveDomain.Testing nuspec file is " + $ReactiveDomainTestingNuspec)
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
      
    if ($currentCondition -match "net8.0")
    {
        $currentFramework = "net8.0"
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
    Write-Host ("Updating dependency versions from " + $CsProj) 

    [xml]$xml = Get-Content -Path $Nuspec -Encoding UTF8
    $dependencyNodes = $xml.package.metadata.dependencies.group.dependency
    
    $net8 = $xml | Select-XML -XPath "//package/metadata/dependencies/group[@targetFramework='net8.0']"
    $net8Nodes = $net8.Node.ChildNodes
       
    foreach($refnode in $net8Nodes)
    {
        if ( $refnode.id -match "ReactiveDomain")
        {
            $refnode.version = $RDVersion
            continue
        }

        $pRef = GetPackageRefFromProject $refnode.id $CsProj "net8.0"
        if ((($pRef.ComparisonOperator -eq "" -or $pRef.Framework -eq "") -or 
            ($pRef.ComparisonOperator -eq "==" -and $pRef.Framework -eq "net8.0") -or 
            ($pRef.ComparisonOperator -eq "!=" -and $pRef.Framework -ne "net8.0")) -and
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

# These all go into updating the main ReactiveDomain.Policy.nuspec 
UpdateDependencyVersions $ReactiveDomainPolicyNuspec $RDPolicyProject 
UpdateDependencyVersions $ReactiveDomainPolicyNuspec $RDPolicyStorageProject 
UpdateDependencyVersions $ReactiveDomainPolicyNuspec $RDIdentityStorageProject 

# These go into updating the ReactiveDomainTesting.nuspec
UpdateDependencyVersions $ReactiveDomainTestingNuspec $ReactiveDomainTestingProject 

# *******************************************************************************************************

# Pack the nuspec files to create the .nupkg files using the set versionString  *************************

Write-Host "Packing reactivedomain nuget packages"
Write-Host "Version string to use: " + $RDVersion
Write-Host "RD-Vuspec: " + $ReactiveDomainNuspec

& $nuget pack $ReactiveDomainNuspec -version $RDVersion -OutputDirectory $nupkgsDir
& $nuget pack $ReactiveDomainPolicyNuspec -version $RDVersion -OutputDirectory $nupkgsDir
& $nuget pack $ReactiveDomainTestingNuspec -version $RDVersion -OutputDirectory $nupkgsDir

# *******************************************************************************************************************************

# Cleanup the temp directory
Remove-Item $TempDir -Recurse 