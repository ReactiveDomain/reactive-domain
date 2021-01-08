REM Update the versions in ./src/build.props and here then run this
del .\nupkgs\*.*
dotnet build .\src\Reactive-domain.sln -c Restore
dotnet build .\src\Reactive-domain.sln -c Debug
powershell -Command "& {.\tools\CreateDebugNuget.ps1 -beta8}"

