# Windows Server 2019 (1809) Nano Server runtime image for .NET 8.0
FROM mcr.microsoft.com/dotnet/aspnet:8.0-nanoserver-1809

WORKDIR C:/app
COPY publish/ ./

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "DotNetDemoApi.dll"]

