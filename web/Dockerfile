# Build context is the repository root, to match api/Dockerfile.
#   docker build -f web/Dockerfile -t requestdesk-web .

FROM node:24-alpine AS build
WORKDIR /src
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npm run build -- --configuration production

FROM nginx:1.27-alpine AS runtime
# The nginx image runs envsubst over /etc/nginx/templates/*.template at startup, which is how
# API_UPSTREAM gets into the proxy configuration without a custom entrypoint.
COPY web/nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /src/dist/web/browser /usr/share/nginx/html
ENV API_UPSTREAM=http://api:8080
ENV DNS_RESOLVER=127.0.0.11
EXPOSE 80
HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=3 \
  CMD ["/bin/sh", "-c", "wget -q -O /dev/null http://127.0.0.1/ || exit 1"]
