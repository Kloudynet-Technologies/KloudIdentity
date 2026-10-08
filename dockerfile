# syntax=docker/dockerfile:1.10
# (1.10+ is required for `--mount=type=secret,env=` used by the restore step below)

# Use the official Microsoft .NET SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory in the image to '/src'
WORKDIR /src

# Copy project files based on the context of SCIM Service
COPY . .

# Navigate to the API project folder
WORKDIR /src/Microsoft.SCIM.WebHostSample

# 🔐 Apply OS security patches (fix CVEs)
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
# Restore dependencies. KN.KI.LogAggregator.SerilogInitializer (1.0.5+) comes from the private GitHub
# Packages feed, which nuget.config authenticates via %KLOUDYNET_NUGET_USERNAME% / %KLOUDYNET_NUGET_PASSWORD%.
# They are passed as BuildKit secrets (not ARG) so the token is exposed only to this RUN step and never
# lands in an image layer or `docker history`. Build with:
#   docker build --secret id=nuget_user,env=KLOUDYNET_NUGET_USERNAME --secret id=nuget_pass,env=KLOUDYNET_NUGET_PASSWORD -f ./dockerfile .
RUN --mount=type=secret,id=nuget_user,env=KLOUDYNET_NUGET_USERNAME,required=true \
    --mount=type=secret,id=nuget_pass,env=KLOUDYNET_NUGET_PASSWORD,required=true \
    dotnet restore

# Build the application in Debug configuration
RUN dotnet build -c Debug --no-restore

# Publish the application to the 'publish' folder
RUN dotnet publish -c Debug -o /app --no-restore

# Use the official Microsoft .NET runtime image for running the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime

# Set the working directory in the image to '/app'
WORKDIR /app

# 🔐 Apply OS security patches (runtime CVEs) and install the ODBC stack.
# SQL-integrated apps (e.g. UTS Archival) are reached through System.Data.Odbc, which needs the
# unixODBC driver manager plus a driver registered in /etc/odbcinst.ini under the exact name the
# app config's Driver uses ("ODBC Driver 17 for SQL Server" / "ODBC Driver 18 for SQL Server").
# libgssapi-krb5-2 is a hard runtime dependency of the msodbcsql .so files that apt does not pull in
# with --no-install-recommends — without it unixODBC reports "Can't open lib ... file not found".
# curl/gnupg are only needed to add the Microsoft package repo and are removed afterwards.
RUN apt-get update \
    && apt-get upgrade -y \
    && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
        | gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
    && curl -fsSL https://packages.microsoft.com/config/debian/12/prod.list \
        -o /etc/apt/sources.list.d/mssql-release.list \
    && apt-get update \
    && ACCEPT_EULA=Y apt-get install -y --no-install-recommends unixodbc libgssapi-krb5-2 msodbcsql17 msodbcsql18 \
    && apt-get purge -y --auto-remove curl gnupg \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# 🔐 Trust the Amazon RDS certificate authorities (needed for TLS to Amazon RDS). Only adds CAs,
# so Azure SQL keeps working. The bundle holds many certificates and update-ca-certificates needs
# one per file, hence the split.
ADD https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem /tmp/rds-global-bundle.pem
RUN apt-get update && apt-get install -y --no-install-recommends ca-certificates \
 && awk 'BEGIN{n=0} /BEGIN CERTIFICATE/{n++} {print > ("/usr/local/share/ca-certificates/rds-" n ".crt")}' /tmp/rds-global-bundle.pem \
 && update-ca-certificates && rm -rf /tmp/rds-global-bundle.pem /var/lib/apt/lists/*

# Copy the build output from the 'publish' folder to '/app' in the image
COPY --from=build /app .

# Set the environment variable for ASP.NET Core to listen on port 80
ENV ASPNETCORE_URLS=http://+:80

# Expose port 80 in the image
EXPOSE 80

# Start the application
ENTRYPOINT ["dotnet", "Microsoft.SCIM.WebHostSample.dll"]
