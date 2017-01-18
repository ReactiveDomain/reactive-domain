
$currentScriptDirectory = Get-Location
[System.IO.Directory]::SetCurrentDirectory($currentScriptDirectory.Path)

Set-Location -Path ${PSScriptRoot}
$solutiondir = Resolve-Path -Path "$currentScriptDirectory\src\ReactiveDomain.Tests"

Write-Host "Solution dir is " $solutiondir

$path = "$solutiondir\ReactiveDomain.Tests\ReactiveDomain.Tests.nuspec"
Write-Host "NuSpec File is" $path

$dll = "$solutiondir\ReactiveDomain\bin\x64\Debug\ReactiveDomain.Tests.dll" 
Write-Host "Loading dll: " $dll

$Assembly = [Reflection.Assembly]::Loadfile($dll)
$AssemblyName = $Assembly.GetName()
$Assemblyversion = $AssemblyName.version.ToString()

Write-Host "Assembly version is" $Assemblyversion

#Modify the nuspec file to get the assembly version of this build
$xml = [xml](Get-Content $path)
$node = $xml.package.metadata
$node.version = $Assemblyversion
$xml.Save($path)








