
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY *.sln .
COPY *.csproj .
RUN dotnet restore

COPY *.cs .
COPY wwwroot/ /app/wwwroot/
WORKDIR /source/
RUN dotnet publish -c release --property:OutputPath=/app --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-azurelinux3.0-distroless
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["dotnet", "hello-k8s.dll"]