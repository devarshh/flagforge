# syntax=docker/dockerfile:1
# The dashboard. Build from the npm workspace root: docker build -f apps/dashboard/Dockerfile src/frontend

FROM node:26-alpine AS build
WORKDIR /workspace

# Install in its own layer, so it reruns only when a manifest or the lock file changes.
COPY package.json package-lock.json ./
COPY packages/sdk/package.json packages/sdk/
COPY apps/dashboard/package.json apps/dashboard/
COPY apps/demo/package.json apps/demo/
RUN npm ci

COPY tsconfig.base.json ./
COPY apps/dashboard/ apps/dashboard/
RUN npm run build -w apps/dashboard

FROM nginxinc/nginx-unprivileged:stable-alpine
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY apps/dashboard/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /workspace/apps/dashboard/dist /usr/share/nginx/html
EXPOSE 8080
