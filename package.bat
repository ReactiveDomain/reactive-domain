REM Update the versions in ./src/build.props and here then run this
msbuild src\ReactiveDomain.sln /p:"PackageVersion=0.8.22" /t:"Restore;Build"

