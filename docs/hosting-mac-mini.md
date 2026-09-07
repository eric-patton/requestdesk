# Hosting the demo on a Mac Mini behind a Cloudflare Tunnel

The public demo runs on a Mac Mini at home. Docker runs the compose stack; `cloudflared` opens an
outbound tunnel to Cloudflare, and Cloudflare routes a hostname on a domain in the account to it.
No ports are opened on the router, no inbound connection reaches the Mac except through the tunnel,
and the whole thing costs the price of the domain.

## Once, on the Mac

1. **Docker.** Docker Desktop for Mac, or OrbStack. Both build the images natively for Apple
   Silicon; every base image used here (dotnet, node, nginx, postgres) has an arm64 build.
2. **Never sleep.** System Settings, Energy: prevent automatic sleeping when the display is off,
   and start up after a power failure. `sudo pmset -a sleep 0 disablesleep 1` does the same from a
   shell. Docker Desktop should be set to start at login.
3. **Clone and configure.**

   ```sh
   git clone https://github.com/eric-patton/requestdesk.git ~/requestdesk
   cd ~/requestdesk
   cat > .env <<EOF
   POSTGRES_PASSWORD=$(openssl rand -base64 24 | tr -d '/+=')
   JWT_SIGNING_KEY=$(openssl rand -base64 48 | tr -d '/+=')
   EOF
   docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
   curl -s http://localhost:8085/health
   ```

   The `.env` file is gitignored and never leaves the machine. Those two values are the only secrets
   the stack has.

4. **The tunnel.**

   ```sh
   brew install cloudflared
   cloudflared tunnel login                 # opens a browser page; pick the domain
   cloudflared tunnel create requestdesk
   cloudflared tunnel route dns requestdesk requestdesk.<your-domain>
   ```

   Then `~/.cloudflared/config.yml`:

   ```yaml
   tunnel: <the tunnel id printed by create>
   credentials-file: /Users/<you>/.cloudflared/<the tunnel id>.json
   ingress:
     - hostname: requestdesk.<your-domain>
       service: http://localhost:8085
     - service: http_status:404
   ```

   And install it as a service so it survives reboots:

   ```sh
   sudo cloudflared service install
   ```

   Within a minute `https://requestdesk.<your-domain>` answers, with a certificate from Cloudflare.

## Updating

```sh
cd ~/requestdesk && git pull && docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

The API migrates its own database on startup, so a pull that carries a migration needs nothing extra.
Demo data resets on the hour anyway.

## What is exposed, and what is not

- Only nginx, on `127.0.0.1:8085`, and only through the tunnel. The API and PostgreSQL publish no
  ports in the production override.
- Cloudflare terminates TLS and passes the real client address in `CF-Connecting-IP`; nginx forwards
  it as `X-Forwarded-For`, and the API's sign-in rate limit keys on it.
- The demo seeds synthetic data and wipes it hourly. Uploads are capped, allowlisted, stored under
  random keys and never served from a static path. Nothing sends email.
- If the Mac goes down, so does the demo. The vault's monthly reminder to load the URL exists for
  exactly that reason.
