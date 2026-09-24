# Dockerfile for ASP.NET 4.7.2 MVC application deployment on Render
FROM mono:latest

WORKDIR /app

# Copy all project files into container
COPY . /app

# Configure Debian archive fallback if on EOL Debian, then install mono-xsp4
RUN (sed -i 's/deb.debian.org/archive.debian.org/g' /etc/apt/sources.list 2>/dev/null || true) && \
    (sed -i 's|security.debian.org/debian-security|archive.debian.org/debian-security|g' /etc/apt/sources.list 2>/dev/null || true) && \
    (sed -i '/updates/d' /etc/apt/sources.list 2>/dev/null || true) && \
    apt-get update -o Acquire::Check-Valid-Until=false -o Acquire::AllowInsecureRepositories=true || true && \
    apt-get install -y --no-install-recommends mono-xsp4 || true

# Expose port 8080
EXPOSE 8080

# Run xsp4 web server with root pointing to Final directory if present, or /app
CMD ["sh", "-c", "if [ -d /app/Final ]; then xsp4 --nonstop --port=8080 --root=/app/Final; else xsp4 --nonstop --port=8080 --root=/app; fi"]
