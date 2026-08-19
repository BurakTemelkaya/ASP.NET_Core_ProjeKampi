FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

ARG SIXLABORS_LICENSE
ENV SixLaborsLicenseKey=${SIXLABORS_LICENSE}

COPY . .

RUN dotnet restore "CoreDemo/CoreDemo.csproj"
RUN dotnet publish "CoreDemo/CoreDemo.csproj" -c Release -o /app/publish /p:UseAppHost=false

# YENİ EKLENEN SATIR: log4net.config dosyasını bul ve publish klasörüne kopyala
RUN find . -name "log4net.config" -exec cp {} /app/publish/ \;

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

RUN mkdir -p /app/wwwroot && chmod 777 /app/wwwroot

COPY --from=build /app/publish/. .

EXPOSE 8080
ENTRYPOINT ["dotnet", "CoreDemo.dll"]