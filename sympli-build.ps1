$ErrorActionPreference = "Stop"

dotnet restore --no-cache ./src/MassTransit.Abstractions
dotnet build ./src/MassTransit.Abstractions -f netstandard2.1 --configuration Release

dotnet restore --no-cache ./src/MassTransit
dotnet build ./src/MassTransit -f netstandard2.1 --configuration Release

dotnet restore --no-cache ./src/Transports/MassTransit.AmazonSqsTransport
dotnet build ./src/Transports/MassTransit.AmazonSqsTransport -f netstandard2.1 --configuration Release