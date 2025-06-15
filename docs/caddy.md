# Caddy Reverse Proxy Configuration

## Basic Configuration
```caddy
slskd.example.com {
    reverse_proxy localhost:5000 {
        header_up X-Forwarded-Proto {scheme}
        header_up X-Forwarded-Host {host}
    }
    encode gzip
}
```

## With HTTPS Termination
```caddy
slskd.example.com {
    tls your-email@example.com
    reverse_proxy localhost:5000 {
        header_up X-Forwarded-Proto https
        header_up X-Forwarded-Host {host}
    }
}
```

## WebSocket Support
```caddy
slskd.example.com {
    reverse_proxy localhost:5000 {
        header_up X-Forwarded-Proto {scheme}
        header_up X-Forwarded-Host {host}
        # Required for WebSocket proxying
        header_up Connection {upstream_connection}
        header_up Upgrade {upstream_upgrade}
    }
}
