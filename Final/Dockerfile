# Dockerfile for ASP.NET 4.7.2 MVC application deployment on Render
FROM mono:6.12.0.182

# Set working directory
WORKDIR /app

# Copy project files
COPY . /app

# Expose port (Render automatically maps PORT environment variable)
EXPOSE 8080

# Install mono-xsp4 server
RUN apt-get update && apt-get install -y mono-xsp4

# Run xsp4 web server on port 8080
CMD ["xsp4", "--nonstop", "--port=8080", "--root=/app"]
