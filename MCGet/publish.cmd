dotnet publish -c Release -f net8.0 -r linux-x64 -t:CreateDeb,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true -p:CompletionPackage=true -p:CompletionDeb=true --version-suffix ""
dotnet publish -c Release -f net8.0 -r linux-x64 -t:CreateTarball,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true -p:CompletionTarball=true --version-suffix ""
dotnet publish -c Release -f net8.0 -r linux-x64 -t:CreateRpm,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true -p:CompletionPackage=true --version-suffix ""
dotnet publish -c Release -f net8.0 -r linux-arm -t:CreateDeb,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true -p:CompletionPackage=true -p:CompletionDeb=true --version-suffix ""
dotnet publish -c Release -f net8.0 -r linux-arm -t:CreateTarball,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true -p:CompletionTarball=true --version-suffix ""
dotnet publish -c Release -f net8.0 -r linux-arm -t:CreateRpm,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true -p:CompletionPackage=true --version-suffix ""
dotnet publish -c Release -f net8.0 -r linux-arm64 -t:CreateDeb,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true -p:CompletionPackage=true -p:CompletionDeb=true --version-suffix ""
dotnet publish -c Release -f net8.0 -r linux-arm64 -t:CreateTarball,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true -p:CompletionTarball=true --version-suffix ""
dotnet publish -c Release -f net8.0 -r linux-arm64 -t:CreateRpm,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true -p:CompletionPackage=true --version-suffix ""

dotnet publish -c Release -f net8.0 -r win-x86 -t:CreateZip,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true --version-suffix ""
dotnet publish -c Release -f net8.0 -r win-x64 -t:CreateZip,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true --version-suffix ""

dotnet publish -c Release -f net8.0 -r osx-x64 -t:CreateZip,Clean -p:PublishTrimmed=true -p:PublishSingleFile=true --version-suffix ""