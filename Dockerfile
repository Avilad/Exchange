FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
COPY . /build
WORKDIR /build/Client
RUN dotnet publish -c Release -r linux-x64 --no-self-contained -o out
WORKDIR /build/Server
RUN dotnet publish -c Release -r linux-x64 --no-self-contained -o out

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS server
RUN adduser --disabled-password --gecos '' app
WORKDIR /app
COPY --from=build /build/Server/out ./
USER app
ENTRYPOINT ["dotnet", "Server.dll"]

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS client
RUN adduser --disabled-password --gecos '' app
WORKDIR /app
COPY --from=build /build/Client/out ./
USER app
ENTRYPOINT ["dotnet", "Client.dll"]