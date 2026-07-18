REM dotnet tool install --global dotnet-reportgenerator-globaltool

if exist coverage rmdir /s /q coverage
dotnet test --settings coverlet.runsettings --results-directory ./coverage
reportgenerator -reports:"coverage/**/coverage.opencover.xml" -targetdir:"coverage/report" -reporttypes:Html
start coverage/report/index.html