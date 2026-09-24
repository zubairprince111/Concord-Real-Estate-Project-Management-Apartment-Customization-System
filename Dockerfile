# Dockerfile for ASP.NET 4.7.2 MVC application deployment on Render
FROM mono:latest

WORKDIR /app

# Update apt and install mono-xsp4 web server
RUN apt-get update && apt-get install -y --no-install-recommends mono-xsp4 && rm -rf /var/lib/apt/lists/*

# Copy all project files into container
COPY . /app

# Expose port 8080
EXPOSE 8080

# Run xsp4 web server with root pointing to Final directory if present, or /app
CMD ["sh", "-c", "if [ -d /app/Final ]; then exec xsp4 --nonstop --port=8080 --root=/app/Final; else exec xsp4 --nonstop --port=8080 --root=/app; fi"]
