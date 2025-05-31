FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["MedicalData.Client.csproj", "MedicalData.Client/"]
WORKDIR "/src/MedicalData.Client"
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/dist

# Статический хост через nginx
FROM nginx:alpine AS final
COPY nginx.conf /etc/nginx/nginx.conf

COPY --from=build /app/dist/wwwroot /usr/share/nginx/html
EXPOSE 80