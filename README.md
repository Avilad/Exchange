# How to run

This project can be run locally using the .NET Core SDK, or inside Docker containers using Compose.

## .NET Core SDK
Run server: `dotnet run --project Server`
Run client: `dotnet run --project Client [...]`

## Docker Compose
Run server: `docker-compose up server`
Run client: `docker-compose run client [...]`


# Client Commands

The client supports all server operations via command-line arguments.

## Add Order
Syntax: `[client] a [symbol] [action] [price] [volume]`
Example: `docker-compose run client a RIO sell 123.45 20`

## Remove Order
Syntax: `[client] r [order_id]`
Example: `docker-compose run client r 9e870944-446d-43c9-834f-a4c9ce216c31`

## Best Price Feed
Syntax: `[client] b`
Example: `docker-compose run client b`

## Trade Feed
Syntax: `[client] t`
Example: `docker-compose run client t`