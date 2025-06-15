# slskd

[![Build](https://img.shields.io/github/actions/workflow/status/slskd/slskd/ci.yml?branch=master&logo=github)](https://github.com/slskd/slskd/actions/workflows/ci.yml)
[![Docker Pulls](https://img.shields.io/docker/pulls/slskd/slskd?logo=docker)](https://hub.docker.com/r/slskd/slskd)
[![GitHub all releases](https://img.shields.io/github/downloads/slskd/slskd/total?logo=github&color=brightgreen)](https://github.com/slskd/slskd/releases)
[![Contributors](https://img.shields.io/github/contributors/slskd/slskd?logo=github)](https://github.com/slskd/slskd/graphs/contributors)
[![Discord](https://img.shields.io/discord/971446666257391616?label=Discord&logo=discord)](https://slskd.org/discord)
[![Matrix](https://img.shields.io/badge/Matrix-%3F%20online-na?logo=matrix&color=brightgreen)](https://slskd.org/matrix)

A modern client-server application for the [Soulseek](https://www.slsknet.org/news/) file-sharing network.

## Table of Contents
- [Features](#features)
- [Quick Start](#quick-start)
  - [With Docker](#with-docker)
  - [With Binaries](#with-binaries)
- [Configuration](#configuration)
- [HTTPS Setup](#https-setup)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)

## Features

### Secure access

slskd runs as a daemon or Docker container in your network (or in the cloud!) and is accessible from a web browser. It's designed to be exposed to the internet, and everything is secured with a token that [you can control](docs/config.md#authentication). It also supports [reverse proxies](docs/reverse_proxy.md), making it work well with other self-hosted tools.

![Secure Access](https://user-images.githubusercontent.com/17145758/193290217-0e6d87f5-a547-4451-8d90-d554a902716c.png)

### Search

Search for things just like you're used to with the official Soulseek client. slskd makes it easy to enter multiple searches quickly.

![Search](https://user-images.githubusercontent.com/17145758/193286989-30bd524d-81b6-4721-bd72-e4438c2b7b69.png)

### Results

Sort and filter search results using the same filters you use today. Dismiss results you're not interested in, and download the ones you want in a couple of clicks.

![Results](https://user-images.githubusercontent.com/17145758/193288396-dc3cc83d-6d93-414a-93f6-cea0696ac245.png)

### Downloads

Monitor the speed and status of downloads, grouped by user and folder. Click the progress bar to fetch your place in queue, and use the selection tools to cancel, retry, or clear completed downloads. Use the controls at the top to quickly manage downloads by status.

![Downloads](https://user-images.githubusercontent.com/17145758/193289840-3aee153f-3656-4f15-b086-8b1ca25d38bb.png)

### Pretty much everything else

slskd can do almost everything the official Soulseek client can; browse user shares, join chat rooms, privately chat with other users.

New features are added all the time!

### Enhanced Security

slskd now includes advanced security features to protect your instance:

-   **Security Headers**: Automatically adds HTTP security headers (CSP, HSTS, X-Content-Type-Options, X-Frame-Options, X-XSS-Protection, Referrer-Policy, and Public-Key-Pins) to mitigate common web vulnerabilities. These can be configured in `config/slskd.yml` under the `security.headers` section.
-   **Rate Limiting**: Protects authentication endpoints from brute-force attacks by limiting the number of login attempts per IP address. Configurable in `config/slskd.yml` under the `security.rateLimiting` section.

## Quick Start

### With Docker

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

### With Docker-Compose

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

This command or docker-compose file (depending on your choice) starts a container instance of slskd on ports 5030 (HTTP) and 5031 (HTTPS using a self-signed certificate). slskd begins listening for incoming connections on port 50300 and maps the application directory to the provided path.

Once the container is running you can access the web UI over HTTP on port 5030, or HTTPS on port 5031. The default username and password are `slskd` and `slskd`, respectively. You'll want to change these if the application will be internet facing.

The `SLSKD_REMOTE_CONFIGURATION` environment variable allows you to modify application configuration settings from the web UI. You might not want to enable this for an internet-facing installation.

You can find a more in-depth guide to running slskd in Docker in [docker.md](docs/docker.md).

### Using .env for Credentials

For secure credential management, create a `.env` file in your project root:

```env
SLSKD_USERNAME=your_soulseek_username
SLSKD_PASSWORD=your_soulseek_password
```

Then update your `docker-compose.yml` to reference these variables:

```yaml
env_file:
  - .env
environment:
  - SLSKD_USERNAME=${SLSKD_USERNAME}
  - SLSKD_PASSWORD=${SLSKD_PASSWORD}
```

**Security Note:** 
- Never commit `.env` files to source control
- Add `.env` to your `.gitignore` file
- Consider using Docker secrets for production deployments

### Troubleshooting Permission Issues

If you encounter errors like "Directory /app/data is not writeable":
- Add `:z` suffix to volume mounts in docker-compose.yml
- Set proper permissions on host directories:
  ```bash
  sudo chown -R $USER:$USER <path/to/application/data>
  sudo chmod -R 755 <path/to/application/data>
  ```
This is especially important on SELinux-enabled systems.

### Troubleshooting .env Issues

If your environment variables aren't loading:
1. Verify `.env` is in the same directory as `docker-compose.yml`
2. Check variable names match exactly (case-sensitive)
3. Restart container: `docker-compose up -d --force-recreate`
4. View variables: `docker-compose exec slskd env`

### With Binaries

The latest stable binaries can be downloaded from the [releases](https://github.com/slskd/slskd/releases) page. Platform-specific binaries and the static content for the Web UI are produced as artifacts from every [build](https://github.com/slskd/slskd/actions?query=workflow%3ACI) if you'd prefer to use a canary release.

Binaries are shipped as zip files; extract the zip to your chosen directory and run.

> **Note**: Ensure the application directory has proper write permissions:
> ```bash
> sudo chown -R $USER:$USER ~/.local/share/slskd
> sudo chmod -R 755 ~/.local/share/slskd
> ```
>
> An application directory will be created in either `~/.local/share/slskd` (on Linux and macOS) or `%localappdata%/slskd` (on Windows). In the root of this directory the file `slskd.yml` will be created the first time the application runs. Edit this file to enter your credentials for the Soulseek network, and tweak any additional settings using the [configuration guide](docs/config.md).

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

Caddy is an easy-to-use, open-source web server with automatic HTTPS. It's an excellent choice for proxying `slskd`.

For detailed configuration examples and instructions, please refer to [caddy.md](docs/caddy.md).

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
