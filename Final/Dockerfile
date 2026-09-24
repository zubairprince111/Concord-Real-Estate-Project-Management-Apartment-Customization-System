# Dockerfile for ASP.NET 4.7.2 MVC application deployment on Render (Nginx + FastCGI Mono Server)
FROM ubuntu:22.04

ENV DEBIAN_FRONTEND=noninteractive
WORKDIR /app

# Install Mono runtime, fastcgi-mono-server4 and Nginx
RUN apt-get update && \
    apt-get install -y --no-install-recommends mono-complete mono-fastcgi-server4 nginx ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# Copy project files
COPY . /app

# Grant execution permission to start.sh
RUN chmod +x /app/start.sh || chmod +x /app/Final/start.sh

# Expose port 8080
EXPOSE 8080

# Run start.sh
CMD ["sh", "-c", "if [ -f /app/start.sh ]; then exec /app/start.sh; else exec /app/Final/start.sh; fi"]
