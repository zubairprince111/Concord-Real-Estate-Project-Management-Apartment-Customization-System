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

# Configure Nginx site to forward requests to FastCGI Mono Server
RUN echo 'server {\n\
    listen 8080 default_server;\n\
    server_name _;\n\
    location / {\n\
        root /app/Final;\n\
        fastcgi_index Index;\n\
        fastcgi_pass 127.0.0.1:9000;\n\
        include /etc/nginx/fastcgi_params;\n\
        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;\n\
        fastcgi_param PATH_INFO "";\n\
    }\n\
}' > /etc/nginx/sites-available/default

# Expose port 8080 for Render
EXPOSE 8080

# Start FastCGI Mono server in background and Nginx in foreground
CMD ["sh", "-c", "APP_ROOT=/app; if [ -d /app/Final ]; then APP_ROOT=/app/Final; fi; fastcgi-mono-server4 /applications=/:$APP_ROOT /socket=tcp:127.0.0.1:9000 & nginx -g 'daemon off;'"]
