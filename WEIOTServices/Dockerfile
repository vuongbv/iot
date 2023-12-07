#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["WEIOTServices/src/WE.IOT.Service/WE.IOT.Service.csproj", "WEIOTServices/src/WE.IOT.Service/"]
COPY ["WEIOTServices/src/WE.IOT.Service.Application/WE.IOT.Service.Application.csproj", "WEIOTServices/src/WE.IOT.Service.Application/"]
COPY ["WEIOTServices/src/WE.IOT.Service.Business.Store/WE.IOT.Service.Domain.csproj", "WEIOTServices/src/WE.IOT.Service.Business.Store/"]
COPY ["WE.Common/src/Common.csproj", "WE.Common/src/"]
RUN dotnet restore "WEIOTServices/src/WE.IOT.Service/WE.IOT.Service.csproj"
COPY . .
WORKDIR "/src/WEIOTServices/src/WE.IOT.Service"
RUN dotnet build "WE.IOT.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WE.IOT.Service.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WE.IOT.Service.dll"]