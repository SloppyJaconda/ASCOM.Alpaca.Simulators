dotnet publish -c Release -r win-x64 --self-contained true /p:PublishTrimmed=true /p:PublishReadyToRun=true /p:PublishReadyToRunShowWarnings=true -o ./bin/ASCOM.Alpaca.Simulators.windows-x64
dotnet publish -c Release -r win-x86 --self-contained true /p:PublishTrimmed=true /p:PublishReadyToRun=true /p:PublishReadyToRunShowWarnings=true -o ./bin/ASCOM.Alpaca.Simulators.windows-x86