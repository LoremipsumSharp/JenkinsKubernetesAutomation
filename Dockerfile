FROM mcr.microsoft.com/dotnet/core/aspnet:2.1.11
WORKDIR /app
COPY  ./src/FM.Kube.Consul.Sync.Host/publish  /app
ENTRYPOINT ["dotnet", "FM.Kube.Consul.Sync.Host.dll"]