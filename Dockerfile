# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0-azurelinux3.0 AS build-env
WORKDIR /App

# Make sure we run bash
CMD ["bash"]

# Install all required build tools in a single layer (rarely changes — stays cached)
# This is Node v16. For 18, use nodejs18.
RUN --mount=type=cache,target=/var/cache/tdnf,sharing=locked \
    tdnf install -y gawk nodejs npm

# Copy only the files needed to restore dependencies.
# These layers are cached until a manifest file changes, so routine source edits
# don't re-run the expensive restore steps below.
COPY NuGet.config global.json Directory.Build.props Directory.Build.targets Directory.Packages.props TryDotNet.sln ./
COPY eng/ ./eng/

# .csproj files — one COPY per project to preserve directory structure
COPY src/Microsoft.TryDotNet/Microsoft.TryDotNet.csproj src/Microsoft.TryDotNet/
COPY src/Microsoft.TryDotNet.FileIntegration.Tests/Microsoft.TryDotNet.FileIntegration.Tests.csproj src/Microsoft.TryDotNet.FileIntegration.Tests/
COPY src/Microsoft.TryDotNet.IntegrationTests/Microsoft.TryDotNet.IntegrationTests.csproj src/Microsoft.TryDotNet.IntegrationTests/
COPY src/Microsoft.TryDotNet.SimulatorGenerator/Microsoft.TryDotNet.SimulatorGenerator.csproj src/Microsoft.TryDotNet.SimulatorGenerator/
COPY src/Microsoft.TryDotNet.Tests/Microsoft.TryDotNet.Tests.csproj src/Microsoft.TryDotNet.Tests/
COPY src/Microsoft.TryDotNet.WasmRunner/Microsoft.TryDotNet.WasmRunner.csproj src/Microsoft.TryDotNet.WasmRunner/

# npm manifests
COPY src/microsoft-trydotnet/package.json src/microsoft-trydotnet/package-lock.json src/microsoft-trydotnet/
COPY src/microsoft-trydotnet-editor/package.json src/microsoft-trydotnet-editor/package-lock.json src/microsoft-trydotnet-editor/
COPY src/microsoft-trydotnet-styles/package.json src/microsoft-trydotnet-styles/package-lock.json src/microsoft-trydotnet-styles/
COPY src/microsoft-learn-mock/package.json src/microsoft-learn-mock/package-lock.json src/microsoft-learn-mock/

# Restore NuGet packages. The cache mount persists the package cache across local
# builds; in CI the layer itself is cached by the GHA cache backend.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore --configfile /App/NuGet.config /App/TryDotNet.sln

# Copy all remaining source (changes frequently — only layers below rebuild on edits)
COPY . ./

# Build javascript library. The npm cache mount speeds up repeated local builds.
RUN --mount=type=cache,target=/root/.npm \
    /App/build-js.sh

# Build and publish a release
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish -c Release -o out /App/src/Microsoft.TryDotNet

# Build runtime image
FROM mcr.microsoft.com/dotnet/sdk:10.0-azurelinux3.0
ARG TRY_DOT_NET_BUILD_ID
WORKDIR /App

# Make sure we run bash
CMD ["bash"]

# Install runtime tools in a single layer
RUN --mount=type=cache,target=/var/cache/tdnf,sharing=locked \
    tdnf install -y procps

# Copy from build image
COPY --from=build-env /App/out .

# Set up to run and expose app on port 80
EXPOSE 80
ENV ASPNETCORE_URLS=http://*:80/

# This is a workaround for the fact that the Try .NET website is not yet container-aware
ENV TRY_DOT_NET_REQUEST_SCHEME=https
ENV TRY_DOT_NET_BUILD_ID=$TRY_DOT_NET_BUILD_ID
ENV TRY_DOT_NET_MANUAL_BUILD_ID=2 

# Run the Microsoft.TryDotNet website
ENTRYPOINT ["dotnet", "Microsoft.TryDotNet.dll"]
