del .\nupkgs\*.* /q
del .\bld\Debug\*.* /q
del .\bld\pub\*.* /q
dotnet restore .\src\ReactiveDomain.sln
dotnet build .\src\ReactiveDomain.sln -c Debug
dotnet publish .\src\ReactiveDomain.PolicyTool\ReactiveDomain.PolicyTool.csproj -p:PublishProfile=FolderProfile
powershell -Command "& {.\tools\CreateDebugNuget.ps1 -rc009}"

