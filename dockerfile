# Use the official Microsoft .NET SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory in the image to '/src'
WORKDIR /src

# Copy project files based on the context of LoggerService
COPY . .


# Navigate to the API project folder
WORKDIR /src/Microsoft.SCIM.WebHostSample

# Restore dependencies
RUN dotnet restore

# Build the application in Debug configuration
RUN dotnet build -c Debug --no-restore

# Publish the application to the 'publish' folder
RUN dotnet publish -c Debug -o /app --no-restore

# Use the official Microsoft .NET runtime image for running the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Set the working directory in the image to '/app'
WORKDIR /app

# Copy the build output from the 'publish' folder to '/app' in the image
COPY --from=build /app .

# Set the environment variable for ASP.NET Core to listen on port 80
ENV ASPNETCORE_URLS=http://+:80

# Expose port 80 in the image
EXPOSE 80

# Start the application
ENTRYPOINT ["dotnet", "Microsoft.SCIM.WebHostSample.dll"]
