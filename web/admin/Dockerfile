FROM node:24-alpine AS build
WORKDIR /app
COPY web/admin/package*.json ./
RUN npm ci
COPY web/admin/ ./
RUN npm run build

FROM nginx:1.29-alpine
COPY deploy/docker/admin-web.nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
