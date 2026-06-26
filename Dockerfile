# 1. Use the official Microsoft .NET 10 SDK image to build the project binaries
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Copy everything and restore NuGet package lines
COPY . ./
RUN dotnet restore

# Build and publish a release container bundle 
RUN dotnet publish -c Release -o out

# 2. Build runtime image using ASP.NET Core 10 environment layer
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .

# Expose standard web traffic routing channels
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "ApexWallet.Api.dll"]