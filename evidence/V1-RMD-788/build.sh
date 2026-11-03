#!/bin/sh
"C:/Users/semih/.dotnet/dotnet.exe" build D:/PROJECT/ALKAROS/src/Host/ALKAROS.Host.csproj > D:/PROJECT/ALKAROS/evidence/V1-RMD-788/build.log 2>&1
echo "exit=$?" >> D:/PROJECT/ALKAROS/evidence/V1-RMD-788/build.log
