del .\*.nukpg /q
del .\bld\Release\*.* /q
del .\bld\pub\*.* /q
del .\bld\tools\*.* /q
dotnet restore .\src\ReactiveDomain.sln
dotnet build .\src\ReactiveDomain.sln -c Release
dotnet publish .\src\ReactiveDomain.PolicyTool\ReactiveDomain.PolicyTool.csproj -c Release -p:PublishProfile=FolderProfile --framework net8.0
dotnet publish .\src\ReactiveDomain.PolicyTool\ReactiveDomain.PolicyTool.csproj -c Release -p:PublishProfile=FolderProfile --framework net10.0
pwsh.exe -Command "& {.\tools\CreateNuget.ps1}"

