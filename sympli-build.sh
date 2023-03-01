#!/bin/bash
set -e
PROJECTS="./src/MassTransit.Abstractions"

project_list=$(echo $PROJECTS | tr "," "\n")

# remember where we are
root=$PWD
for p in $project_list
do
  dotnet restore --no-cache $p
  dotnet build $p --configuration Release
  dotnet pack $p -p:TargetFrameworks=netstandard2.1 --no-build --configuration Release --output $p/nuget;

  # publish
  cd $p/nuget
  dotnet nuget push "*.nupkg" --source $NUGET_REPO --api-key $NUGET_KEY
  cd $root
done
