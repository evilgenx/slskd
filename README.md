# slskd

[![Docker Pulls](https://img.shields.io/docker/pulls/slskd/slskd?logo=docker)](https://hub.docker.com/r/slskd/slskd)
[![Docker Image Size](https://img.shields.io/docker/image-size/slskd/slskd/latest?logo=docker)](https://hub.docker.com/r/slskd/slskd)
[![Docker Version](https://img.shields.io/docker/v/slskd/slskd?logo=docker)](https://hub.docker.com/r/slskd/slskd)
[![Build](https://img.shields.io/github/actions/workflow/status/slskd/slskd/ci.yml?branch=master&logo=github)](https://github.com/slskd/slskd/actions/workflows/ci.yml)
[![GitHub Downloads](https://img.shields.io/github/downloads/slskd/slskd/total?logo=github&color=brightgreen)](https://github.com/slskd/slskd/releases)
[![Discord](https://img.shields.io/discord/971446666257391616?label=Discord&logo=discord)](https://slskd.org/discord)

A modern client-server application for the [Soulseek](https://www.slsknet.org/news/) file-sharing network.

> **Note for Docker Hub Users**: This README is optimized for both GitHub and Docker Hub. Some visual elements may appear differently on Docker Hub.

## Table of Contents
- [Docker Quick Start](#docker-quick-start)
- [Features](#features)
- [For GitHub Users](#for-github-users)
- [Configuration](#configuration)
- [HTTPS Setup](#https-setup)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)

## Features

- **Secure Access**: Token-based authentication with reverse proxy support
- **Advanced Search**: Multiple concurrent searches with filtering
- **Results Management**: Sort, filter, and download search results
- **Download Monitoring**: Track progress and manage queues
- **User Shares**: Browse other users' shared files
- **Chat Rooms**: Join and participate in group discussions
- **Private Messaging**: Direct communication with other users
- **Enhanced Security**:
  - Automatic HTTP security headers (CSP, HSTS, etc.)
  - Authentication rate limiting
  - Secure token-based access

## Docker Quick Start

### Basic Docker Run

```shell
docker run -d \
  -p 5030:5030 \
  -p 5031:5031 \
  -p 50300:50300 \
  -e SLSKD_REMOTE_CONFIGURATION=true \
  -v <path/to/application/data>:/app \
  --name slskd \
  slskd/slskd:latest
```

### Docker Compose

```yaml
version: "2"
services:
  slskd:
    image: slskd/slskd
    container_name: slskd
    ports:
      - "5030:5030"
      - "5031:5031"
      - "50300:50300"
    env_file:
      - .env
    environment:
      - SLSKD_HTTP_PORT=5030
      - SLSKD_HTTPS_PORT=5031
      - SLSKD_SLSK_LISTEN_PORT=50300
      - SLSKD_APP_DIR=/app
      - SLSKD_REMOTE_CONFIGURATION=true
      - SLSKD_USERNAME=${SLSKD_USERNAME}
      - SLSKD_PASSWORD=${SLSKD_PASSWORD}
    volumes:
      - <path/to/application/data>:/app:z
    restart: always
```

**Getting Started:**
1. Create `.env` file with your credentials
2. Start container: `docker-compose up -d`
3. Access web UI at `http://localhost:5030`
4. Default credentials: `slskd`/`slskd` (change immediately!)

**Security Recommendations:**
- Disable `SLSKD_REMOTE_CONFIGURATION` for production
- Use Docker secrets instead of `.env` for sensitive data
- Add `.env` to `.gitignore` to prevent accidental commits

> Full Docker guide: [docker.md](docs/docker.md)

## For GitHub Users

### Binary Installation

Download the latest binaries from [releases](https://github.com/slskd/slskd/releases). Extract the zip file to your preferred directory and run the executable.

**Permissions Note:**
```bash
sudo chown -R $USER:$USER ~/.local/share/slskd
sudo chmod -R 755 ~/.local/share/slskd
```

The application creates:
- `~/.local/share/slskd` (Linux/macOS)
- `%localappdata%/slskd` (Windows)

Edit `slskd.yml` for configuration.

### Development & Contribution

```bash
git clone https://github.com/slskd/slskd.git
cd slskd
dotnet build
dotnet run --project src/slskd
```

See our [CONTRIBUTING guidelines](CONTRIBUTING.md) for details.

## HTTPS Setup

For production deployments, you should configure HTTPS with a valid certificate instead of using the self-signed certificate. Here's how to set up Let's Encrypt with Nginx reverse proxy:

1. Install Nginx and Certbot:
```bash
sudo apt install nginx certbot python3-certbot-nginx
```

2. Create Nginx configuration (`/etc/nginx/sites-available/slskd`):
```nginx
server {
    listen 80;
    server_name yourdomain.com;
    
    location / {
        proxy_pass http://localhost:5030;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

3. Enable the site and obtain certificate:
```bash
sudo ln -s /etc/nginx/sites-available/slskd /etc/nginx/sites-enabled/
sudo certbot --nginx -d yourdomain.com
```

4. Update slskd configuration:
```yaml
web:
  https:
    disabled: true # Disable built-in HTTPS
  urlBase: https://yourdomain.com
```

5. Configure reverse proxy in docker-compose:
```yaml
ports:
  - "127.0.0.1:5030:5030" # Only expose internally
```

## Reverse Proxy with Caddy

Caddy is an easy-to-use, open-source web server with automatic HTTPS. It's an excellent choice for proxying `slskd`. Here's a detailed configuration guide:

### Basic Caddyfile Configuration
```Caddyfile
yourdomain.com {
    reverse_proxy localhost:5030 {
        header_up X-Real-IP {remote_host}
    }
}
```

### Configuration with Custom Headers
```Caddyfile
yourdomain.com {
    # Security headers
    header {
        Strict-Transport-Security "max-age=31536000;"
        X-Content-Type-Options "nosniff"
        X-Frame-Options "SAMEORIGIN"
        Content-Security-Policy "default-src 'self';"
    }

    # Reverse proxy to slskd
    reverse_proxy localhost:5030
}
```

### Docker Compose Integration
```yaml
version: "3"
services:
  slskd:
    image: slskd/slskd
    # ... [other slskd configuration]
    ports:
      - "127.0.0.1:5030:5030"  # Internal only

  caddy:
    image: caddy:latest
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy_data:/data
      - caddy_config:/config

volumes:
  caddy_data:
  caddy_config:
```

### Automatic HTTPS Features
Caddy automatically:
1. Obtains and renews Let's Encrypt certificates
2. Redirects HTTP → HTTPS
3. Enables HTTP/2 and HTTP/3 (QUIC)
4. Implements best-practice security headers

For advanced configurations including load balancing and caching, see [caddy.md](docs/caddy.md).

## Configuration

Once running, log in to the web UI using the default username `slskd` and password `slskd` to complete the configuration.

Detailed documentation for configuration options can be found in [config.md](docs/config.md), and an example of the YAML configuration file can be reviewed in [slskd.example.yml](config/slskd.example.yml).

For advanced configuration options, see the [configuration guide](docs/config.md).

## Contributing

We welcome contributions! Please see our [CONTRIBUTING guidelines](CONTRIBUTING.md) for details on how to submit pull requests, report issues, and suggest improvements.

### Development Setup
1. Clone the repository
2. Install .NET 9.0 SDK and Node.js 18
3. Run `dotnet build` in the root directory
4. Start the development server with `dotnet run --project src/slskd`
