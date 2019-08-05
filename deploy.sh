#!/bin/bash

dotnet nuget push ./src/../packages/ReactiveDomain.Core.1.0.0.nupkg -k $PkiApiKey -s "https://api.nuget.org/v3/index.json"