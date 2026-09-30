# syntax=docker/dockerfile:1
# The Acme Coffee demo. Build from the npm workspace root: docker build -f apps/demo/Dockerfile src/frontend

FROM node:24-alpine AS build
WORKDIR /workspace

# Install in its own layer, so it reruns only when a manifest or the lock file changes.
COPY package.json package-lock.json ./
COPY packages/sdk/package.json packages/sdk/
COPY apps/dashboard/package.json apps/dashboard/
COPY apps/demo/package.json apps/demo/
RUN npm ci

COPY tsconfig.base.json ./
COPY packages/sdk/ packages/sdk/
COPY apps/demo/ apps/demo/
RUN npm run build -w packages/sdk && npm run build -w apps/demo

FROM nginxinc/nginx-unprivileged:stable-alpine
# Served at /demo/config.json; set it when the container starts (Compose, Kubernetes).
ENV DEMO_SDK_KEY=""
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY apps/demo/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /workspace/apps/demo/dist /usr/share/nginx/html/demo
EXPOSE 8080
