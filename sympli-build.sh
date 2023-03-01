#!/bin/bash
set -e
PROJECTS="./src/MassTransit.Abstractions,./src/MassTransit,./src/Transports/MassTransit.AmazonSqsTransport"

project_list=$(echo $PROJECTS | tr "," "\n")

# remember where we are
root=$PWD
for p in $project_list
do
  dotnet restore --no-cache $p
  dotnet build $p -p:BUILD_NUMBER=$BUILD_NUMBER --configuration Release
  dotnet pack $p -p:BUILD_NUMBER=$BUILD_NUMBER -p:TargetFrameworks=netstandard2.1 --no-build --configuration Release --output $p/nuget;

  # publish
  cd $p/nuget
  dotnet nuget push "*.nupkg" --source $NUGET_REPO --api-key $NUGET_KEY
  cd $root
done
