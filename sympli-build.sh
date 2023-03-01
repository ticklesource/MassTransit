#!/bin/bash
set -e
PROJECTS="./src/MassTransit.Abstractions,./src/MassTransit,./src/Transports/MassTransit.AmazonSqsTransport"

project_list=$(echo $PROJECTS | tr "," "\n")

# remember where we are
root=$PWD
for p in $project_list
do
  dotnet restore --no-cache $p
  dotnet build $p --configuration Release

  # publish
  cd $p/nuget
  dotnet nuget push "*.nupkg" --source $NUGET_REPO --api-key $NUGET_KEY
  cd $root
done
