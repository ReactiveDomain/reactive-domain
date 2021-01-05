REM Update the versions in ./src/build.props and here then run this
del .\nupkgs\*.*
dotnet pack .\src\ReactiveDomain.sln -o nupkgs -p:PackageVersion=0.8.23-beta-5 -c Debug --include-symbols

