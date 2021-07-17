del .\nupkgs\*.* /q
dotnet restore .\src\ReactiveDomain.sln
dotnet build .\src\ReactiveDomain.sln -c Debug
powershell -Command "& {.\tools\CreateDebugNuget.ps1 -beta179}"

