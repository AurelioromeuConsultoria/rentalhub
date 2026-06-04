FROM node:24-alpine AS build
WORKDIR /app

COPY apps/Admin/package*.json ./
RUN npm ci

COPY apps/Admin ./
ARG VITE_API_BASE_URL
ENV VITE_API_BASE_URL=${VITE_API_BASE_URL}
RUN npm run build

FROM nginx:1.29-alpine AS runtime
ENV API_PROXY_TARGET=http://api:8080
COPY apps/Admin/nginx.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
