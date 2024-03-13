param (
    [switch]$publish = $false
)

$ErrorActionPreference = "Stop"

$sso_profile = 'ticklestaging'
$aws_region = 'ap-southeast-2'
$version = '8.2.0'
$build_number = '0'
$nuget_dir = [IO.Path]::Combine($env:userprofile, "nuget_local")
$prelease_tag = '-rc'

$nuget_repo = aws ssm get-parameter --name "/DevOps/InternalNugetSource" --region $aws_region --output text --query Parameter.Value --profile $sso_profile
$nuget_key = aws ssm get-parameter --name "/DevOps/NugetAPIKey" --with-decryption --region $aws_region --output text --query Parameter.Value --profile $sso_profile

# project order matters, due to the dependencies
$projects = "./src/MassTransit.Abstractions,./src/MassTransit,./src/Transports/MassTransit.AmazonSqsTransport"
$project_list = $projects.split(",");

foreach ($p in $project_list) {
    dotnet restore -p:TargetFrameworks=netstandard2.1 --no-cache $p
    dotnet build $p -p:TargetFrameworks=netstandard2.1 -p:SympliVersion=$version -p:BUILD_NUMBER=$build_number -p:PreleaseTag=$prelease_tag --configuration Release
    dotnet pack $p -p:TargetFrameworks=netstandard2.1 --no-build -p:SympliVersion=$version -p:BUILD_NUMBER=$build_number -p:PreleaseTag=$prelease_tag --configuration Release --output $nuget_dir
}

if ($publish) {
    if (([string]::IsNullOrEmpty($prelease_tag)) -or ($prelease_tag -eq "-sympli")) {
        Write-Output "Publish is not allowed. Invalid prelease_tag: $prelease_tag"
        Break
    }
    Write-Output "Publishing local packages..."
    $packages = "MassTransit.Abstractions, MassTransit, MassTransit.AmazonSQS"
    foreach ($package_id in $packages.split(", ")) {
        $package_path = [IO.Path]::Combine($nuget_dir, "$package_id.$version.$build_number$prelease_tag.nupkg")
        dotnet nuget push $package_path --source $nuget_repo --api-key $nuget_key
    }
}
