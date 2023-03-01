#!/bin/bash
set -e

PROJECTS="./src/MassTransit.Abstractions,./src/MassTransit,./src/Transports/MassTransit.AmazonSqsTransport"

project_list=$(echo $PROJECTS | tr "," "\n")

for p in $project_list
do
  dotnet restore --no-cache $p
  dotnet build -p:Publish=enable $p -f netstandard2.1 --configuration Release
done
