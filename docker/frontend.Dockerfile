FROM node:22-alpine AS build
WORKDIR /src
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
# The app calls the API on a relative /api path (same origin as the SPA),
# so no API base URL needs to be baked in at build time.
RUN npm run build

FROM nginx:1.27-alpine AS runtime
COPY docker/frontend.nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
