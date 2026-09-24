# Dockerfile for ASP.NET 4.7.2 MVC application deployment on Render
FROM mono:6.12.0.182

WORKDIR /app

# Copy all project files into container
COPY . /app

# Install mono-xsp4 server
RUN apt-get update && apt-get install -y mono-xsp4

# Expose port 8080
EXPOSE 8080

# Run xsp4 web server with root pointing to Final folder if present, or /app
CMD ["sh", "-c", "if [ -d /app/Final ]; then xsp4 --nonstop --port=8080 --root=/app/Final; else xsp4 --nonstop --port=8080 --root=/app; fi"]
