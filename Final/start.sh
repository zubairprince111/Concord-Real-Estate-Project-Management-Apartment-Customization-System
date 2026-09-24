#!/bin/sh
set -e

PORT="${PORT:-8080}"
APP_ROOT="/app"
if [ -d "/app/Final" ]; then
    APP_ROOT="/app/Final"
fi

mkdir -p /etc/nginx/sites-available /etc/nginx/sites-enabled

cat <<EOF > /etc/nginx/sites-available/default
server {
    listen ${PORT} default_server;
    listen [::]:${PORT} default_server;
    server_name _;

    root ${APP_ROOT};

    location / {
        fastcgi_pass 127.0.0.1:9000;
        include /etc/nginx/fastcgi_params;
        fastcgi_param SCRIPT_FILENAME \$document_root\$fastcgi_script_name;
        fastcgi_param PATH_INFO \$fastcgi_script_name;
        fastcgi_param PATH_TRANSLATED \$document_root\$fastcgi_script_name;
    }
}
EOF

ln -sf /etc/nginx/sites-available/default /etc/nginx/sites-enabled/default

echo "Starting fastcgi-mono-server4 for ${APP_ROOT}..."
fastcgi-mono-server4 /applications=/:${APP_ROOT} /socket=tcp:127.0.0.1:9000 &

echo "Starting Nginx on port ${PORT}..."
exec nginx -g "daemon off;"
