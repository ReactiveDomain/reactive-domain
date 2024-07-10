del .\*.nukpg /q
del .\bld\Release\*.* /q
del .\bld\pub\*.* /q
dotnet restore .\src\ReactiveDomain.sln
dotnet build .\src\ReactiveDomain.sln -c Release
dotnet publish .\src\ReactiveDomain.PolicyTool\ReactiveDomain.PolicyTool.csproj -c Release -p:PublishProfile=FolderProfile
powershell -Command "& {.\tools\CreateNuget.ps1}"

