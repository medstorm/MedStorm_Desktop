FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["Cubist.5004.MedStorm.Desktop.Client/Cubist.5004.MedStorm.Desktop.Client.csproj", "Cubist.5004.MedStorm.Desktop.Client/"]
COPY ["Cubist.5004.MedStorm.Desktop.Core/Cubist.5004.MedStorm.Desktop.Core.csproj", "Cubist.5004.MedStorm.Desktop.Core/"]
RUN dotnet restore "Cubist.5004.MedStorm.Desktop.Client/Cubist.5004.MedStorm.Desktop.Client.csproj"
COPY . .
WORKDIR "/src/Cubist.5004.MedStorm.Desktop.Client"
RUN dotnet build "Cubist.5004.MedStorm.Desktop.Client.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Cubist.5004.MedStorm.Desktop.Client.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cubist.5004.MedStorm.Desktop.Client.dll"]