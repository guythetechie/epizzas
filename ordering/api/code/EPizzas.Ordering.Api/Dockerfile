#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0-preview AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0-preview AS build
WORKDIR /src
COPY ["api/code/EPizzas.Ordering.Api/EPizzas.Ordering.Api.csproj", "api/code/EPizzas.Ordering.Api/"]
RUN dotnet restore "api/code/EPizzas.Ordering.Api/EPizzas.Ordering.Api.csproj"
COPY . .
WORKDIR "/src/api/code/EPizzas.Ordering.Api"
RUN dotnet build "EPizzas.Ordering.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EPizzas.Ordering.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EPizzas.Ordering.Api.dll"]