del .\nupkgs\*.* /q
dotnet build .\src\Reactive-domain.sln -c Restore
dotnet build .\src\Reactive-domain.sln -c Debug
powershell -Command "& {.\tools\CreateDebugNuget.ps1 -beta52}"

